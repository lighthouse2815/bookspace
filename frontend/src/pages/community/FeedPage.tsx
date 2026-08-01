import {
  ArrowClockwise,
  BookOpenText,
  Flag,
  Sparkle,
  UserPlus,
  UsersThree,
} from '@phosphor-icons/react'
import { useEffect } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ActivityCard } from '../../components/community/ActivityCard'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useFeed, useFollowUser, usePeopleSuggestions } from '../../hooks/useCommunity'
import { errorMessage } from '../../lib/api'
import type { FeedFilter, UserDiscoveryItem } from '../../types/domain'

const PAGE_SIZE = 10
const SUGGESTION_PAGE_SIZE = 3

type FeedView = 'all' | 'review' | 'reading' | 'club' | 'challenge'

const feedFilters: Array<{
  value: FeedView
  label: string
  emptyLabel: string
  type?: FeedFilter
}> = [
  { value: 'all', label: 'Tất cả', emptyLabel: 'phù hợp' },
  { value: 'review', label: 'Đánh giá', emptyLabel: 'đánh giá', type: 'REVIEW' },
  { value: 'reading', label: 'Tiến độ', emptyLabel: 'đọc sách', type: 'READING' },
  { value: 'club', label: 'Câu lạc bộ', emptyLabel: 'câu lạc bộ', type: 'CLUB' },
  { value: 'challenge', label: 'Thử thách', emptyLabel: 'thử thách', type: 'CHALLENGE' },
]

function parseFeedView(value: string | null): FeedView {
  return feedFilters.some((option) => option.value === value) ? (value as FeedView) : 'all'
}

function parsePositivePage(value: string | null) {
  const page = Number(value)
  return Number.isInteger(page) && page > 0 ? page : 1
}

function feedSearchParams(view: FeedView, page = 1) {
  const params = new URLSearchParams()
  if (view !== 'all') params.set('type', view)
  if (page > 1) params.set('page', String(page))
  return params
}

function SuggestedReader({ person }: { person: UserDiscoveryItem }) {
  const { showToast } = useToast()
  const follow = useFollowUser(person.id, person.isFollowing)

  const toggleFollow = () => {
    follow.mutate(undefined, {
      onSuccess: () =>
        showToast(
          person.isFollowing ? 'Đã bỏ theo dõi' : 'Đã theo dõi người đọc này',
          'success',
        ),
      onError: (error) => showToast(errorMessage(error), 'error'),
    })
  }

  return (
    <article className="flex items-start gap-3 py-3 first:pt-0 last:pb-0">
      <Link
        to={`/users/${person.id}`}
        aria-label={`Xem hồ sơ ${person.displayName}`}
        className="shrink-0"
      >
        <Avatar src={person.avatarUrl} name={person.displayName} size="sm" />
      </Link>
      <div className="min-w-0 flex-1">
        <Link
          to={`/users/${person.id}`}
          className="block truncate text-sm font-semibold text-heading hover:text-accent-strong"
        >
          {person.displayName}
        </Link>
        <p className="mt-0.5 line-clamp-2 break-words text-xs leading-5 text-muted">
          {person.reasonText}
        </p>
        <Button
          type="button"
          variant={person.isFollowing ? 'secondary' : 'ghost'}
          size="sm"
          loading={follow.isPending}
          disabled={follow.isPending}
          aria-label={`${person.isFollowing ? 'Bỏ theo dõi' : 'Theo dõi'} ${person.displayName}`}
          icon={<UserPlus size={15} aria-hidden />}
          className="mt-2"
          onClick={toggleFollow}
        >
          {person.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
        </Button>
      </div>
    </article>
  )
}

function SuggestedReaders() {
  const suggestions = usePeopleSuggestions(1, SUGGESTION_PAGE_SIZE)

  return (
    <section className="surface p-5" aria-labelledby="feed-suggestions-title">
      <div className="flex items-center gap-2">
        <Sparkle size={20} weight="duotone" className="text-accent-strong" aria-hidden />
        <h2 id="feed-suggestions-title" className="font-semibold text-heading">
          Độc giả nên theo dõi
        </h2>
      </div>

      {suggestions.isLoading || suggestions.isPending ? (
        <div className="mt-4 space-y-4" aria-label="Đang tải gợi ý độc giả" aria-busy="true">
          {Array.from({ length: 2 }, (_, index) => (
            <div key={index} className="flex animate-pulse gap-3">
              <div className="h-8 w-8 shrink-0 rounded-full bg-surface-muted" />
              <div className="flex-1 space-y-2">
                <div className="h-3 w-2/3 rounded bg-surface-muted" />
                <div className="h-3 w-full rounded bg-surface-muted" />
              </div>
            </div>
          ))}
        </div>
      ) : suggestions.isError ? (
        <div className="mt-4 text-sm text-muted" role="alert">
          <p>Không thể tải gợi ý độc giả.</p>
          <button
            type="button"
            className="mt-2 font-semibold text-accent-strong hover:underline"
            onClick={() => void suggestions.refetch()}
          >
            Thử lại
          </button>
        </div>
      ) : suggestions.data?.items.length ? (
        <div className="mt-4 divide-y divide-border">
          {suggestions.data.items.map((person) => (
            <SuggestedReader key={person.id} person={person} />
          ))}
        </div>
      ) : (
        <p className="mt-4 text-sm leading-6 text-muted">
          Chưa có gợi ý mới. Bạn có thể{' '}
          <Link to="/people" className="font-semibold text-accent-strong hover:underline">
            khám phá thêm độc giả
          </Link>
          .
        </p>
      )}
    </section>
  )
}

export function FeedPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const rawView = searchParams.get('type')
  const rawPage = searchParams.get('page')
  const view = parseFeedView(rawView)
  const page = parsePositivePage(rawPage)
  const selectedFilter = feedFilters.find((option) => option.value === view) ?? feedFilters[0]
  const feed = useFeed({ type: selectedFilter.type, page, pageSize: PAGE_SIZE })

  useEffect(() => {
    const canonicalPage = feed.data
      ? Math.min(page, Math.max(1, feed.data.totalPages))
      : page
    const expectedView = view === 'all' ? null : view
    const expectedPage = canonicalPage > 1 ? String(canonicalPage) : null

    if (rawView === expectedView && rawPage === expectedPage) return
    setSearchParams(feedSearchParams(view, canonicalPage), { replace: true })
  }, [feed.data, page, rawPage, rawView, setSearchParams, view])

  const changeView = (nextView: FeedView) => {
    setSearchParams(feedSearchParams(nextView))
  }

  const changePage = (nextPage: number) => {
    setSearchParams(feedSearchParams(view, nextPage))
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const isInitialLoading = feed.isLoading || feed.isPending

  return (
    <div className="container-page section-space">
      <header className="max-w-2xl">
        <p className="eyebrow">Cộng đồng</p>
        <h1 className="page-title mt-4">Những gì người đọc đang nghĩ tới.</h1>
        <p className="mt-3 leading-7 text-muted">
          Bài đánh giá, tiến độ và cột mốc mới từ những người bạn theo dõi.
        </p>
      </header>

      <section className="mt-8 rounded-2xl border border-border bg-surface p-2 sm:p-3">
        <div className="flex gap-1 overflow-x-auto" aria-label="Lọc hoạt động bảng tin">
          {feedFilters.map((option) => (
            <button
              key={option.value}
              type="button"
              className={`filter-tab whitespace-nowrap ${view === option.value ? 'filter-tab-active' : ''}`}
              aria-current={view === option.value ? 'page' : undefined}
              onClick={() => changeView(option.value)}
            >
              {option.label}
            </button>
          ))}
        </div>
      </section>

      <div className="mt-8 grid gap-8 lg:grid-cols-[minmax(0,1fr)_18rem]">
        <section aria-label="Hoạt động từ cộng đồng">
          <div className="mb-4 flex min-h-9 flex-wrap items-center justify-between gap-3 text-sm text-muted">
            <span aria-live="polite">
              {feed.data ? `${feed.data.totalItems} hoạt động` : 'Bảng tin của bạn'}
              {feed.isFetching && !isInitialLoading ? ' · Đang làm mới…' : ''}
            </span>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              icon={<ArrowClockwise size={16} aria-hidden />}
              loading={feed.isFetching && !isInitialLoading}
              disabled={isInitialLoading}
              aria-label="Làm mới bảng tin"
              onClick={() => void feed.refetch()}
            >
              Làm mới
            </Button>
          </div>

          {isInitialLoading ? (
            <LoadingRows count={5} />
          ) : feed.isError ? (
            <ErrorState
              message={errorMessage(feed.error, 'Không thể tải bảng tin. Vui lòng thử lại.')}
              retry={() => void feed.refetch()}
            />
          ) : feed.data?.items.length ? (
            <>
              <div className="space-y-4">
                {feed.data.items.map((item) => (
                  <ActivityCard key={`${item.type}-${item.id}`} item={item} />
                ))}
              </div>
              <Pagination
                page={page}
                totalPages={feed.data.totalPages}
                disabled={feed.isFetching}
                onPageChange={changePage}
                className="mt-8"
              />
            </>
          ) : (
            <EmptyState
              icon={UsersThree}
              title={
                view === 'all'
                  ? 'Bảng tin của bạn còn yên ắng'
                  : `Chưa có hoạt động ${selectedFilter.emptyLabel}`
              }
              description={
                view === 'all'
                  ? 'Theo dõi những người đọc thú vị để thấy bài đánh giá và hành trình mới của họ.'
                  : 'Thử một bộ lọc khác hoặc theo dõi thêm độc giả để làm phong phú bảng tin.'
              }
              action={
                <Link to="/people" className="button button-primary button-md">
                  Khám phá độc giả
                </Link>
              }
            />
          )}
        </section>

        <aside className="space-y-4">
          <SuggestedReaders />
          <div className="surface p-5">
            <BookOpenText size={23} weight="duotone" className="text-accent-strong" />
            <h2 className="mt-4 font-semibold text-heading">Viết từ trải nghiệm thật</h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Đánh giá xuất hiện từ trang chi tiết sách để cuộc trò chuyện luôn có ngữ cảnh.
            </p>
            <Link
              to="/books"
              className="mt-4 inline-block text-sm font-semibold text-accent-strong hover:underline"
            >
              Chọn một cuốn sách
            </Link>
          </div>
          <div className="surface p-5">
            <Flag size={23} weight="duotone" className="text-accent-strong" />
            <h2 className="mt-4 font-semibold text-heading">Tôn trọng người đọc khác</h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Tránh tiết lộ nội dung quan trọng và tập trung vào góc nhìn của riêng bạn.
            </p>
          </div>
        </aside>
      </div>
    </div>
  )
}
