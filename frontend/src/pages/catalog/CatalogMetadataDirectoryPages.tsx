import { Books, MagnifyingGlass, Tag, UserCircle } from '@phosphor-icons/react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CatalogFollowButton } from '../../components/catalog/CatalogFollowButton'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import {
  useMetadataDirectory,
  type MetadataDirectoryKind,
} from '../../hooks/useCatalog'
import { errorMessage } from '../../lib/api'
import type { Author, Category } from '../../types/domain'

const PAGE_SIZE = 12

type MetadataItem = Author | Category

const copy = {
  author: {
    eyebrow: 'Người viết tạo nên catalog',
    title: 'Khám phá tác giả',
    description: 'Tìm người viết theo tên hoặc tiểu sử, rồi mở toàn bộ sách của họ trên BookSpace.',
    searchPlaceholder: 'Tên tác giả hoặc từ khóa tiểu sử',
    searchLabel: 'Tìm tác giả',
    resultLabel: 'tác giả',
    emptyTitle: 'Không tìm thấy tác giả phù hợp',
    emptyDescription: 'Hãy thử tên ngắn hơn hoặc xóa từ khóa để xem toàn bộ tác giả.',
  },
  category: {
    eyebrow: 'Lối vào những thế giới đọc',
    title: 'Khám phá thể loại',
    description: 'Duyệt chủ đề theo tên hoặc mô tả và đi thẳng tới những cuốn sách phù hợp.',
    searchPlaceholder: 'Tên thể loại hoặc từ khóa mô tả',
    searchLabel: 'Tìm thể loại',
    resultLabel: 'thể loại',
    emptyTitle: 'Không tìm thấy thể loại phù hợp',
    emptyDescription: 'Hãy thử từ khóa khác hoặc xóa bộ lọc để xem toàn bộ thể loại.',
  },
} as const

function itemDescription(kind: MetadataDirectoryKind, item: MetadataItem) {
  return kind === 'author'
    ? (item as Author).biography
    : (item as Category).description
}

function CatalogMetadataDirectoryPage({ kind }: { kind: MetadataDirectoryKind }) {
  const [params, setParams] = useSearchParams()
  const searchParam = params.get('search')?.trim() ?? ''
  const sort = params.get('sort') === 'bookCount' ? 'bookCount' : 'name'
  const page = Math.max(1, Number(params.get('page')) || 1)
  const [search, setSearch] = useState(searchParam)
  const directory = useMetadataDirectory(kind, {
    search: searchParam || undefined,
    sort,
    page,
    pageSize: PAGE_SIZE,
  })
  const labels = copy[kind]

  useEffect(() => setSearch(searchParam), [searchParam])

  const updateParam = (key: string, value?: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key !== 'page') next.delete('page')
    setParams(next)
  }

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    updateParam('search', search.trim() || undefined)
  }

  const clearFilters = () => {
    setSearch('')
    setParams({})
  }

  return (
    <div className="container-page section-space">
      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-end">
        <div>
          <p className="eyebrow">{labels.eyebrow}</p>
          <h1 className="page-title mt-4">{labels.title}</h1>
          <p className="mt-4 max-w-2xl text-base leading-7 text-muted">{labels.description}</p>
          <div className="mt-6 flex flex-wrap gap-2" aria-label="Chọn loại danh mục">
            <Link
              to="/authors"
              className={`button button-sm ${kind === 'author' ? 'button-primary' : 'button-secondary'}`}
            >
              <UserCircle size={17} aria-hidden />
              Tác giả
            </Link>
            <Link
              to="/categories"
              className={`button button-sm ${kind === 'category' ? 'button-primary' : 'button-secondary'}`}
            >
              <Tag size={17} aria-hidden />
              Thể loại
            </Link>
          </div>
        </div>

        <form onSubmit={submitSearch} className="surface p-4">
          <label htmlFor={`${kind}-directory-search`} className="text-sm font-semibold text-heading">
            {labels.searchLabel}
          </label>
          <div className="relative mt-2">
            <MagnifyingGlass
              size={18}
              className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
              aria-hidden
            />
            <input
              id={`${kind}-directory-search`}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              className="input pl-10"
              placeholder={labels.searchPlaceholder}
              maxLength={200}
            />
          </div>
          <Button type="submit" size="sm" className="mt-3 w-full">
            Tìm kiếm
          </Button>
        </form>
      </div>

      <div className="mt-12 flex flex-wrap items-center justify-between gap-4 border-t border-border pt-8">
        <p className="text-sm text-muted" aria-live="polite">
          {directory.data ? (
            <>
              Tìm thấy <strong className="text-heading">{directory.data.totalItems}</strong>{' '}
              {labels.resultLabel}
            </>
          ) : (
            'Đang cập nhật kết quả'
          )}
        </p>
        <label className="flex items-center gap-3 text-sm font-medium text-muted">
          Sắp xếp
          <select
            className="input w-48"
            value={sort}
            onChange={(event) => updateParam('sort', event.target.value)}
            aria-label={`Sắp xếp ${labels.resultLabel}`}
          >
            <option value="name">Tên A đến Z</option>
            <option value="bookCount">Nhiều sách nhất</option>
          </select>
        </label>
      </div>

      <div className="mt-7">
        {directory.isLoading ? <LoadingRows count={6} /> : null}
        {directory.isError ? (
          <ErrorState
            message={errorMessage(
              directory.error,
              `Không thể tải danh sách ${labels.resultLabel}. Vui lòng thử lại.`,
            )}
            retry={() => void directory.refetch()}
          />
        ) : null}
        {directory.data?.items.length ? (
          <>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {directory.data.items.map((item) => {
                const description = itemDescription(kind, item)
                return (
                  <article
                    key={item.id}
                    className="surface group flex min-w-0 flex-col p-5 transition hover:-translate-y-0.5 hover:border-accent/50"
                  >
                    <Link
                      to={`/${kind === 'author' ? 'authors' : 'categories'}/${item.id}`}
                      className="flex min-w-0 flex-1 gap-4 focus-visible:focus-ring"
                    >
                      {kind === 'author' ? (
                        <Avatar src={(item as Author).avatarUrl} name={item.name} size="lg" />
                      ) : (
                        <span className="grid h-14 w-14 shrink-0 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
                          <Tag size={25} weight="duotone" aria-hidden />
                        </span>
                      )}
                      <span className="min-w-0">
                        <span className="block truncate text-base font-bold text-heading transition-colors group-hover:text-accent-strong">
                          {item.name}
                        </span>
                        <span className="mt-1 block text-xs font-semibold uppercase tracking-wider text-accent-strong">
                          {item.bookCount ?? 0} cuốn sách
                        </span>
                        <span className="mt-2 line-clamp-2 block text-sm leading-6 text-muted">
                          {description || 'Thông tin đang được cập nhật.'}
                        </span>
                      </span>
                    </Link>
                    <div className="mt-4 border-t border-border pt-4">
                      <CatalogFollowButton kind={kind} id={item.id} compact />
                    </div>
                  </article>
                )
              })}
            </div>
            <Pagination
              page={directory.data.page}
              totalPages={directory.data.totalPages}
              onPageChange={(nextPage) => updateParam('page', String(nextPage))}
              disabled={directory.isFetching}
              className="mt-9"
            />
          </>
        ) : null}
        {directory.data && directory.data.totalItems === 0 ? (
          <EmptyState
            title={labels.emptyTitle}
            description={labels.emptyDescription}
            icon={kind === 'author' ? UserCircle : Tag}
            action={
              <Button variant="secondary" size="sm" onClick={clearFilters}>
                Xóa bộ lọc
              </Button>
            }
          />
        ) : null}
      </div>

      <div className="mt-12 flex justify-center">
        <Link to="/books" className="button button-secondary button-md">
          <Books size={18} aria-hidden />
          Xem toàn bộ catalog sách
        </Link>
      </div>
    </div>
  )
}

export function AuthorsDirectoryPage() {
  return <CatalogMetadataDirectoryPage kind="author" />
}

export function CategoriesDirectoryPage() {
  return <CatalogMetadataDirectoryPage kind="category" />
}
