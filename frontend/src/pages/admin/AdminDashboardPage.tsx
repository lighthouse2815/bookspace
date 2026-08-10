import {
  ArrowRight,
  BookOpenText,
  Books,
  ChatsCircle,
  ClockCounterClockwise,
  Flag,
  ShieldWarning,
  TrendUp,
  Users,
} from '@phosphor-icons/react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { AdminNav } from '../../components/admin/AdminNav'
import { ErrorState, LoadingRows } from '../../components/ui/States'
import { adminService } from '../../services/admin.service'

const number = new Intl.NumberFormat('vi-VN')
const dateTime = new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short',
  timeStyle: 'short',
})
const shortDate = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  timeZone: 'UTC',
})

export function AdminDashboardPage() {
  const dashboard = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: adminService.dashboard,
  })

  if (dashboard.isLoading) {
    return (
      <div className="container-page section-space">
        <AdminNav />
        <LoadingRows count={6} />
      </div>
    )
  }

  if (dashboard.isError || !dashboard.data) {
    return (
      <div className="container-page section-space">
        <AdminNav />
        <ErrorState
          message="Không thể tải tổng quan vận hành."
          retry={() => void dashboard.refetch()}
        />
      </div>
    )
  }

  const data = dashboard.data
  const maxDailyPages = Math.max(...data.activityLast7Days.map((item) => item.pagesRead), 1)

  return (
    <div className="container-page section-space">
      <AdminNav />
      <header className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Vận hành BookSpace</p>
          <h1 className="page-title mt-3">Tổng quan quản trị</h1>
          <p className="section-copy mt-4">
            Sức khỏe cộng đồng, catalog và hoạt động đọc từ dữ liệu BookSpace.
          </p>
        </div>
        <p className="text-xs text-muted">
          Cập nhật {dateTime.format(new Date(data.generatedAt))}
        </p>
      </header>

      {data.pendingReports > 0 ? (
        <section className="mt-8 flex flex-wrap items-center gap-4 rounded-2xl border border-amber-300 bg-amber-50 px-5 py-4 text-amber-950 dark:border-amber-800 dark:bg-amber-950/25 dark:text-amber-100">
          <div className="grid h-10 w-10 place-items-center rounded-xl bg-amber-200/70 dark:bg-amber-900/70">
            <ShieldWarning size={21} weight="duotone" aria-hidden />
          </div>
          <div className="min-w-0 flex-1">
            <h2 className="font-semibold">{number.format(data.pendingReports)} báo cáo cần xem xét</h2>
            <p className="mt-1 text-sm opacity-80">Ưu tiên xử lý hàng đợi để giữ cộng đồng an toàn.</p>
          </div>
          <Link to="/admin/moderation" className="button button-secondary button-sm">
            Mở hàng đợi
            <ArrowRight size={16} />
          </Link>
        </section>
      ) : null}

      <section className="mt-8 overflow-hidden rounded-2xl border border-border bg-border">
        <h2 className="sr-only">Chỉ số vận hành chính</h2>
        <div className="grid gap-px sm:grid-cols-2 xl:grid-cols-4">
          {[
            {
              icon: Users,
              value: data.totalUsers,
              label: 'Tài khoản hoạt động',
              detail: `+${number.format(data.newUsersLast30Days)} trong 30 ngày`,
            },
            {
              icon: Books,
              value: data.totalBooks,
              label: 'Sách trong catalog',
              detail: `${number.format(data.totalAuthors)} tác giả · ${number.format(data.totalCategories)} thể loại`,
            },
            {
              icon: ChatsCircle,
              value: data.totalReviews,
              label: 'Đánh giá công khai',
              detail: `${number.format(data.totalClubs)} câu lạc bộ`,
            },
            {
              icon: Flag,
              value: data.activeChallenges,
              label: 'Thử thách đang diễn ra',
              detail: `${number.format(data.publishedChallenges)} đã xuất bản`,
            },
          ].map(({ icon: Icon, value, label, detail }) => (
            <article key={label} className="bg-surface p-6">
              <Icon size={23} weight="duotone" className="text-accent-strong" aria-hidden />
              <p className="mt-5 text-3xl font-bold tabular-nums tracking-tight text-heading">
                {number.format(value)}
              </p>
              <h3 className="mt-1 text-sm font-semibold text-heading">{label}</h3>
              <p className="mt-2 text-xs leading-5 text-muted">{detail}</p>
            </article>
          ))}
        </div>
      </section>

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.35fr_0.65fr]">
        <section className="surface p-5 sm:p-7">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h2 className="text-xl font-bold text-heading">Nhịp đọc 7 ngày</h2>
              <p className="mt-1 text-sm text-muted">Số trang ghi nhận trên toàn hệ thống.</p>
            </div>
            <div className="flex gap-5 text-right">
              <div>
                <p className="text-xl font-bold tabular-nums text-heading">
                  {number.format(data.activeReadersLast30Days)}
                </p>
                <p className="text-xs text-muted">độc giả / 30 ngày</p>
              </div>
              <div>
                <p className="text-xl font-bold tabular-nums text-heading">
                  {number.format(data.readingSessionsLast30Days)}
                </p>
                <p className="text-xs text-muted">phiên / 30 ngày</p>
              </div>
            </div>
          </div>
          <div
            className="mt-8 flex h-56 items-end gap-2 sm:gap-4"
            role="img"
            aria-label="Biểu đồ số trang đọc trong bảy ngày gần nhất"
          >
            {data.activityLast7Days.map((item) => (
              <div key={item.date} className="flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-2">
                <span className="text-xs font-semibold tabular-nums text-heading">
                  {number.format(item.pagesRead)}
                </span>
                <div
                  className="w-full max-w-10 rounded-t-lg bg-accent transition-[height] duration-500 motion-reduce:transition-none"
                  style={{ height: `${Math.max((item.pagesRead / maxDailyPages) * 78, 3)}%` }}
                  title={`${number.format(item.pagesRead)} trang, ${number.format(item.readingSessions)} phiên, ${number.format(item.newUsers)} tài khoản mới`}
                />
                <span className="text-[11px] tabular-nums text-muted">
                  {shortDate.format(new Date(`${item.date}T00:00:00Z`))}
                </span>
              </div>
            ))}
          </div>
          <div className="mt-6 grid gap-px overflow-hidden rounded-xl bg-border sm:grid-cols-2">
            <div className="bg-surface-muted p-4">
              <p className="text-xs font-medium text-muted">Trang đọc / 30 ngày</p>
              <p className="mt-1 text-2xl font-bold tabular-nums text-heading">
                {number.format(data.pagesReadLast30Days)}
              </p>
            </div>
            <div className="bg-surface-muted p-4">
              <p className="text-xs font-medium text-muted">Phút đọc / 30 ngày</p>
              <p className="mt-1 text-2xl font-bold tabular-nums text-heading">
                {number.format(data.readingMinutesLast30Days)}
              </p>
            </div>
          </div>
        </section>

        <section className="surface p-5 sm:p-7">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-xl font-bold text-heading">Tài khoản mới</h2>
              <p className="mt-1 text-sm text-muted">Đăng ký gần đây nhất.</p>
            </div>
            <ClockCounterClockwise size={23} weight="duotone" className="text-accent-strong" aria-hidden />
          </div>
          <div className="mt-6 divide-y divide-border">
            {data.recentUsers.map((user) => (
              <article key={user.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                <div className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-accent-soft text-sm font-bold text-accent-strong">
                  {user.displayName.trim().charAt(0).toLocaleUpperCase('vi-VN')}
                </div>
                <div className="min-w-0 flex-1">
                  <Link to={`/users/${user.id}`} className="block truncate text-sm font-semibold text-heading hover:text-accent-strong">
                    {user.displayName}
                  </Link>
                  <p className="mt-0.5 text-xs text-muted">{dateTime.format(new Date(user.joinedAt))}</p>
                </div>
                <span className={`text-xs font-semibold ${user.isLocked ? 'text-red-600 dark:text-red-400' : 'text-muted'}`}>
                  {user.isLocked ? 'Đã khóa' : user.role === 'ADMIN' ? 'Admin' : 'Độc giả'}
                </span>
              </article>
            ))}
          </div>
          {!data.recentUsers.length ? (
            <p className="mt-6 rounded-xl bg-surface-muted p-4 text-sm text-muted">Chưa có tài khoản.</p>
          ) : null}
          {data.lockedUsers > 0 ? (
            <p className="mt-5 text-xs text-muted">
              {number.format(data.lockedUsers)} tài khoản đang bị khóa bởi kiểm duyệt.
            </p>
          ) : null}
        </section>
      </div>

      <section className="mt-8 grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-3">
        {[
          { to: '/admin/books', icon: BookOpenText, title: 'Quản lý catalog', text: 'Thêm sách và rà soát metadata.' },
          { to: '/admin/challenges', icon: TrendUp, title: 'Điều phối thử thách', text: 'Tạo, xuất bản và theo dõi chương trình.' },
          { to: '/admin/moderation', icon: ShieldWarning, title: 'An toàn cộng đồng', text: 'Xử lý báo cáo và lưu dấu quyết định.' },
        ].map(({ to, icon: Icon, title, text }) => (
          <Link key={to} to={to} className="group bg-surface p-5 transition-colors hover:bg-surface-muted focus-visible:focus-ring">
            <Icon size={22} weight="duotone" className="text-accent-strong" aria-hidden />
            <h2 className="mt-4 font-semibold text-heading">{title}</h2>
            <p className="mt-1 text-sm leading-6 text-muted">{text}</p>
            <span className="mt-4 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong">
              Mở công cụ <ArrowRight size={15} className="transition-transform group-hover:translate-x-0.5" />
            </span>
          </Link>
        ))}
      </section>
    </div>
  )
}
