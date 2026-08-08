import {
  ArrowLeft,
  Books,
  IdentificationCard,
  Tag,
} from '@phosphor-icons/react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { BookCard } from '../../components/books/BookCard'
import { CatalogFollowButton } from '../../components/catalog/CatalogFollowButton'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingGrid } from '../../components/ui/States'
import { useAuthor, useBooks, useCategory } from '../../hooks/useCatalog'
import type { Author, Category } from '../../types/domain'

type MetadataKind = 'author' | 'category'
const pageSize = 12

function DetailSkeleton() {
  return (
    <div className="container-page section-space animate-pulse" aria-label="Đang tải hồ sơ catalog">
      <div className="surface grid gap-6 p-6 sm:grid-cols-[8rem_1fr] sm:p-8">
        <div className="h-32 w-32 rounded-3xl bg-surface-muted" />
        <div className="space-y-4 py-2">
          <div className="h-4 w-28 rounded bg-surface-muted" />
          <div className="h-10 max-w-lg rounded bg-surface-muted" />
          <div className="h-4 max-w-2xl rounded bg-surface-muted" />
          <div className="h-4 max-w-xl rounded bg-surface-muted" />
        </div>
      </div>
      <div className="mt-12">
        <LoadingGrid count={pageSize} />
      </div>
    </div>
  )
}

function CatalogMetadataDetailPage({ kind }: { kind: MetadataKind }) {
  const { id } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const parsedPage = Number(searchParams.get('page'))
  const page = Number.isInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1
  const author = useAuthor(kind === 'author' ? id : undefined)
  const category = useCategory(kind === 'category' ? id : undefined)
  const metadataQuery = kind === 'author' ? author : category
  const books = useBooks({
    authorId: kind === 'author' ? id : undefined,
    categoryId: kind === 'category' ? id : undefined,
    sort: 'title',
    page,
    pageSize,
  })

  if (metadataQuery.isLoading) return <DetailSkeleton />

  if (metadataQuery.isError || !metadataQuery.data) {
    return (
      <div className="container-page section-space">
        <ErrorState
          message={
            kind === 'author'
              ? 'Không thể tải hồ sơ tác giả này.'
              : 'Không thể tải thông tin thể loại này.'
          }
          retry={() => void metadataQuery.refetch()}
        />
        <Link
          to="/books"
          className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-accent-strong"
        >
          <ArrowLeft size={17} />
          Quay lại catalog
        </Link>
      </div>
    )
  }

  const metadata = metadataQuery.data as Author | Category
  const detail =
    kind === 'author'
      ? (metadata as Author).biography
      : (metadata as Category).description
  const totalBooks = books.data?.totalItems ?? metadata.bookCount ?? 0

  const changePage = (nextPage: number) => {
    const next = new URLSearchParams(searchParams)
    if (nextPage > 1) next.set('page', String(nextPage))
    else next.delete('page')
    setSearchParams(next)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <div className="container-page section-space">
      <Link
        to="/books"
        className="inline-flex items-center gap-2 text-sm font-semibold text-muted transition-colors hover:text-accent-strong focus-visible:focus-ring"
      >
        <ArrowLeft size={17} />
        Catalog sách
      </Link>

      <section className="surface mt-6 overflow-hidden p-6 sm:p-8" aria-labelledby="metadata-title">
        <div className="grid gap-6 sm:grid-cols-[8rem_1fr] sm:items-center">
          {kind === 'author' && (metadata as Author).avatarUrl ? (
            <img
              src={(metadata as Author).avatarUrl}
              alt={`Ảnh đại diện của ${metadata.name}`}
              className="h-32 w-32 rounded-3xl border border-border object-cover shadow-cover"
            />
          ) : (
            <div className="grid h-32 w-32 place-items-center rounded-3xl border border-border bg-accent-soft text-accent-strong">
              {kind === 'author' ? (
                <IdentificationCard size={48} weight="duotone" aria-hidden />
              ) : (
                <Tag size={48} weight="duotone" aria-hidden />
              )}
            </div>
          )}
          <div className="min-w-0">
            <p className="eyebrow">{kind === 'author' ? 'Hồ sơ tác giả' : 'Không gian thể loại'}</p>
            <h1 id="metadata-title" className="page-title mt-3">
              {metadata.name}
            </h1>
            <p className="mt-4 max-w-3xl whitespace-pre-line text-base leading-7 text-body">
              {detail ||
                (kind === 'author'
                  ? 'Tiểu sử tác giả đang được cập nhật.'
                  : 'Mô tả thể loại đang được cập nhật.')}
            </p>
            <div className="mt-5 flex flex-wrap items-center gap-3">
              <span className="inline-flex items-center gap-2 rounded-full bg-surface-muted px-3 py-1.5 text-sm font-semibold text-heading">
                <Books size={17} aria-hidden />
                {totalBooks} cuốn sách
              </span>
              <CatalogFollowButton kind={kind} id={metadata.id} />
            </div>
          </div>
        </div>
      </section>

      <section className="mt-12" aria-labelledby="metadata-books-title">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="eyebrow">Danh mục đọc</p>
            <h2 id="metadata-books-title" className="mt-2 text-2xl font-bold tracking-tight text-heading">
              {kind === 'author' ? `Sách của ${metadata.name}` : `Sách thuộc ${metadata.name}`}
            </h2>
          </div>
          {books.data?.totalItems ? (
            <p className="text-sm font-medium text-muted" aria-live="polite">
              Trang {books.data.page}/{Math.max(books.data.totalPages, 1)}
            </p>
          ) : null}
        </div>

        <div className="mt-7">
          {books.isLoading ? <LoadingGrid count={pageSize} /> : null}
          {books.isError ? (
            <ErrorState
              message="Không thể tải danh sách sách."
              retry={() => void books.refetch()}
            />
          ) : null}
          {books.data && books.data.items.length === 0 ? (
            <EmptyState
              title="Chưa có sách trong danh mục"
              description="Metadata đã sẵn sàng nhưng chưa có cuốn sách active nào được liên kết."
              icon={Books}
              action={
                <Link to="/books" className="button button-secondary button-sm">
                  Khám phá catalog
                </Link>
              }
            />
          ) : null}
          {books.data?.items.length ? (
            <>
              <div className="book-grid">
                {books.data.items.map((book) => (
                  <BookCard key={book.id} book={book} />
                ))}
              </div>
              <Pagination
                className="mt-10"
                page={books.data.page}
                totalPages={books.data.totalPages}
                disabled={books.isFetching}
                onPageChange={changePage}
              />
            </>
          ) : null}
        </div>
      </section>
    </div>
  )
}

export function AuthorDetailPage() {
  return <CatalogMetadataDetailPage kind="author" />
}

export function CategoryDetailPage() {
  return <CatalogMetadataDetailPage kind="category" />
}
