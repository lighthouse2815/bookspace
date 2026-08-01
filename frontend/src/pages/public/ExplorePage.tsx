import {
  ArrowRight,
  BookmarkSimple,
  Books,
  MagnifyingGlass,
  Sparkle,
  UsersThree,
} from '@phosphor-icons/react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { BookCard } from '../../components/books/BookCard'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingGrid, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useBookRecommendations, useBooks, useCategories } from '../../hooks/useCatalog'
import { useAddToLibrary } from '../../hooks/useReading'
import { useChallenges, useClubs } from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import type { BookRecommendation } from '../../types/domain'

const RECOMMENDATION_PAGE_SIZE = 12

const recommendationFallbacks: Record<BookRecommendation['reasonCode'], string> = {
  FOLLOWED_READER_LIKED: 'Được một độc giả bạn theo dõi yêu thích.',
  MATCHED_AUTHOR: 'Cùng tác giả với những cuốn sách bạn quan tâm.',
  MATCHED_CATEGORY: 'Hợp với chủ đề bạn thường đọc.',
  POPULAR_FALLBACK: 'Đang được cộng đồng BookSpace quan tâm.',
}

function recommendationReason(recommendation: BookRecommendation) {
  return recommendation.reasonText.trim() || recommendationFallbacks[recommendation.reasonCode]
}

export function ExplorePage() {
  const [search, setSearch] = useState('')
  const [recommendationPage, setRecommendationPage] = useState(1)
  const [pendingBookId, setPendingBookId] = useState<string | null>(null)
  const pendingBookIdRef = useRef<string | null>(null)
  const navigate = useNavigate()
  const { isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const books = useBooks({ sort: 'popular', page: 1, pageSize: 8 })
  const recommendations = useBookRecommendations({
    page: recommendationPage,
    pageSize: RECOMMENDATION_PAGE_SIZE,
  })
  const addToLibrary = useAddToLibrary()
  const categories = useCategories()
  const clubs = useClubs()
  const challenges = useChallenges()

  useEffect(() => {
    const result = recommendations.data
    if (!result || result.totalItems === 0 || result.totalPages === 0) return
    if (recommendationPage > result.totalPages) setRecommendationPage(result.totalPages)
  }, [recommendationPage, recommendations.data])

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const query = search.trim()
    navigate(query ? `/books?search=${encodeURIComponent(query)}` : '/books')
  }

  const saveRecommendation = async (recommendation: BookRecommendation) => {
    if (pendingBookIdRef.current) return
    pendingBookIdRef.current = recommendation.book.id
    setPendingBookId(recommendation.book.id)
    try {
      await addToLibrary.mutateAsync({
        bookId: recommendation.book.id,
        shelf: 'WANT_TO_READ',
      })
      showToast(`Đã thêm “${recommendation.book.title}” vào kệ Muốn đọc`, 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể thêm sách vào thư viện'), 'error')
    } finally {
      pendingBookIdRef.current = null
      setPendingBookId(null)
    }
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

      {isAuthenticated ? (
        <section className="mt-14" aria-labelledby="recommendations-title">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <div className="flex items-center gap-2 text-accent-strong">
                <Sparkle size={22} weight="fill" aria-hidden />
                <h2
                  id="recommendations-title"
                  className="text-2xl font-bold tracking-tight text-heading"
                >
                  Dành cho bạn
                </h2>
              </div>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
                Gợi ý từ gu đọc của bạn và những đánh giá công khai trong cộng đồng.
              </p>
            </div>
            {recommendations.data?.totalItems ? (
              <p className="text-sm font-medium text-muted" aria-live="polite">
                {recommendations.data.totalItems} cuốn sách phù hợp
              </p>
            ) : null}
          </div>

          <div className="mt-7">
            {recommendations.isLoading ? (
              <LoadingGrid count={RECOMMENDATION_PAGE_SIZE} />
            ) : null}
            {recommendations.isError ? (
              <ErrorState
                message={errorMessage(
                  recommendations.error,
                  'Không thể tải gợi ý sách. Vui lòng thử lại.',
                )}
                retry={() => void recommendations.refetch()}
              />
            ) : null}
            {recommendations.data?.items.length ? (
              <>
                <div className="book-grid">
                  {recommendations.data.items.map((recommendation) => (
                    <div key={recommendation.book.id} className="flex min-w-0 flex-col">
                      <BookCard book={recommendation.book} />
                      <p className="mt-3 flex min-h-10 items-start gap-2 text-xs leading-5 text-muted">
                        <Sparkle
                          size={15}
                          weight="duotone"
                          className="mt-0.5 shrink-0 text-accent-strong"
                          aria-hidden
                        />
                        <span>{recommendationReason(recommendation)}</span>
                      </p>
                      <Button
                        type="button"
                        variant="secondary"
                        size="sm"
                        className="mt-3 w-full"
                        icon={<BookmarkSimple size={16} aria-hidden />}
                        loading={pendingBookId === recommendation.book.id}
                        disabled={pendingBookId !== null}
                        onClick={() => void saveRecommendation(recommendation)}
                        aria-label={`Thêm ${recommendation.book.title} vào kệ Muốn đọc`}
                      >
                        Thêm vào Muốn đọc
                      </Button>
                    </div>
                  ))}
                </div>
                <Pagination
                  page={recommendations.data.page}
                  totalPages={recommendations.data.totalPages}
                  onPageChange={setRecommendationPage}
                  disabled={recommendations.isFetching || pendingBookId !== null}
                  className="mt-9"
                />
              </>
            ) : null}
            {recommendations.data && recommendations.data.totalItems === 0 ? (
              <EmptyState
                title="Chưa có gợi ý mới"
                description="Bạn đã lưu các gợi ý hiện có. Hãy khám phá catalog hoặc ghi thêm hoạt động đọc để BookSpace hiểu gu của bạn hơn."
                icon={Sparkle}
                action={
                  <Link to="/books" className="button button-secondary button-sm">
                    Xem toàn bộ catalog
                  </Link>
                }
              />
            ) : null}
          </div>
        </section>
      ) : null}

      <section className="mt-14">
        <div className="flex items-end justify-between gap-4">
          <div>
            <h2 className="text-2xl font-bold tracking-tight text-heading">Được đọc nhiều</h2>
            <p className="mt-2 text-sm text-muted">
              Những tựa sách đang tạo nên nhiều cuộc trò chuyện.
            </p>
          </div>
          <Link
            to="/books"
            className="hidden items-center gap-1.5 text-sm font-semibold text-accent-strong sm:flex"
          >
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
        <h2 className="text-2xl font-bold tracking-tight text-heading">
          Đi theo chủ đề bạn quan tâm
        </h2>
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
                  <Link
                    key={club.id}
                    to={`/clubs/${club.id}`}
                    className="surface flex gap-4 p-4 hover:border-accent/50"
                  >
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
                  <Link
                    key={challenge.id}
                    to={`/challenges/${challenge.id}`}
                    className="surface flex gap-4 p-4 hover:border-accent/50"
                  >
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
