import {
  BookOpenText,
  Books,
  CalendarBlank,
  ClockCounterClockwise,
  GearSix,
  LockKey,
  Star,
  UserCircle,
  UserMinus,
  UserPlus,
  Users,
} from '@phosphor-icons/react'
import { useCallback, useState } from 'react'
import { Link, Navigate, useLocation, useParams, useSearchParams } from 'react-router-dom'
import { ActivityCard } from '../../components/community/ActivityCard'
import { ProfileConnectionsDialog } from '../../components/community/ProfileConnectionsDialog'
import { ReviewCard } from '../../components/community/ReviewCard'
import { BookCard } from '../../components/books/BookCard'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingGrid, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useFollowUser,
  useUser,
  useUserActivity,
  useUserLibrary,
  useUserReviews,
} from '../../hooks/useCommunity'
import { errorMessage, isNotFoundError } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { Shelf } from '../../types/domain'

type ProfileTab = 'overview' | 'books' | 'reviews' | 'activity'
type ConnectionKind = 'followers' | 'following'

const tabs: Array<{ id: ProfileTab; label: string; icon: typeof Books }> = [
  { id: 'overview', label: 'Tổng quan', icon: UserCircle },
  { id: 'books', label: 'Kệ sách', icon: Books },
  { id: 'reviews', label: 'Đánh giá', icon: Star },
  { id: 'activity', label: 'Hoạt động', icon: ClockCounterClockwise },
]

const shelfFilters: Array<{ value?: Shelf; label: string }> = [
  { label: 'Tất cả' },
  { value: 'READING', label: 'Đang đọc' },
  { value: 'READ', label: 'Đã đọc' },
  { value: 'WANT_TO_READ', label: 'Muốn đọc' },
]

export function CurrentProfileRedirect() {
  const { user } = useAuth()
  return user ? <Navigate to={`/users/${user.id}`} replace /> : <Navigate to="/login" replace />
}

export function ProfilePage() {
  const { id } = useParams()
  const { user: currentUser, isAuthenticated, isLoading: isAuthLoading } = useAuth()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const [connections, setConnections] = useState<ConnectionKind | null>(null)
  const profile = useUser(id)
  const { showToast } = useToast()
  const ownProfile = currentUser?.id === id
  const follow = useFollowUser(id ?? '', Boolean(profile.data?.isFollowing))

  const requestedTab = searchParams.get('tab')
  const activeTab: ProfileTab = tabs.some((tab) => tab.id === requestedTab)
    ? (requestedTab as ProfileTab)
    : 'overview'
  const requestedPage = Number(searchParams.get('page') ?? '1')
  const page = Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1
  const requestedShelf = searchParams.get('shelf')
  const shelf = shelfFilters.some((item) => item.value === requestedShelf)
    ? (requestedShelf as Shelf)
    : undefined
  const canSeeLibrary = ownProfile || Boolean(profile.data?.privacy?.isReadingShelfPublic)
  const canSeeActivity = ownProfile || Boolean(profile.data?.privacy?.isReadingActivityPublic)

  const library = useUserLibrary(
    id,
    activeTab === 'books' ? shelf : undefined,
    activeTab === 'books' ? page : 1,
    activeTab === 'books' ? 12 : 4,
    Boolean(profile.data) && canSeeLibrary && (activeTab === 'overview' || activeTab === 'books'),
  )
  const reviews = useUserReviews(
    id,
    activeTab === 'reviews' ? page : 1,
    activeTab === 'reviews' ? 10 : 2,
    Boolean(profile.data) && (activeTab === 'overview' || activeTab === 'reviews'),
  )
  const activity = useUserActivity(
    id,
    activeTab === 'activity' ? page : 1,
    activeTab === 'activity' ? 10 : 3,
    Boolean(profile.data) && canSeeActivity && (activeTab === 'overview' || activeTab === 'activity'),
  )

  const closeConnections = useCallback(() => setConnections(null), [])

  const changeView = (next: { tab: ProfileTab; page?: number; shelf?: Shelf }) => {
    const params = new URLSearchParams()
    if (next.tab !== 'overview') params.set('tab', next.tab)
    if (next.shelf) params.set('shelf', next.shelf)
    if ((next.page ?? 1) > 1) params.set('page', String(next.page))
    setSearchParams(params)
  }

  if (isAuthLoading || profile.isPending) {
    return (
      <div className="container-page section-space">
        <div className="h-48 animate-pulse rounded-2xl bg-surface-muted" />
      </div>
    )
  }

  if (profile.isError && isNotFoundError(profile.error)) {
    return (
      <div className="container-page section-space">
        <EmptyState
          title="Không tìm thấy hồ sơ"
          description="Độc giả này không còn hoạt động hoặc đường dẫn không chính xác."
          icon={UserCircle}
          action={
            <Link to="/people" className="button button-secondary button-sm">
              Xem danh sách độc giả
            </Link>
          }
        />
      </div>
    )
  }

  if (profile.isError || !profile.data) {
    return (
      <div className="container-page section-space">
        <ErrorState message="Không thể tải hồ sơ người đọc." retry={() => void profile.refetch()} />
      </div>
    )
  }

  const person = profile.data

  return (
    <div className="container-page section-space">
      <section className="surface overflow-hidden">
        <div className="relative h-36 overflow-hidden bg-[linear-gradient(135deg,var(--surface-muted),var(--accent-soft))]">
          <div className="absolute -right-12 -top-20 h-56 w-56 rounded-full border border-accent/15" />
          <div className="absolute right-20 top-8 h-28 w-28 rounded-full border border-accent/10" />
        </div>
        <div className="px-5 pb-7 sm:px-8">
          <div className="-mt-12 flex flex-col gap-5 sm:flex-row sm:items-end">
            <div className="rounded-full border-4 border-surface bg-surface">
              <Avatar src={person.avatarUrl} name={person.displayName} size="xl" />
            </div>
            <div className="min-w-0 flex-1 sm:pb-1">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="break-words text-2xl font-bold tracking-tight text-heading">
                  {person.displayName}
                </h1>
                {person.followsYou ? (
                  <span className="rounded-full bg-accent-soft px-2.5 py-1 text-xs font-semibold text-accent-strong">
                    Đang theo dõi bạn
                  </span>
                ) : null}
              </div>
              <p className="mt-1 text-sm text-muted">Hồ sơ đọc sách công khai</p>
            </div>
            {ownProfile ? (
              <Link to="/settings" className="button button-secondary button-md">
                <GearSix size={18} /> Cài đặt hồ sơ
              </Link>
            ) : isAuthenticated ? (
              <Button
                variant={person.isFollowing ? 'secondary' : 'primary'}
                loading={follow.isPending}
                disabled={follow.isPending}
                aria-label={`${person.isFollowing ? 'Bỏ theo dõi' : 'Theo dõi'} ${person.displayName}`}
                icon={person.isFollowing ? <UserMinus size={18} /> : <UserPlus size={18} />}
                onClick={() =>
                  follow.mutate(undefined, {
                    onSuccess: () =>
                      showToast(
                        person.isFollowing ? 'Đã bỏ theo dõi' : 'Đã theo dõi người đọc này',
                        'success',
                      ),
                    onError: (error) => showToast(errorMessage(error), 'error'),
                  })
                }
              >
                {person.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
              </Button>
            ) : (
              <Link
                to="/login"
                state={{ from: `${location.pathname}${location.search}` }}
                aria-label={`Đăng nhập để theo dõi ${person.displayName}`}
                className="button button-primary button-md"
              >
                Đăng nhập để theo dõi
              </Link>
            )}
          </div>

          {person.bio ? (
            <p className="mt-6 max-w-2xl whitespace-pre-line break-words text-sm leading-6 text-body">
              {person.bio}
            </p>
          ) : ownProfile ? (
            <p className="mt-6 text-sm text-muted">Bạn có thể thêm giới thiệu trong phần cài đặt.</p>
          ) : null}

          <div className="mt-6 flex flex-wrap items-center gap-x-6 gap-y-3 text-sm">
            <button
              type="button"
              className="inline-flex items-center gap-2 text-muted hover:text-heading"
              onClick={() => setConnections('followers')}
            >
              <Users size={17} />
              <strong className="text-heading">{person.followerCount ?? 0}</strong> người theo dõi
            </button>
            <button
              type="button"
              className="text-muted hover:text-heading"
              onClick={() => setConnections('following')}
            >
              <strong className="text-heading">{person.followingCount ?? 0}</strong> đang theo dõi
            </button>
            {isAuthenticated && !ownProfile && (person.mutualFollowCount ?? 0) > 0 ? (
              <span className="text-muted">
                <strong className="text-heading">{person.mutualFollowCount}</strong> kết nối chung
              </span>
            ) : null}
            <span className="inline-flex items-center gap-2 text-muted">
              <CalendarBlank size={17} />
              Tham gia {person.joinedAt ? formatDate(person.joinedAt) : 'BookSpace'}
            </span>
          </div>
        </div>
      </section>

      <nav className="mt-6 flex gap-1 overflow-x-auto rounded-2xl border border-border bg-surface p-1.5" aria-label="Nội dung hồ sơ">
        {tabs.map((tab) => {
          const Icon = tab.icon
          const active = activeTab === tab.id
          return (
            <button
              key={tab.id}
              type="button"
              className={`filter-tab ${active ? 'filter-tab-active' : ''}`}
              aria-current={active ? 'page' : undefined}
              onClick={() => changeView({ tab: tab.id })}
            >
              <Icon size={17} /> {tab.label}
            </button>
          )
        })}
      </nav>

      <main className="mt-8">
        {activeTab === 'overview' ? (
          <ProfileOverview
            booksRead={person.booksReadCount ?? 0}
            canSeeLibrary={canSeeLibrary}
            canSeeActivity={canSeeActivity}
            library={library}
            reviews={reviews}
            activity={activity}
            onOpenTab={(tab) => changeView({ tab })}
          />
        ) : null}

        {activeTab === 'books' ? (
          canSeeLibrary ? (
            <section>
              <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
                <div>
                  <p className="eyebrow">Thư viện công khai</p>
                  <h2 className="mt-3 text-2xl font-bold text-heading">Những cuốn sách tạo nên hành trình</h2>
                </div>
                <div className="flex gap-1 overflow-x-auto" aria-label="Lọc kệ sách">
                  {shelfFilters.map((filter) => (
                    <button
                      key={filter.value ?? 'ALL'}
                      type="button"
                      className={`filter-tab ${shelf === filter.value ? 'filter-tab-active' : ''}`}
                      onClick={() => changeView({ tab: 'books', shelf: filter.value })}
                    >
                      {filter.label}
                    </button>
                  ))}
                </div>
              </div>
              <div className="mt-7">
                {library.isLoading ? (
                  <LoadingGrid count={8} />
                ) : library.isError ? (
                  <ErrorState message="Không thể tải kệ sách công khai." retry={() => void library.refetch()} />
                ) : library.data?.items.length ? (
                  <>
                    <div className="book-grid">
                      {library.data.items.map((entry) => (
                        <div key={entry.bookId}>
                          <BookCard book={entry.book} />
                          <div className="mt-3 flex items-center justify-between gap-3 text-xs text-muted">
                            <span>{shelfLabel(entry.shelf)}</span>
                            {entry.shelf === 'READING' ? (
                              <span className="font-semibold text-accent-strong">
                                {entry.progressPercent}%
                              </span>
                            ) : null}
                          </div>
                        </div>
                      ))}
                    </div>
                    <Pagination
                      page={library.data.page}
                      totalPages={library.data.totalPages}
                      disabled={library.isFetching}
                      onPageChange={(nextPage) => changeView({ tab: 'books', page: nextPage, shelf })}
                      className="mt-8"
                    />
                  </>
                ) : (
                  <EmptyState
                    title="Kệ sách này chưa có nội dung"
                    description="Các cuốn sách phù hợp với bộ lọc sẽ xuất hiện tại đây."
                    icon={Books}
                  />
                )}
              </div>
            </section>
          ) : (
            <PrivateSection title="Kệ sách đang riêng tư" />
          )
        ) : null}

        {activeTab === 'reviews' ? (
          <section className="mx-auto max-w-3xl">
            <div>
              <p className="eyebrow">Góc nhìn công khai</p>
              <h2 className="mt-3 text-2xl font-bold text-heading">Đánh giá gần đây</h2>
            </div>
            <div className="mt-7">
              {reviews.isLoading ? (
                <LoadingRows count={5} />
              ) : reviews.isError ? (
                <ErrorState message="Không thể tải đánh giá của độc giả." retry={() => void reviews.refetch()} />
              ) : reviews.data?.items.length ? (
                <>
                  <div className="space-y-4">
                    {reviews.data.items.map((review) => (
                      <ReviewCard key={review.id} review={review} bookId={review.bookId} />
                    ))}
                  </div>
                  <Pagination
                    page={reviews.data.page}
                    totalPages={reviews.data.totalPages}
                    disabled={reviews.isFetching}
                    onPageChange={(nextPage) => changeView({ tab: 'reviews', page: nextPage })}
                    className="mt-8"
                  />
                </>
              ) : (
                <EmptyState
                  title="Chưa có đánh giá công khai"
                  description="Khi độc giả viết cảm nhận về sách, nội dung sẽ xuất hiện tại đây."
                  icon={Star}
                />
              )}
            </div>
          </section>
        ) : null}

        {activeTab === 'activity' ? (
          canSeeActivity ? (
            <section className="mx-auto max-w-3xl">
              <div>
                <p className="eyebrow">Dòng thời gian</p>
                <h2 className="mt-3 text-2xl font-bold text-heading">Nhịp đọc gần đây</h2>
              </div>
              <div className="mt-7">
                {activity.isLoading ? (
                  <LoadingRows count={5} />
                ) : activity.isError ? (
                  <ErrorState message="Không thể tải dòng hoạt động." retry={() => void activity.refetch()} />
                ) : activity.data?.items.length ? (
                  <>
                    <div className="space-y-4">
                      {activity.data.items.map((item) => (
                        <ActivityCard key={`${item.type}-${item.id}`} item={item} />
                      ))}
                    </div>
                    <Pagination
                      page={activity.data.page}
                      totalPages={activity.data.totalPages}
                      disabled={activity.isFetching}
                      onPageChange={(nextPage) => changeView({ tab: 'activity', page: nextPage })}
                      className="mt-8"
                    />
                  </>
                ) : (
                  <EmptyState
                    title="Chưa có hoạt động công khai"
                    description="Các cột mốc đọc sách, đánh giá và thử thách sẽ xuất hiện tại đây."
                    icon={ClockCounterClockwise}
                  />
                )}
              </div>
            </section>
          ) : (
            <PrivateSection title="Dòng hoạt động đang riêng tư" />
          )
        ) : null}
      </main>

      {id && connections ? (
        <ProfileConnectionsDialog
          userId={id}
          kind={connections}
          open
          onClose={closeConnections}
        />
      ) : null}
    </div>
  )
}

function ProfileOverview({
  booksRead,
  canSeeLibrary,
  canSeeActivity,
  library,
  reviews,
  activity,
  onOpenTab,
}: {
  booksRead: number
  canSeeLibrary: boolean
  canSeeActivity: boolean
  library: ReturnType<typeof useUserLibrary>
  reviews: ReturnType<typeof useUserReviews>
  activity: ReturnType<typeof useUserActivity>
  onOpenTab: (tab: ProfileTab) => void
}) {
  return (
    <div className="space-y-10">
      <section className="grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-3">
        <div className="bg-surface p-6">
          <BookOpenText size={23} weight="duotone" className="text-accent-strong" />
          <p className="mt-4 text-3xl font-bold text-heading">{booksRead}</p>
          <p className="mt-1 text-sm text-muted">cuốn đã đọc</p>
        </div>
        <div className="bg-surface p-6 sm:col-span-2">
          <h2 className="font-semibold text-heading">Dấu vân tay đọc sách</h2>
          <p className="mt-2 max-w-xl text-sm leading-6 text-muted">
            Kệ sách, đánh giá và các cột mốc công khai giúp bạn hiểu gu đọc trước khi kết nối.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            <button type="button" className="button button-secondary button-sm" onClick={() => onOpenTab('books')}>
              Xem kệ sách
            </button>
            <button type="button" className="button button-secondary button-sm" onClick={() => onOpenTab('reviews')}>
              Đọc đánh giá
            </button>
          </div>
        </div>
      </section>

      <section>
        <div className="flex items-end justify-between gap-4">
          <div>
            <p className="eyebrow">Trên kệ</p>
            <h2 className="mt-3 text-xl font-bold text-heading">Sách gần đây</h2>
          </div>
          {canSeeLibrary ? (
            <button type="button" className="text-sm font-semibold text-accent-strong hover:underline" onClick={() => onOpenTab('books')}>
              Xem tất cả
            </button>
          ) : null}
        </div>
        <div className="mt-5">
          {!canSeeLibrary ? (
            <PrivatePreview label="Kệ sách" />
          ) : library.isLoading ? (
            <LoadingGrid count={4} />
          ) : library.data?.items.length ? (
            <div className="book-grid">
              {library.data.items.map((entry) => <BookCard key={entry.bookId} book={entry.book} />)}
            </div>
          ) : (
            <EmptyState title="Kệ sách còn trống" description="Chưa có sách công khai để hiển thị." />
          )}
        </div>
      </section>

      <section className="grid gap-8 lg:grid-cols-2">
        <div>
          <div className="flex items-end justify-between gap-4">
            <div>
              <p className="eyebrow">Cảm nhận</p>
              <h2 className="mt-3 text-xl font-bold text-heading">Đánh giá mới</h2>
            </div>
            <button type="button" className="text-sm font-semibold text-accent-strong hover:underline" onClick={() => onOpenTab('reviews')}>
              Xem tất cả
            </button>
          </div>
          <div className="mt-5 space-y-4">
            {reviews.isLoading ? (
              <LoadingRows count={2} />
            ) : reviews.data?.items.length ? (
              reviews.data.items.map((review) => (
                <ReviewCard key={review.id} review={review} bookId={review.bookId} />
              ))
            ) : (
              <EmptyState title="Chưa có đánh giá" description="Những cảm nhận công khai sẽ xuất hiện tại đây." icon={Star} />
            )}
          </div>
        </div>

        <div>
          <div className="flex items-end justify-between gap-4">
            <div>
              <p className="eyebrow">Gần đây</p>
              <h2 className="mt-3 text-xl font-bold text-heading">Hoạt động</h2>
            </div>
            {canSeeActivity ? (
              <button type="button" className="text-sm font-semibold text-accent-strong hover:underline" onClick={() => onOpenTab('activity')}>
                Xem tất cả
              </button>
            ) : null}
          </div>
          <div className="mt-5 space-y-4">
            {!canSeeActivity ? (
              <PrivatePreview label="Dòng hoạt động" />
            ) : activity.isLoading ? (
              <LoadingRows count={2} />
            ) : activity.data?.items.length ? (
              activity.data.items.map((item) => (
                <ActivityCard key={`${item.type}-${item.id}`} item={item} />
              ))
            ) : (
              <EmptyState title="Chưa có hoạt động" description="Các cột mốc công khai sẽ xuất hiện tại đây." icon={ClockCounterClockwise} />
            )}
          </div>
        </div>
      </section>
    </div>
  )
}

function PrivateSection({ title }: { title: string }) {
  return (
    <div className="mx-auto max-w-2xl rounded-2xl border border-dashed border-border bg-surface p-10 text-center">
      <div className="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-surface-muted text-muted">
        <LockKey size={26} />
      </div>
      <h2 className="mt-4 text-xl font-bold text-heading">{title}</h2>
      <p className="mt-2 text-sm leading-6 text-muted">
        Độc giả này chưa chia sẻ phần nội dung này trên hồ sơ công khai.
      </p>
    </div>
  )
}

function PrivatePreview({ label }: { label: string }) {
  return (
    <div className="flex min-h-40 items-center gap-4 rounded-2xl border border-dashed border-border bg-surface p-6">
      <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-surface-muted text-muted">
        <LockKey size={21} />
      </div>
      <div>
        <p className="font-semibold text-heading">{label} đang riêng tư</p>
        <p className="mt-1 text-sm text-muted">Chỉ chủ hồ sơ có thể xem nội dung này.</p>
      </div>
    </div>
  )
}

function shelfLabel(shelf: Shelf) {
  if (shelf === 'READING') return 'Đang đọc'
  if (shelf === 'READ') return 'Đã đọc'
  return 'Muốn đọc'
}
