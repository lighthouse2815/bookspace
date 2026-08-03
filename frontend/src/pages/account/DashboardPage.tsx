import { BookOpenText, Fire, Flag, Hourglass, Lightning, Play, Sparkle, TrendUp } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'
import { BookCover } from '../../components/books/BookCover'
import { Progress } from '../../components/ui/Progress'
import { ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useInsightsOverview } from '../../hooks/useInsights'
import { useOnboarding } from '../../hooks/useOnboarding'
import { useReadingGoals } from '../../hooks/useReadingProduct'
import { useDashboard } from '../../hooks/useSocialProduct'
import { formatDate } from '../../lib/format'

export function DashboardPage() {
  const { user } = useAuth()
  const dashboard = useDashboard()
  const onboarding = useOnboarding()
  const readingGoals = useReadingGoals()
  const localInsights = useInsightsOverview(30)

  if (dashboard.isLoading) {
    return (
      <div className="container-page section-space">
        <LoadingRows count={5} />
      </div>
    )
  }

  if (dashboard.isError || !dashboard.data) {
    return (
      <div className="container-page section-space">
        <ErrorState message="Không thể tải tổng quan đọc sách." retry={() => void dashboard.refetch()} />
      </div>
    )
  }

  const data = dashboard.data
  const maxWeekly = Math.max(...data.weeklyPages.map((item) => item.value), 1)
  const activeGoal = readingGoals.data?.items.find((goal) => goal.status === 'ACTIVE')
  const activeGoalUnit = activeGoal
    ? { BOOKS: 'cuốn', PAGES: 'trang', MINUTES: 'phút' }[activeGoal.metric]
    : ''

  return (
    <div className="container-page section-space">
      <div>
        <p className="text-sm font-semibold text-accent-strong">Tổng quan của bạn</p>
        <h1 className="page-title mt-2">Chào {user?.displayName}, hôm nay mình đọc gì?</h1>
        <p className="mt-3 text-muted">Nhịp đọc gần đây và những cuốn sách đang chờ bạn quay lại.</p>
      </div>

      {onboarding.data?.status === 'PENDING' || onboarding.data?.status === 'SKIPPED' ? (
        <section className="mt-8 grid gap-5 rounded-2xl border border-accent/25 bg-accent-soft p-5 sm:grid-cols-[auto_minmax(0,1fr)_auto] sm:items-center sm:p-6">
          <div className="grid h-11 w-11 place-items-center rounded-xl bg-accent text-white">
            <Sparkle size={22} weight="fill" aria-hidden />
          </div>
          <div>
            <h2 className="font-bold text-heading">
              {onboarding.data.status === 'PENDING'
                ? 'Tiếp tục cá nhân hóa góc đọc'
                : 'Làm mới gợi ý dành cho bạn'}
            </h2>
            <p className="mt-1 text-sm leading-6 text-muted">
              Chọn vài chủ đề và cuốn sách yêu thích để BookSpace hiểu gu đọc của bạn.
            </p>
          </div>
          <Link
            to="/onboarding"
            state={{ from: '/dashboard' }}
            className="button button-primary button-md"
          >
            {onboarding.data.status === 'PENDING' ? 'Tiếp tục thiết lập' : 'Thiết lập ngay'}
          </Link>
        </section>
      ) : null}

      <section className="mt-10 grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-2 xl:grid-cols-4">
        {[
          { icon: BookOpenText, value: data.booksRead, label: 'Cuốn đã đọc' },
          { icon: TrendUp, value: data.pagesRead, label: 'Trang đã đọc' },
          { icon: Hourglass, value: data.readingMinutes, label: 'Phút tập trung' },
          {
            icon: Fire,
            value: localInsights.data?.currentStreak ?? data.currentStreak,
            label: 'Ngày liên tiếp',
          },
        ].map(({ icon: Icon, value, label }) => (
          <div key={label} className="bg-surface p-6">
            <Icon size={23} weight="duotone" className="text-accent-strong" />
            <p className="mt-5 text-3xl font-bold tracking-tight text-heading">{value.toLocaleString('vi-VN')}</p>
            <p className="mt-1 text-sm text-muted">{label}</p>
          </div>
        ))}
      </section>

      <section className="mt-8 overflow-hidden rounded-2xl border border-border bg-surface">
        <div className="grid gap-6 p-5 sm:p-7 lg:grid-cols-[auto_minmax(0,1fr)_auto] lg:items-center">
          <div className="grid h-12 w-12 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
            <Flag size={24} weight="duotone" />
          </div>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-accent-strong">Mục tiêu đang theo đuổi</p>
            {readingGoals.isLoading ? (
              <div className="mt-3 h-6 w-52 animate-pulse rounded bg-surface-muted" />
            ) : readingGoals.isError ? (
              <p className="mt-2 text-sm text-muted">Chưa thể tải mục tiêu. Bạn có thể thử lại ở trang Mục tiêu đọc.</p>
            ) : activeGoal ? (
              <>
                <div className="mt-2 flex flex-wrap items-baseline justify-between gap-x-5 gap-y-1">
                  <h2 className="text-xl font-bold text-heading">
                    {activeGoal.currentValue.toLocaleString('vi-VN')}/{activeGoal.targetValue.toLocaleString('vi-VN')} {activeGoalUnit}
                  </h2>
                  <p className="text-sm text-muted">Kết thúc {formatDate(activeGoal.endDate)}</p>
                </div>
                <Progress value={activeGoal.progressPercent} className="mt-4 max-w-3xl" />
              </>
            ) : (
              <p className="mt-2 text-sm text-muted">Chưa có mục tiêu đang thực hiện. Hãy chọn một mốc nhỏ cho nhịp đọc tiếp theo.</p>
            )}
          </div>
          <Link to="/goals" className="button button-secondary button-sm justify-self-start lg:justify-self-end">
            {activeGoal ? 'Xem mục tiêu' : 'Tạo mục tiêu'}
          </Link>
        </div>
      </section>

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.25fr_0.75fr]">
        <section className="surface p-5 sm:p-7">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="text-xl font-bold text-heading">Đang đọc</h2>
              <p className="mt-1 text-sm text-muted">Tiếp tục từ nơi bạn đã dừng.</p>
            </div>
            <Link to="/library?shelf=READING" className="text-sm font-semibold text-accent-strong hover:underline">
              Mở thư viện
            </Link>
          </div>
          {data.currentlyReading.length ? (
            <div className="mt-6 space-y-4">
              {data.currentlyReading.map((entry) => (
                <article key={entry.id} className="flex gap-4 rounded-xl p-2 transition-colors hover:bg-surface-muted">
                  <Link to={`/books/${entry.book.id}`} aria-label={`Xem ${entry.book.title}`}>
                    <BookCover
                      src={entry.book.coverImageUrl}
                      title={entry.book.title}
                      className="h-24 w-16 shrink-0 rounded-lg"
                    />
                  </Link>
                  <div className="min-w-0 flex-1 py-1">
                    <Link to={`/books/${entry.book.id}`} className="block truncate font-semibold text-heading hover:text-accent-strong">
                      {entry.book.title}
                    </Link>
                    <p className="mt-1 text-sm text-muted">{entry.book.author?.name}</p>
                    <Progress
                      value={entry.progressPercent}
                      label={`${entry.currentPage}/${entry.book.pageCount ?? '?'} trang`}
                      className="mt-4"
                    />
                    <Link
                      to={`/journal?bookId=${entry.bookId}`}
                      className="mt-3 inline-flex items-center gap-1.5 text-xs font-bold text-accent-strong hover:underline"
                    >
                      <Play size={14} weight="fill" />
                      Bắt đầu phiên đọc
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <div className="mt-6 rounded-xl bg-surface-muted p-5 text-sm text-muted">
              Chưa có sách đang đọc.{' '}
              <Link to="/books" className="font-semibold text-accent-strong">
                Chọn một cuốn
              </Link>
            </div>
          )}
        </section>

        <section className="surface p-5 sm:p-7">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h2 className="text-xl font-bold text-heading">Nhịp đọc 7 ngày</h2>
              <p className="mt-1 text-sm text-muted">Số trang hoàn thành mỗi ngày.</p>
            </div>
            <Link to="/insights" className="shrink-0 text-sm font-semibold text-accent-strong hover:underline">
              Xem phân tích
            </Link>
          </div>
          <div className="mt-8 flex h-48 items-end gap-2" role="img" aria-label="Biểu đồ số trang đọc trong bảy ngày">
            {data.weeklyPages.map((item) => (
              <div key={item.label} className="flex h-full flex-1 flex-col items-center justify-end gap-2">
                <span className="text-xs font-semibold text-heading">{item.value}</span>
                <div
                  className="w-full max-w-8 rounded-t-lg bg-accent transition-[height] duration-500 motion-reduce:transition-none"
                  style={{ height: `${Math.max((item.value / maxWeekly) * 78, 4)}%` }}
                />
                <span className="text-[11px] text-muted">{item.label}</span>
              </div>
            ))}
          </div>
        </section>
      </div>

      <div className="mt-8 grid gap-8 lg:grid-cols-2">
        <section>
          <div className="flex items-center justify-between gap-4">
            <h2 className="text-xl font-bold text-heading">Phiên đọc gần đây</h2>
            <Link to="/journal" className="text-sm font-semibold text-accent-strong hover:underline">
              Xem nhật ký
            </Link>
          </div>
          <div className="mt-4 space-y-3">
            {data.recentSessions.slice(0, 4).map((session) => (
              <div key={session.id} className="surface flex items-center gap-4 p-4">
                <div className="grid h-10 w-10 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                  <Lightning size={20} weight="duotone" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold text-heading">{session.book?.title || 'Phiên đọc'}</p>
                  <p className="mt-1 text-xs text-muted">{formatDate(session.startedAt)}</p>
                </div>
                <div className="text-right text-sm">
                  <p className="font-semibold text-heading">{session.pagesRead} trang</p>
                  <p className="text-xs text-muted">{session.durationMinutes} phút</p>
                </div>
              </div>
            ))}
            {!data.recentSessions.length ? (
              <p className="rounded-xl border border-border p-5 text-sm text-muted">Chưa có phiên đọc nào.</p>
            ) : null}
          </div>
        </section>

        <section>
          <div className="flex items-center justify-between gap-4">
            <h2 className="text-xl font-bold text-heading">Thử thách đang tham gia</h2>
            <Link to="/challenges" className="text-sm font-semibold text-accent-strong hover:underline">
              Tìm thử thách
            </Link>
          </div>
          <div className="mt-4 space-y-3">
            {data.activeChallenges.slice(0, 4).map((challenge) => (
              <div key={challenge.id} className="surface p-4">
                <div className="flex justify-between gap-4">
                  <div>
                    <p className="font-semibold text-heading">{challenge.title}</p>
                    <p className="mt-1 text-xs text-muted">Kết thúc {formatDate(challenge.endDate)}</p>
                  </div>
                  <span className="text-sm font-semibold text-heading">
                    {challenge.currentBooks}/{challenge.goalBooks}
                  </span>
                </div>
                <Progress value={(challenge.currentBooks / Math.max(challenge.goalBooks, 1)) * 100} className="mt-4" />
              </div>
            ))}
            {!data.activeChallenges.length ? (
              <p className="rounded-xl border border-border p-5 text-sm text-muted">
                Bạn chưa tham gia thử thách nào.
              </p>
            ) : null}
          </div>
        </section>
      </div>
    </div>
  )
}
