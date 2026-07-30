import { Funnel, MagnifyingGlass } from '@phosphor-icons/react'
import { useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { BookCard } from '../../components/books/BookCard'
import { Button } from '../../components/ui/Button'
import { ErrorState, LoadingGrid } from '../../components/ui/States'
import { useBooks, useCategories } from '../../hooks/useCatalog'

export function BooksPage() {
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState(params.get('search') ?? '')
  const query = useMemo(
    () => ({
      search: params.get('search') || undefined,
      categoryId: params.get('categoryId') || undefined,
      sort: params.get('sort') || 'newest',
      page: Number(params.get('page') || 1),
      pageSize: 12,
    }),
    [params],
  )
  const books = useBooks(query)
  const categories = useCategories()
  const result = books.data

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

  return (
    <div className="container-page section-space">
      <div className="max-w-3xl">
        <p className="eyebrow">Catalog độc lập</p>
        <h1 className="page-title mt-4">Tìm cuốn sách phù hợp với thời điểm này.</h1>
        <p className="section-copy mt-4">
          Tìm theo tên, tác giả, ISBN hoặc chủ đề rồi thêm thẳng vào thư viện cá nhân.
        </p>
      </div>

      <form onSubmit={submitSearch} className="mt-10 flex flex-col gap-3 sm:flex-row">
        <div className="relative flex-1">
          <label htmlFor="catalog-search" className="sr-only">
            Tìm trong catalog
          </label>
          <MagnifyingGlass
            size={19}
            className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
          />
          <input
            id="catalog-search"
            className="input pl-11"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Tên sách, tác giả hoặc ISBN"
          />
        </div>
        <Button type="submit">Tìm sách</Button>
      </form>

      <div className="mt-6 flex flex-col gap-3 border-y border-border py-4 md:flex-row md:items-center">
        <div className="flex items-center gap-2 text-sm font-semibold text-heading">
          <Funnel size={18} />
          Bộ lọc
        </div>
        <select
          className="input md:w-52"
          value={query.categoryId ?? ''}
          onChange={(event) => updateParam('categoryId', event.target.value || undefined)}
          aria-label="Lọc theo chủ đề"
        >
          <option value="">Tất cả chủ đề</option>
          {categories.data?.items.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
        <select
          className="input md:ml-auto md:w-48"
          value={query.sort}
          onChange={(event) => updateParam('sort', event.target.value)}
          aria-label="Sắp xếp sách"
        >
          <option value="newest">Mới cập nhật</option>
          <option value="popular">Được đọc nhiều</option>
          <option value="rating">Điểm cao nhất</option>
          <option value="title">Tên A đến Z</option>
        </select>
      </div>

      <div className="mt-8">
        {books.isLoading ? (
          <LoadingGrid count={12} />
        ) : books.isError ? (
          <ErrorState message="Không thể tải catalog sách." retry={() => void books.refetch()} />
        ) : result?.items.length ? (
          <>
            <div className="mb-6 flex items-center justify-between gap-4">
              <p className="text-sm text-muted">
                Tìm thấy <strong className="text-heading">{result.totalItems}</strong> cuốn sách
              </p>
              <p className="text-sm text-muted">
                Trang {result.page}/{Math.max(result.totalPages, 1)}
              </p>
            </div>
            <div className="book-grid">
              {result.items.map((book) => (
                <BookCard key={book.id} book={book} />
              ))}
            </div>
            {result.totalPages > 1 ? (
              <div className="mt-10 flex justify-center gap-2">
                <Button
                  variant="secondary"
                  disabled={result.page <= 1}
                  onClick={() => updateParam('page', String(result.page - 1))}
                >
                  Trang trước
                </Button>
                <Button
                  variant="secondary"
                  disabled={result.page >= result.totalPages}
                  onClick={() => updateParam('page', String(result.page + 1))}
                >
                  Trang sau
                </Button>
              </div>
            ) : null}
          </>
        ) : (
          <div className="empty-state">
            <MagnifyingGlass size={30} className="text-accent-strong" />
            <h2 className="mt-4 text-lg font-semibold text-heading">Không có kết quả phù hợp</h2>
            <p className="mt-2 text-sm text-muted">Thử từ khóa ngắn hơn hoặc bỏ bớt bộ lọc.</p>
            <Button
              variant="secondary"
              className="mt-5"
              onClick={() => {
                setSearch('')
                setParams({})
              }}
            >
              Xóa bộ lọc
            </Button>
          </div>
        )}
      </div>
    </div>
  )
}
