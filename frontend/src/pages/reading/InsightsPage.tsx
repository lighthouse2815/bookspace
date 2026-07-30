import {
  ArrowRight,
  BookOpenText,
  Books,
  CalendarBlank,
  ChartBar,
  Clock,
  Fire,
  Flag,
  TrendUp,
} from '@phosphor-icons/react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { BookCover } from '../../components/books/BookCover'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState } from '../../components/ui/States'
import {
  useInsightsCalendar,
  useInsightsMonthly,
  useInsightsOverview,
  useInsightsWeekly,
} from '../../hooks/useInsights'
import { formatDate } from '../../lib/format'
import type {
  ReadingCalendarDay,
  ReadingInsightsCalendar,
  ReadingInsightsMonth,
  ReadingInsightsWeek,
} from '../../types/domain'

type InsightRange = 30 | 90 | 365

const rangeOptions: Array<{ value: InsightRange; label: string; weeks: number }> = [
  { value: 30, label: '30 ngày', weeks: 4 },
  { value: 90, label: '90 ngày', weeks: 12 },
  { value: 365, label: '365 ngày', weeks: 52 },
]

const monthFormatter = new Intl.DateTimeFormat('vi-VN', { month: 'short' })
const dayFormatter = new Intl.DateTimeFormat('vi-VN', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
})
const shortDateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
})

function parseDateOnly(value: string) {
  return new Date(`${value}T00:00:00`)
}

function formatDateOnly(value: string) {
  return formatDate(`${value}T00:00:00`)
}

function number(value: number, maximumFractionDigits = 0) {
  return value.toLocaleString('vi-VN', { maximumFractionDigits })
}

function activityLevel(day: ReadingCalendarDay, maxValue: number, usePages: boolean) {
  if (!day.isActive) return 0
  const value = usePages ? day.pagesRead : day.minutesRead
  if (value <= 0 || maxValue <= 0) return 1
  const ratio = value / maxValue
  if (ratio <= 0.25) return 1
  if (ratio <= 0.5) return 2
  if (ratio <= 0.75) return 3
  return 4
}

function levelClass(level: number) {
  if (level === 1) return 'border-accent/10 bg-accent/20'
  if (level === 2) return 'border-accent/20 bg-accent/40'
  if (level === 3) return 'border-accent/30 bg-accent/65'
  if (level === 4) return 'border-accent bg-accent'
  return 'border-border/70 bg-surface-muted'
}

function heatmapLabel(day: ReadingCalendarDay) {
  const date = dayFormatter.format(parseDateOnly(day.date))
  if (!day.isActive) return `${date}: không có hoạt động đọc`
  return `${date}: ${number(day.pagesRead)} trang, ${number(day.minutesRead)} phút trong ${number(day.sessionCount)} phiên`
}

function Heatmap({ calendar }: { calendar: ReadingInsightsCalendar }) {
  const weeks = useMemo(() => {
    const sortedDays = [...calendar.daysData].sort((a, b) => a.date.localeCompare(b.date))
    const firstDay = sortedDays[0]
    const leadingEmpty = firstDay ? (parseDateOnly(firstDay.date).getDay() + 6) % 7 : 0
    const cells: Array<ReadingCalendarDay | null> = [
      ...Array.from({ length: leadingEmpty }, () => null),
      ...sortedDays,
    ]
    while (cells.length % 7 !== 0) cells.push(null)

    return Array.from({ length: cells.length / 7 }, (_, index) =>
      cells.slice(index * 7, index * 7 + 7),
    )
  }, [calendar.daysData])

  const maxPages = Math.max(...calendar.daysData.map((day) => day.pagesRead), 0)
  const maxMinutes = Math.max(...calendar.daysData.map((day) => day.minutesRead), 0)
  const usePages = maxPages > 0
  const maxValue = usePages ? maxPages : maxMinutes

  return (
    <div>
      <div
        className="overflow-x-auto pb-3 [scrollbar-color:var(--border)_transparent]"
        aria-label={
          calendar.year
            ? `Lịch hoạt động đọc năm ${calendar.year}`
            : `Lịch hoạt động đọc từ ${calendar.fromDate} đến ${calendar.toDate}`
        }
      >
        <div className="flex min-w-[850px] gap-2">
          <div className="grid shrink-0 grid-rows-[1.35rem_repeat(7,0.78rem)] gap-[3px] pt-px text-[10px] text-muted">
            <span aria-hidden />
            <span aria-hidden />
            <span className="flex items-center">T2</span>
            <span aria-hidden />
            <span className="flex items-center">T4</span>
            <span aria-hidden />
            <span className="flex items-center">T6</span>
            <span aria-hidden />
          </div>
          <div className="flex flex-1 gap-[3px]">
            {weeks.map((week, weekIndex) => {
              const monthStart = week.find((day) => day?.date.endsWith('-01'))
              return (
                <div
                  key={`${calendar.fromDate}-week-${weekIndex}`}
                  className="grid min-w-3 flex-1 grid-rows-[1.35rem_repeat(7,0.78rem)] gap-[3px]"
                >
                  <span className="overflow-visible whitespace-nowrap text-[10px] font-medium text-muted">
                    {monthStart ? monthFormatter.format(parseDateOnly(monthStart.date)) : ''}
                  </span>
                  {week.map((day, dayIndex) =>
                    day ? (
                      <span key={day.date} className="group relative block">
                        <time
                          dateTime={day.date}
                          tabIndex={day.isActive ? 0 : undefined}
                          className={`block h-full w-full rounded-[3px] border transition-transform hover:scale-125 focus-visible:scale-125 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-1 focus-visible:ring-offset-surface ${levelClass(
                            activityLevel(day, maxValue, usePages),
                          )}`}
                          aria-label={heatmapLabel(day)}
                          title={heatmapLabel(day)}
                        />
                        {day.isActive ? (
                          <span
                            aria-hidden
                            className="pointer-events-none absolute bottom-[calc(100%+0.4rem)] left-1/2 z-30 hidden w-max max-w-64 -translate-x-1/2 rounded-lg border border-border bg-heading px-2.5 py-2 text-center text-[10px] leading-4 text-page shadow-xl group-hover:block group-focus-within:block"
                          >
                            {heatmapLabel(day)}
                          </span>
                        ) : null}
                      </span>
                    ) : (
                      <span key={`empty-${weekIndex}-${dayIndex}`} aria-hidden />
                    ),
                  )}
                </div>
              )
            })}
          </div>
        </div>
      </div>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-xs text-muted">
        <p>
          {number(calendar.activeDays)} ngày đọc · {number(calendar.totalPages)} trang ·{' '}
          {number(calendar.totalMinutes)} phút
        </p>
        <div className="flex items-center gap-1.5" aria-label="Mức độ hoạt động: từ ít đến nhiều">
          <span className="mr-1">Ít</span>
          {[0, 1, 2, 3, 4].map((level) => (
            <span
              key={level}
              className={`h-3 w-3 rounded-[3px] border ${levelClass(level)}`}
              aria-hidden
            />
          ))}
          <span className="ml-1">Nhiều</span>
        </div>
      </div>
    </div>
  )
}

function WeeklyChart({ weeks }: { weeks: ReadingInsightsWeek[] }) {
  const maxPages = Math.max(...weeks.map((week) => week.pages), 1)
  const chartWidth = Math.max(680, weeks.length * 44)

  return (
    <div
      className="overflow-x-auto pb-3 [scrollbar-color:var(--border)_transparent]"
      aria-label={`Biểu đồ số trang đọc trong ${weeks.length} tuần`}
    >
      <ol
        className="flex h-64 items-end gap-2"
        style={{ minWidth: `${chartWidth}px` }}
      >
        {weeks.map((week, index) => {
          const height = week.pages > 0 ? Math.max((week.pages / maxPages) * 82, 4) : 2
          const label = `${shortDateFormatter.format(parseDateOnly(week.weekStart))} – ${shortDateFormatter.format(
            parseDateOnly(week.weekEnd),
          )}`
          return (
            <li
              key={week.weekStart}
              className="group flex h-full min-w-0 flex-1 flex-col items-center justify-end"
              aria-label={`${label}: ${number(week.pages)} trang, ${number(week.minutes)} phút, ${number(week.sessions)} phiên`}
            >
              <span
                className={`mb-2 text-[10px] font-semibold text-heading ${
                  weeks.length > 12 ? 'opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100' : ''
                }`}
              >
                {number(week.pages)}
              </span>
              <div className="relative flex h-[76%] w-full max-w-8 items-end">
                <div
                  className="w-full rounded-t-md bg-accent transition-[height,filter] duration-500 hover:brightness-110 motion-reduce:transition-none"
                  style={{ height: `${height}%` }}
                  title={`${label}: ${number(week.pages)} trang`}
                />
              </div>
              <span className="mt-2 whitespace-nowrap text-[9px] text-muted">
                {index === 0 || index === weeks.length - 1 || weeks.length <= 12 || index % 4 === 0
                  ? shortDateFormatter.format(parseDateOnly(week.weekStart))
                  : ''}
              </span>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

function MonthlyChart({ months }: { months: ReadingInsightsMonth[] }) {
  const maxPages = Math.max(...months.map((month) => month.pages), 1)
  const monthLabel = new Intl.DateTimeFormat('vi-VN', { month: 'short', year: '2-digit' })
  const chartWidth = Math.max(680, months.length * 60)

  return (
    <div
      className="overflow-x-auto pb-3 [scrollbar-color:var(--border)_transparent]"
      aria-label={`Biểu đồ báo cáo ${months.length} tháng`}
    >
      <ol className="flex h-64 items-end gap-3" style={{ minWidth: `${chartWidth}px` }}>
        {months.map((month) => {
          const height = month.pages > 0 ? Math.max((month.pages / maxPages) * 82, 4) : 2
          const label = monthLabel.format(parseDateOnly(month.monthStart))
          return (
            <li
              key={month.monthStart}
              className="group flex h-full min-w-0 flex-1 flex-col items-center justify-end"
              aria-label={`${label}: ${number(month.pages)} trang, ${number(month.minutes)} phút, ${number(month.sessions)} phiên, ${number(month.booksFinished)} sách hoàn thành`}
            >
              <span className="mb-2 text-[10px] font-semibold text-heading">{number(month.pages)}</span>
              <div className="relative flex h-[76%] w-full max-w-10 items-end">
                <div
                  className="w-full rounded-t-lg bg-accent transition-[height,filter] duration-500 hover:brightness-110 motion-reduce:transition-none"
                  style={{ height: `${height}%` }}
                  title={`${label}: ${number(month.pages)} trang`}
                />
              </div>
              <span className="mt-2 whitespace-nowrap text-[10px] font-medium text-muted">{label}</span>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

function DeltaBadge({ value }: { value: number | null }) {
  if (value == null) {
    return (
      <span className="rounded-full bg-surface-muted px-2 py-1 text-[11px] font-semibold text-muted">
        Chưa đủ dữ liệu
      </span>
    )
  }

  const isPositive = value > 0
  const isNegative = value < 0
  const label = `${isPositive ? 'Tăng' : isNegative ? 'Giảm' : 'Không đổi'} ${number(Math.abs(value), 1)}%`

  return (
    <span
      className={`rounded-full px-2 py-1 text-[11px] font-bold ${
        isPositive
          ? 'bg-accent-soft text-accent-strong'
          : isNegative
            ? 'bg-red-100 text-red-700 dark:bg-red-950/35 dark:text-red-300'
            : 'bg-surface-muted text-muted'
      }`}
      aria-label={`${label} so với kỳ trước`}
    >
      {isPositive ? '↑' : isNegative ? '↓' : '—'} {number(Math.abs(value), 1)}%
    </span>
  )
}

function InsightsLoading() {
  return (
    <div className="container-page section-space" aria-label="Đang tải phân tích đọc" aria-busy="true">
      <div className="h-4 w-32 animate-pulse rounded bg-surface-muted" />
      <div className="mt-4 h-12 max-w-xl animate-pulse rounded-xl bg-surface-muted" />
      <div className="mt-10 grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <div className="h-64 animate-pulse rounded-2xl bg-surface-muted" />
        <div className="h-64 animate-pulse rounded-2xl bg-surface-muted" />
      </div>
      <div className="mt-8 h-72 animate-pulse rounded-2xl bg-surface-muted" />
      <div className="mt-8 h-80 animate-pulse rounded-2xl bg-surface-muted" />
    </div>
  )
}

export function InsightsPage() {
  const [range, setRange] = useState<InsightRange>(90)
  const [monthlyMonths, setMonthlyMonths] = useState<6 | 12 | 24>(12)
  const weeks = rangeOptions.find((option) => option.value === range)?.weeks ?? 12
  const overview = useInsightsOverview(range)
  const calendar = useInsightsCalendar({ days: 365 })
  const weekly = useInsightsWeekly(weeks)
  const monthly = useInsightsMonthly(monthlyMonths)

  if (overview.isLoading) return <InsightsLoading />

  if (overview.isError || !overview.data) {
    return (
      <div className="container-page section-space">
        <ErrorState
          message="Không thể tải dữ liệu phân tích đọc của bạn."
          retry={() => void overview.refetch()}
        />
      </div>
    )
  }

  const data = overview.data
  const hasActivity = data.totalSessions > 0
  const maxForecastDays = Math.max(
    ...data.forecasts.map((forecast) => forecast.estimatedDaysRemaining ?? 0),
    1,
  )

  return (
    <div className="container-page section-space max-w-7xl">
      <header className="flex flex-wrap items-end justify-between gap-6">
        <div>
          <p className="eyebrow">Dữ liệu của riêng bạn</p>
          <h1 className="page-title mt-4">Phân tích nhịp đọc</h1>
          <p className="mt-3 max-w-2xl text-muted">
            Nhìn lại thói quen, duy trì chuỗi ngày đọc và biết cuốn sách nào sắp về đích.
          </p>
        </div>
        <div
          className="flex rounded-xl border border-border bg-surface p-1"
          role="group"
          aria-label="Khoảng thời gian phân tích"
        >
          {rangeOptions.map((option) => (
            <button
              key={option.value}
              type="button"
              onClick={() => setRange(option.value)}
              className={`rounded-lg px-3 py-2 text-sm font-semibold transition-colors focus-visible:focus-ring ${
                range === option.value
                  ? 'bg-accent text-white'
                  : 'text-muted hover:bg-surface-muted hover:text-heading'
              }`}
              aria-pressed={range === option.value}
            >
              {option.label}
            </button>
          ))}
        </div>
      </header>

      <section className="mt-10 grid overflow-hidden rounded-2xl border border-border bg-surface lg:grid-cols-[1.25fr_0.75fr]">
        <div className="relative isolate min-h-64 overflow-hidden p-6 sm:p-8">
          <div
            className="pointer-events-none absolute -right-20 -top-28 -z-10 h-80 w-80 rounded-full bg-accent-soft blur-3xl"
            aria-hidden
          />
          <div className="flex items-center gap-3 text-accent-strong">
            <Fire size={26} weight="duotone" aria-hidden />
            <p className="eyebrow">Chuỗi hiện tại</p>
          </div>
          <div className="mt-7 flex flex-wrap items-end gap-x-4 gap-y-2">
            <p className="text-7xl font-bold tracking-[-0.07em] text-heading sm:text-8xl">
              {number(data.currentStreak)}
            </p>
            <p className="pb-2 text-lg font-semibold text-muted">ngày liên tiếp</p>
          </div>
          <p className="mt-5 max-w-lg text-sm leading-6 text-muted">
            {data.currentStreak
              ? 'Một phiên đọc hôm nay sẽ giữ cho nhịp này tiếp tục.'
              : 'Bắt đầu một phiên đọc hôm nay để tạo chuỗi mới.'}
          </p>
          <Link to="/journal" className="button button-primary button-sm mt-6">
            Mở nhật ký
            <ArrowRight size={16} />
          </Link>
        </div>
        <div className="grid grid-cols-2 gap-px border-t border-border bg-border lg:border-l lg:border-t-0">
          {[
            { label: 'Kỷ lục', value: data.longestStreak, unit: 'ngày' },
            { label: 'Ngày có đọc', value: data.activeDays, unit: `/${data.days}` },
            {
              label: 'Trang/ngày đọc',
              value: number(data.averagePagesPerActiveDay, 1),
              unit: 'trang',
            },
            {
              label: 'Phút/ngày đọc',
              value: number(data.averageMinutesPerActiveDay, 1),
              unit: 'phút',
            },
          ].map((item) => (
            <div key={item.label} className="bg-surface p-5 sm:p-6">
              <p className="text-2xl font-bold tracking-tight text-heading sm:text-3xl">{item.value}</p>
              <p className="mt-1 text-xs text-muted">{item.unit}</p>
              <p className="mt-4 text-sm font-semibold text-heading">{item.label}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="mt-6 grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-2 lg:grid-cols-4">
        {[
          { icon: BookOpenText, value: data.totalPages, label: 'Trang đã đọc' },
          { icon: Clock, value: data.totalMinutes, label: 'Phút tập trung' },
          { icon: TrendUp, value: data.totalSessions, label: 'Phiên đọc' },
          { icon: Books, value: data.booksFinished, label: 'Sách hoàn thành' },
        ].map(({ icon: Icon, value, label }) => (
          <div key={label} className="bg-surface p-5 sm:p-6">
            <Icon size={22} weight="duotone" className="text-accent-strong" aria-hidden />
            <p className="mt-5 text-3xl font-bold tracking-tight text-heading">{number(value)}</p>
            <p className="mt-1 text-sm text-muted">{label}</p>
          </div>
        ))}
      </section>

      <section className="surface mt-8 overflow-hidden">
        <div className="flex flex-wrap items-end justify-between gap-4 border-b border-border p-5 sm:p-6">
          <div>
            <p className="eyebrow">So với kỳ liền trước</p>
            <h2 className="mt-3 text-xl font-bold text-heading">
              {formatDateOnly(data.comparison.currentFromDate)} —{' '}
              {formatDateOnly(data.comparison.currentToDate)}
            </h2>
          </div>
          <p className="text-xs text-muted">
            Kỳ trước: {formatDateOnly(data.comparison.previousFromDate)} —{' '}
            {formatDateOnly(data.comparison.previousToDate)}
          </p>
        </div>
        <div className="grid gap-px bg-border sm:grid-cols-2 lg:grid-cols-5">
          {[
            { label: 'Phiên đọc', value: data.comparison.sessions },
            { label: 'Trang', value: data.comparison.pages },
            { label: 'Phút', value: data.comparison.minutes },
            { label: 'Ngày có đọc', value: data.comparison.activeDays },
            { label: 'Sách hoàn thành', value: data.comparison.booksFinished },
          ].map((item) => (
            <div key={item.label} className="bg-surface p-5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-xs font-semibold text-muted">{item.label}</p>
                <DeltaBadge value={item.value.changePercent} />
              </div>
              <p className="mt-5 text-2xl font-bold text-heading">{number(item.value.current)}</p>
              <p className="mt-1 text-xs text-muted">Kỳ trước {number(item.value.previous)}</p>
            </div>
          ))}
        </div>
      </section>

      {!hasActivity ? (
        <div className="mt-8">
          <EmptyState
            icon={BookOpenText}
            title={`Chưa có hoạt động trong ${range} ngày qua`}
            description="Ghi lại một phiên đọc để BookSpace bắt đầu tính chuỗi, mức trung bình và dự báo hoàn thành."
            action={
              <Link to="/journal" className="button button-primary button-md">
                Ghi phiên đọc đầu tiên
              </Link>
            }
          />
        </div>
      ) : null}

      <section className="surface mt-8 p-5 sm:p-7">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 text-accent-strong">
              <CalendarBlank size={20} weight="duotone" aria-hidden />
              <p className="eyebrow">Lịch đọc</p>
            </div>
            <h2 className="mt-3 text-2xl font-bold tracking-tight text-heading">
              Mỗi ô là một ngày bạn đã dành cho sách
            </h2>
          </div>
          <p className="rounded-full bg-surface-muted px-3 py-1.5 text-xs font-semibold text-muted">
            365 ngày gần nhất
          </p>
        </div>
        <div className="mt-7">
          {calendar.isLoading ? (
            <div className="h-40 animate-pulse rounded-xl bg-surface-muted" aria-label="Đang tải lịch đọc" />
          ) : calendar.isError || !calendar.data ? (
            <ErrorState
              message="Không thể tải lịch hoạt động đọc."
              retry={() => void calendar.refetch()}
            />
          ) : (
            <>
              <p className="mb-5 text-xs text-muted">
                {formatDateOnly(calendar.data.fromDate)} — {formatDateOnly(calendar.data.toDate)}
              </p>
              <Heatmap calendar={calendar.data} />
            </>
          )}
        </div>
      </section>

      <section className="surface mt-8 p-5 sm:p-7">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 text-accent-strong">
              <ChartBar size={20} weight="duotone" aria-hidden />
              <p className="eyebrow">Theo tuần</p>
            </div>
            <h2 className="mt-3 text-2xl font-bold tracking-tight text-heading">Nhịp trang sách theo thời gian</h2>
            <p className="mt-2 text-sm text-muted">Chiều cao cột biểu thị tổng số trang trong mỗi tuần.</p>
          </div>
          {weekly.data ? (
            <p className="text-xs text-muted">
              {formatDateOnly(weekly.data.fromDate)} — {formatDateOnly(weekly.data.toDate)}
            </p>
          ) : null}
        </div>
        <div className="mt-6">
          {weekly.isLoading ? (
            <div className="h-64 animate-pulse rounded-xl bg-surface-muted" aria-label="Đang tải biểu đồ tuần" />
          ) : weekly.isError || !weekly.data ? (
            <ErrorState message="Không thể tải dữ liệu theo tuần." retry={() => void weekly.refetch()} />
          ) : weekly.data.items.length ? (
            <WeeklyChart weeks={weekly.data.items} />
          ) : (
            <EmptyState
              icon={ChartBar}
              title="Chưa có dữ liệu theo tuần"
              description="Các cột hoạt động sẽ xuất hiện sau khi bạn ghi lại phiên đọc."
            />
          )}
        </div>
      </section>

      <section className="surface mt-8 p-5 sm:p-7">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 text-accent-strong">
              <CalendarBlank size={20} weight="duotone" aria-hidden />
              <p className="eyebrow">Báo cáo tháng</p>
            </div>
            <h2 className="mt-3 text-2xl font-bold tracking-tight text-heading">Bức tranh dài hạn</h2>
            <p className="mt-2 text-sm text-muted">
              Tổng số trang theo tháng, tính theo múi giờ hiện tại của bạn.
            </p>
          </div>
          <label className="field w-36">
            <span className="field-label">Số tháng</span>
            <select
              className="input"
              value={monthlyMonths}
              onChange={(event) => setMonthlyMonths(Number(event.target.value) as 6 | 12 | 24)}
            >
              <option value={6}>6 tháng</option>
              <option value={12}>12 tháng</option>
              <option value={24}>24 tháng</option>
            </select>
          </label>
        </div>
        <div className="mt-6">
          {monthly.isLoading ? (
            <div className="h-64 animate-pulse rounded-xl bg-surface-muted" aria-label="Đang tải báo cáo tháng" />
          ) : monthly.isError || !monthly.data ? (
            <ErrorState message="Không thể tải báo cáo theo tháng." retry={() => void monthly.refetch()} />
          ) : monthly.data.items.length ? (
            <>
              <p className="mb-4 text-xs text-muted">
                {formatDateOnly(monthly.data.fromDate)} — {formatDateOnly(monthly.data.toDate)}
              </p>
              <MonthlyChart months={monthly.data.items} />
            </>
          ) : (
            <EmptyState
              icon={CalendarBlank}
              title="Chưa có báo cáo tháng"
              description="Dữ liệu tháng sẽ xuất hiện sau khi bạn ghi lại hoạt động đọc."
            />
          )}
        </div>
      </section>

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.25fr_0.75fr]">
        <section>
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <p className="eyebrow">Dự báo hoàn thành</p>
              <h2 className="mt-3 text-2xl font-bold tracking-tight text-heading">Những cuốn đang về đích</h2>
            </div>
            <Link to="/library?shelf=READING" className="text-sm font-semibold text-accent-strong hover:underline">
              Mở thư viện
            </Link>
          </div>
          <div className="mt-5 space-y-4">
            {data.forecasts.length ? (
              data.forecasts.map((forecast) => {
                const progress =
                  forecast.pageCount > 0 ? (forecast.currentPage / forecast.pageCount) * 100 : 0
                const paceWidth = forecast.estimatedDaysRemaining
                  ? Math.max(
                      8,
                      100 - (forecast.estimatedDaysRemaining / maxForecastDays) * 100,
                    )
                  : 0
                return (
                  <article key={forecast.libraryItemId} className="surface flex gap-4 p-4 sm:gap-5 sm:p-5">
                    <Link to={`/books/${forecast.bookId}`} className="shrink-0">
                      <BookCover
                        src={forecast.coverImageUrl ?? undefined}
                        title={forecast.title}
                        className="h-28 w-[4.7rem] rounded-xl sm:h-32 sm:w-[5.35rem]"
                      />
                    </Link>
                    <div className="min-w-0 flex-1 py-1">
                      <Link
                        to={`/books/${forecast.bookId}`}
                        className="line-clamp-2 font-semibold text-heading hover:text-accent-strong"
                      >
                        {forecast.title}
                      </Link>
                      <p className="mt-2 text-xs text-muted">
                        {number(forecast.currentPage)}/{number(forecast.pageCount)} trang · còn{' '}
                        {number(forecast.remainingPages)} trang
                      </p>
                      <Progress value={progress} className="mt-3" />
                      <div className="mt-4 flex flex-wrap items-center justify-between gap-2 text-xs">
                        <span className="text-muted">
                          Nhịp hiện tại: {number(forecast.averagePagesPerDay, 1)} trang/ngày
                        </span>
                        {forecast.estimatedFinishDate && forecast.estimatedDaysRemaining != null ? (
                          <span className="font-semibold text-accent-strong">
                            Khoảng {formatDateOnly(forecast.estimatedFinishDate)} ·{' '}
                            {forecast.estimatedDaysRemaining} ngày
                          </span>
                        ) : (
                          <span className="font-medium text-muted">Cần thêm dữ liệu để dự báo</span>
                        )}
                      </div>
                      {paceWidth ? (
                        <div className="mt-3 h-1 overflow-hidden rounded-full bg-surface-muted" aria-hidden>
                          <div className="h-full rounded-full bg-accent/50" style={{ width: `${paceWidth}%` }} />
                        </div>
                      ) : null}
                    </div>
                  </article>
                )
              })
            ) : (
              <EmptyState
                icon={Books}
                title="Chưa có sách để dự báo"
                description="Thêm sách vào kệ Đang đọc và cập nhật trang hiện tại để nhận ngày hoàn thành dự kiến."
                action={
                  <Link to="/books" className="button button-secondary button-md">
                    Tìm sách
                  </Link>
                }
              />
            )}
          </div>
        </section>

        <aside className="space-y-5">
          <section className="surface p-5 sm:p-6">
            <div className="flex items-center gap-3">
              <div className="grid h-11 w-11 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                <Flag size={22} weight="duotone" aria-hidden />
              </div>
              <div>
                <h2 className="font-semibold text-heading">Sức khỏe mục tiêu</h2>
                <p className="mt-0.5 text-xs text-muted">Tất cả mục tiêu của bạn</p>
              </div>
            </div>
            <dl className="mt-6 grid grid-cols-2 gap-px overflow-hidden rounded-xl border border-border bg-border">
              {[
                ['Đang làm', data.goals.active],
                ['Hoàn thành', data.goals.completed],
                ['Hết hạn', data.goals.expired],
                ['Tổng cộng', data.goals.total],
              ].map(([label, value]) => (
                <div key={label} className="bg-surface p-4">
                  <dt className="text-xs text-muted">{label}</dt>
                  <dd className="mt-2 text-2xl font-bold text-heading">{number(Number(value))}</dd>
                </div>
              ))}
            </dl>
            <Link to="/goals" className="button button-secondary button-md mt-5 w-full">
              Xem mục tiêu đọc
            </Link>
          </section>

          <section className="rounded-2xl border border-accent/20 bg-accent-soft p-5 sm:p-6">
            <TrendUp size={24} weight="duotone" className="text-accent-strong" aria-hidden />
            <h2 className="mt-5 text-lg font-bold text-heading">Một nhịp đọc bền vững</h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Trung bình mỗi ngày có đọc, bạn hoàn thành {number(data.averagePagesPerActiveDay, 1)} trang
              trong {number(data.averageSessionsPerActiveDay, 1)} phiên. Dữ liệu chỉ được tính từ nhật ký
              của chính bạn.
            </p>
          </section>

          <section className="surface p-5 sm:p-6">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="eyebrow">Dự báo mục tiêu</p>
                <h2 className="mt-3 font-bold text-heading">Bạn có đang đúng tiến độ?</h2>
              </div>
              <Flag size={24} weight="duotone" className="text-accent-strong" aria-hidden />
            </div>
            {data.goalForecasts.length ? (
              <div className="mt-5 space-y-4">
                {data.goalForecasts.map((goal) => {
                  const metricLabels = {
                    BOOKS: { label: 'Sách', unit: 'cuốn' },
                    PAGES: { label: 'Trang', unit: 'trang' },
                    MINUTES: { label: 'Thời gian', unit: 'phút' },
                  } as const
                  const metric = metricLabels[goal.metric]
                  const progress = goal.targetValue > 0 ? (goal.currentValue / goal.targetValue) * 100 : 0
                  return (
                    <article key={goal.goalId} className="rounded-xl border border-border p-4">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <h3 className="text-sm font-semibold text-heading">{metric.label}</h3>
                        <span
                          className={`rounded-full px-2 py-1 text-[11px] font-bold ${
                            goal.isOnTrack === true
                              ? 'bg-accent-soft text-accent-strong'
                              : goal.isOnTrack === false
                                ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/35 dark:text-amber-300'
                                : 'bg-surface-muted text-muted'
                          }`}
                        >
                          {goal.isOnTrack === true
                            ? 'Đúng tiến độ'
                            : goal.isOnTrack === false
                              ? 'Cần tăng nhịp'
                              : 'Đang thu thập dữ liệu'}
                        </span>
                      </div>
                      <p className="mt-3 text-lg font-bold text-heading">
                        {number(goal.currentValue)}/{number(goal.targetValue)}{' '}
                        <span className="text-xs font-medium text-muted">{metric.unit}</span>
                      </p>
                      <Progress value={progress} className="mt-3" />
                      <p className="mt-3 text-xs leading-5 text-muted">
                        Nhịp {number(goal.averagePerDay, 1)} {metric.unit}/ngày · còn{' '}
                        {number(goal.remainingValue)} {metric.unit}
                      </p>
                      <p className="mt-1 text-xs font-semibold text-accent-strong">
                        {goal.estimatedFinishDate
                          ? `Dự kiến hoàn thành ${formatDateOnly(goal.estimatedFinishDate)}`
                          : `Hạn mục tiêu ${formatDate(goal.endDate)}`}
                      </p>
                    </article>
                  )
                })}
              </div>
            ) : (
              <p className="mt-5 rounded-xl bg-surface-muted p-4 text-sm leading-6 text-muted">
                Chưa có mục tiêu đang hoạt động để dự báo.{' '}
                <Link to="/goals" className="font-semibold text-accent-strong hover:underline">
                  Tạo mục tiêu
                </Link>
              </p>
            )}
          </section>
        </aside>
      </div>
    </div>
  )
}
