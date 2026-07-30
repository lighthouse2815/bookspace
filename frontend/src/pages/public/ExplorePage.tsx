import { ArrowRight, Books, MagnifyingGlass, UsersThree } from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { BookCard } from '../../components/books/BookCard'
import { ErrorState, LoadingGrid, LoadingRows } from '../../components/ui/States'
import { useBooks, useCategories } from '../../hooks/useCatalog'
import { useChallenges, useClubs } from '../../hooks/useSocialProduct'

export function ExplorePage() {
  const [search, setSearch] = useState('')
  const navigate = useNavigate()
  const books = useBooks({ sort: 'popular', page: 1, pageSize: 8 })
  const categories = useCategories()
  const clubs = useClubs()
  const challenges = useChallenges()

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const query = search.trim()
    navigate(query ? `/books?search=${encodeURIComponent(query)}` : '/books')
  }

  return (
    <div className="container-page section-space">
      <div className="grid gap-8 lg:grid-cols-[1fr_22rem] lg:items-end">
        <div>
          <p className="eyebrow">Khám phá BookSpace</p>
          <h1 className="page-title mt-4 max-w-3xl">
            Tìm một cuốn sách, một thử thách hoặc một nhóm để bắt đầu.
          </h1>
        </div>
        <form onSubmit={submit} className="relative">
          <label htmlFor="explore-search" className="sr-only">
            Tìm kiếm sách
          </label>
          <MagnifyingGlass
            size={19}
            className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
          />
          <input
            id="explore-search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            className="input pl-11"
            placeholder="Tên sách, tác giả, ISBN"
          />
        </form>
      </div>

      <section className="mt-14">
        <div className="flex items-end justify-between gap-4">
          <div>
            <h2 className="text-2xl font-bold tracking-tight text-heading">Được đọc nhiều</h2>
            <p className="mt-2 text-sm text-muted">Những tựa sách đang tạo nên nhiều cuộc trò chuyện.</p>
          </div>
          <Link to="/books" className="hidden items-center gap-1.5 text-sm font-semibold text-accent-strong sm:flex">
            Xem catalog
            <ArrowRight size={16} />
          </Link>
        </div>
        <div className="mt-7">
          {books.isLoading ? (
            <LoadingGrid />
          ) : books.isError ? (
            <ErrorState message="Không thể tải sách nổi bật." retry={() => void books.refetch()} />
          ) : (
            <div className="book-grid">
              {books.data?.items.map((book) => (
                <BookCard key={book.id} book={book} />
              ))}
            </div>
          )}
        </div>
      </section>

      <section className="mt-16 border-t border-border pt-12">
        <h2 className="text-2xl font-bold tracking-tight text-heading">Đi theo chủ đề bạn quan tâm</h2>
        {categories.isLoading ? (
          <div className="mt-6 flex flex-wrap gap-2">
            {Array.from({ length: 8 }, (_, index) => (
              <div key={index} className="h-10 w-28 animate-pulse rounded-full bg-surface-muted" />
            ))}
          </div>
        ) : categories.isError ? (
          <ErrorState message="Chưa thể tải chủ đề." retry={() => void categories.refetch()} />
        ) : (
          <div className="mt-6 flex flex-wrap gap-2">
            {categories.data?.items.map((category) => (
              <Link
                key={category.id}
                to={`/books?categoryId=${category.id}`}
                className="rounded-full border border-border bg-surface px-4 py-2 text-sm font-medium text-body transition-colors hover:border-accent hover:text-accent-strong"
              >
                {category.name}
                {typeof category.bookCount === 'number' ? ` · ${category.bookCount}` : ''}
              </Link>
            ))}
          </div>
        )}
      </section>

      <section className="mt-16 grid gap-10 border-t border-border pt-12 lg:grid-cols-2">
        <div>
          <div className="flex items-center gap-3">
            <UsersThree size={24} weight="duotone" className="text-accent-strong" />
            <h2 className="text-2xl font-bold tracking-tight text-heading">Câu lạc bộ mở</h2>
          </div>
          <div className="mt-6">
            {clubs.isLoading ? (
              <LoadingRows count={3} />
            ) : clubs.isError ? (
              <ErrorState message="Không thể tải câu lạc bộ." retry={() => void clubs.refetch()} />
            ) : (
              <div className="space-y-3">
                {clubs.data?.items.slice(0, 3).map((club) => (
                  <Link key={club.id} to={`/clubs/${club.id}`} className="surface flex gap-4 p-4 hover:border-accent/50">
                    <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                      <UsersThree size={21} weight="duotone" />
                    </div>
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-heading">{club.name}</p>
                      <p className="mt-1 line-clamp-1 text-sm text-muted">{club.description}</p>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>
        </div>
        <div>
          <div className="flex items-center gap-3">
            <Books size={24} weight="duotone" className="text-accent-strong" />
            <h2 className="text-2xl font-bold tracking-tight text-heading">Thử thách đang mở</h2>
          </div>
          <div className="mt-6">
            {challenges.isLoading ? (
              <LoadingRows count={3} />
            ) : challenges.isError ? (
              <ErrorState message="Không thể tải thử thách." retry={() => void challenges.refetch()} />
            ) : (
              <div className="space-y-3">
                {challenges.data?.items.slice(0, 3).map((challenge) => (
                  <Link key={challenge.id} to={`/challenges/${challenge.id}`} className="surface flex gap-4 p-4 hover:border-accent/50">
                    <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                      <Books size={21} weight="duotone" />
                    </div>
                    <div>
                      <p className="font-semibold text-heading">{challenge.title}</p>
                      <p className="mt-1 text-sm text-muted">
                        {challenge.goalBooks} cuốn · {challenge.participantCount} người tham gia
                      </p>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  )
}
