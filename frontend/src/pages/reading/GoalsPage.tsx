import {
  CalendarBlank,
  CheckCircle,
  Flag,
  PencilSimple,
  Plus,
  Trash,
  X,
} from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { InputField, SelectField } from '../../components/ui/FormField'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import {
  useCreateReadingGoal,
  useDeleteReadingGoal,
  useReadingGoals,
  useUpdateReadingGoal,
} from '../../hooks/useReadingProduct'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { ReadingGoal, ReadingGoalMetric, ReadingGoalPeriod, ReadingGoalStatus } from '../../types/domain'

interface GoalFormState {
  metric: ReadingGoalMetric
  period: ReadingGoalPeriod
  targetValue: string
  startDate: string
  endDate: string
}

const metricLabels: Record<ReadingGoalMetric, string> = {
  BOOKS: 'Cuốn sách',
  PAGES: 'Trang sách',
  MINUTES: 'Phút đọc',
}

const metricUnits: Record<ReadingGoalMetric, string> = {
  BOOKS: 'cuốn',
  PAGES: 'trang',
  MINUTES: 'phút',
}

const periodLabels: Record<ReadingGoalPeriod, string> = {
  WEEK: 'Theo tuần',
  MONTH: 'Theo tháng',
  YEAR: 'Theo năm',
  CUSTOM: 'Khoảng tùy chọn',
}

const statusLabels: Record<ReadingGoalStatus, string> = {
  ACTIVE: 'Đang thực hiện',
  COMPLETED: 'Đã hoàn thành',
  EXPIRED: 'Đã hết hạn',
}

function localDateValue(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function rangeForPeriod(period: ReadingGoalPeriod) {
  const today = new Date()
  const year = today.getFullYear()
  const month = today.getMonth()

  if (period === 'WEEK') {
    const day = today.getDay() || 7
    const start = new Date(year, month, today.getDate() - day + 1)
    const end = new Date(year, month, start.getDate() + 6)
    return { startDate: localDateValue(start), endDate: localDateValue(end) }
  }

  if (period === 'MONTH') {
    return {
      startDate: localDateValue(new Date(year, month, 1)),
      endDate: localDateValue(new Date(year, month + 1, 0)),
    }
  }

  if (period === 'YEAR') {
    return {
      startDate: localDateValue(new Date(year, 0, 1)),
      endDate: localDateValue(new Date(year, 11, 31)),
    }
  }

  const end = new Date(year, month, today.getDate() + 30)
  return { startDate: localDateValue(today), endDate: localDateValue(end) }
}

function createGoalForm(period: ReadingGoalPeriod = 'MONTH'): GoalFormState {
  return {
    metric: 'BOOKS',
    period,
    targetValue: '',
    ...rangeForPeriod(period),
  }
}

function goalFormFromGoal(goal: ReadingGoal): GoalFormState {
  return {
    metric: goal.metric,
    period: goal.period,
    targetValue: String(goal.targetValue),
    startDate: goal.startDate.slice(0, 10),
    endDate: goal.endDate.slice(0, 10),
  }
}

function statusClass(status: ReadingGoalStatus) {
  if (status === 'COMPLETED') return 'bg-accent-soft text-accent-strong'
  if (status === 'EXPIRED') return 'bg-surface-muted text-muted'
  return 'bg-heading text-page'
}

export function GoalsPage() {
  const goalsQuery = useReadingGoals()
  const createGoal = useCreateReadingGoal()
  const updateGoal = useUpdateReadingGoal()
  const deleteGoal = useDeleteReadingGoal()
  const { showToast } = useToast()
  const [showForm, setShowForm] = useState(false)
  const [editingGoal, setEditingGoal] = useState<ReadingGoal | null>(null)
  const [form, setForm] = useState<GoalFormState>(() => createGoalForm())
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [statusFilter, setStatusFilter] = useState<'ALL' | ReadingGoalStatus>('ALL')
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const goals = goalsQuery.data?.items ?? []
  const visibleGoals = goals.filter((goal) => statusFilter === 'ALL' || goal.status === statusFilter)
  const activeGoals = goals.filter((goal) => goal.status === 'ACTIVE')
  const completedGoals = goals.filter((goal) => goal.status === 'COMPLETED')

  const resetForm = () => {
    setShowForm(false)
    setEditingGoal(null)
    setForm(createGoalForm())
    setErrors({})
  }

  const startCreating = () => {
    setEditingGoal(null)
    setForm(createGoalForm())
    setErrors({})
    setShowForm(true)
  }

  const startEditing = (goal: ReadingGoal) => {
    setEditingGoal(goal)
    setForm(goalFormFromGoal(goal))
    setErrors({})
    setShowForm(true)
    setPendingDeleteId(null)
  }

  const changePeriod = (period: ReadingGoalPeriod) => {
    setForm((current) => ({ ...current, period, ...rangeForPeriod(period) }))
    setErrors({})
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const targetValue = Number(form.targetValue)
    const nextErrors: Record<string, string> = {}

    if (!Number.isInteger(targetValue) || targetValue < 1) {
      nextErrors.targetValue = 'Nhập một mục tiêu là số nguyên lớn hơn 0.'
    }
    if (!form.startDate) nextErrors.startDate = 'Chọn ngày bắt đầu.'
    if (!form.endDate) nextErrors.endDate = 'Chọn ngày kết thúc.'
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      nextErrors.endDate = 'Ngày kết thúc cần sau hoặc trùng ngày bắt đầu.'
    }

    if (Object.keys(nextErrors).length) {
      setErrors(nextErrors)
      return
    }

    const input = {
      metric: form.metric,
      period: form.period,
      targetValue,
      startDate: new Date(`${form.startDate}T00:00:00`).toISOString(),
      endDate: new Date(`${form.endDate}T23:59:59`).toISOString(),
    }

    try {
      if (editingGoal) {
        await updateGoal.mutateAsync({ id: editingGoal.id, input })
        showToast('Đã cập nhật mục tiêu đọc', 'success')
      } else {
        await createGoal.mutateAsync(input)
        showToast('Đã tạo mục tiêu đọc', 'success')
      }
      resetForm()
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu mục tiêu đọc'), 'error')
    }
  }

  const removeGoal = async (id: string) => {
    try {
      await deleteGoal.mutateAsync(id)
      setPendingDeleteId(null)
      if (editingGoal?.id === id) resetForm()
      showToast('Đã xóa mục tiêu đọc', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa mục tiêu đọc'), 'error')
    }
  }

  return (
    <div className="container-page section-space max-w-6xl">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Nhịp đọc có chủ đích</p>
          <h1 className="page-title mt-4">Mục tiêu đọc</h1>
          <p className="mt-3 max-w-2xl text-muted">
            Đặt một cột mốc rõ ràng, để mỗi phiên đọc nhỏ đều có hướng đi.
          </p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link to="/journal" className="button button-secondary button-md">
            Mở nhật ký
          </Link>
          <Button icon={<Plus size={18} />} onClick={startCreating}>
            Thêm mục tiêu
          </Button>
        </div>
      </div>

      <section className="mt-9 grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-3">
        {[
          { label: 'Đang theo đuổi', value: activeGoals.length },
          { label: 'Đã hoàn thành', value: completedGoals.length },
          {
            label: 'Tiến độ trung bình',
            value: activeGoals.length
              ? `${Math.round(activeGoals.reduce((sum, goal) => sum + goal.progressPercent, 0) / activeGoals.length)}%`
              : '—',
          },
        ].map(({ label, value }) => (
          <div key={label} className="bg-surface p-5 sm:p-6">
            <p className="text-2xl font-bold tracking-tight text-heading">{value}</p>
            <p className="mt-1 text-sm text-muted">{label}</p>
          </div>
        ))}
      </section>

      {showForm ? (
        <section className="mt-8 surface p-5 sm:p-7">
          <div className="flex items-start justify-between gap-5">
            <div>
              <h2 className="text-xl font-bold text-heading">
                {editingGoal ? 'Điều chỉnh mục tiêu' : 'Mục tiêu mới'}
              </h2>
              <p className="mt-1 text-sm text-muted">
                Tiến độ sẽ được tính từ hoạt động đọc của bạn trong khoảng thời gian đã chọn.
              </p>
            </div>
            <button type="button" className="icon-button" onClick={resetForm} aria-label="Đóng biểu mẫu mục tiêu">
              <X size={18} />
            </button>
          </div>

          <form onSubmit={submit} className="mt-6 grid gap-5 md:grid-cols-2 xl:grid-cols-4">
            <SelectField
              label="Theo dõi"
              name="metric"
              value={form.metric}
              onChange={(event) => {
                setForm({ ...form, metric: event.target.value as ReadingGoalMetric })
                setErrors({})
              }}
            >
              {Object.entries(metricLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="Chu kỳ"
              name="period"
              value={form.period}
              onChange={(event) => changePeriod(event.target.value as ReadingGoalPeriod)}
            >
              {Object.entries(periodLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SelectField>
            <InputField
              label={`Mục tiêu (${metricUnits[form.metric]})`}
              name="targetValue"
              type="number"
              min={1}
              step={1}
              inputMode="numeric"
              value={form.targetValue}
              error={errors.targetValue}
              onChange={(event) => {
                setForm({ ...form, targetValue: event.target.value })
                setErrors({ ...errors, targetValue: '' })
              }}
              required
            />
            <div className="hidden xl:block" aria-hidden />
            <InputField
              label="Bắt đầu"
              name="startDate"
              type="date"
              value={form.startDate}
              error={errors.startDate}
              onChange={(event) => {
                setForm({ ...form, startDate: event.target.value })
                setErrors({ ...errors, startDate: '' })
              }}
              required
            />
            <InputField
              label="Kết thúc"
              name="endDate"
              type="date"
              value={form.endDate}
              error={errors.endDate}
              onChange={(event) => {
                setForm({ ...form, endDate: event.target.value })
                setErrors({ ...errors, endDate: '' })
              }}
              required
            />
            <div className="flex items-end gap-3 md:col-span-2 xl:col-span-2">
              <Button type="submit" loading={createGoal.isPending || updateGoal.isPending} icon={<Flag size={18} />}>
                {editingGoal ? 'Lưu thay đổi' : 'Tạo mục tiêu'}
              </Button>
              <Button type="button" variant="ghost" onClick={resetForm}>
                Hủy
              </Button>
            </div>
          </form>
        </section>
      ) : null}

      <section className="mt-10">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-heading">Mục tiêu của bạn</h2>
            <p className="mt-1 text-sm text-muted">Nhìn lại điều đang tiến triển và những mốc đã qua.</p>
          </div>
          <div className="flex flex-wrap gap-2" aria-label="Lọc mục tiêu theo trạng thái">
            {(
              [
                ['ALL', 'Tất cả'],
                ['ACTIVE', 'Đang làm'],
                ['COMPLETED', 'Hoàn thành'],
                ['EXPIRED', 'Hết hạn'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                type="button"
                onClick={() => setStatusFilter(value)}
                className={`rounded-full px-3 py-1.5 text-sm font-semibold transition-colors focus-visible:focus-ring ${
                  statusFilter === value
                    ? 'bg-accent text-white'
                    : 'bg-surface-muted text-muted hover:bg-accent-soft hover:text-accent-strong'
                }`}
                aria-pressed={statusFilter === value}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className="mt-5">
          {goalsQuery.isLoading ? (
            <LoadingRows count={4} />
          ) : goalsQuery.isError ? (
            <ErrorState message="Không thể tải các mục tiêu đọc." retry={() => void goalsQuery.refetch()} />
          ) : visibleGoals.length ? (
            <div className="grid gap-4 lg:grid-cols-2">
              {visibleGoals.map((goal) => (
                <article key={goal.id} className="surface p-5 sm:p-6">
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="flex items-start gap-3">
                      <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                        <Flag size={22} weight="duotone" />
                      </div>
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="font-semibold text-heading">{metricLabels[goal.metric]}</h3>
                          <span className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${statusClass(goal.status)}`}>
                            {statusLabels[goal.status]}
                          </span>
                        </div>
                        <p className="mt-1 text-sm text-muted">
                          {periodLabels[goal.period]} · {formatDate(goal.startDate)} — {formatDate(goal.endDate)}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-1">
                      {goal.status === 'ACTIVE' ? (
                        <button
                          type="button"
                          className="icon-button"
                          onClick={() => startEditing(goal)}
                          aria-label={`Sửa mục tiêu ${metricLabels[goal.metric]}`}
                        >
                          <PencilSimple size={18} />
                        </button>
                      ) : null}
                      <button
                        type="button"
                        className="icon-button text-red-700 hover:bg-red-50 dark:text-red-300 dark:hover:bg-red-950/30"
                        onClick={() => setPendingDeleteId(goal.id)}
                        aria-label={`Xóa mục tiêu ${metricLabels[goal.metric]}`}
                      >
                        <Trash size={18} />
                      </button>
                    </div>
                  </div>

                  <div className="mt-7 flex items-end justify-between gap-4">
                    <p className="text-3xl font-bold tracking-tight text-heading">
                      {goal.currentValue.toLocaleString('vi-VN')}
                      <span className="ml-1 text-base font-medium text-muted">/ {goal.targetValue.toLocaleString('vi-VN')} {metricUnits[goal.metric]}</span>
                    </p>
                    <span className="text-sm font-semibold text-accent-strong">{Math.round(goal.progressPercent)}%</span>
                  </div>
                  <Progress value={goal.progressPercent} className="mt-4" />

                  {goal.status === 'COMPLETED' && goal.completedAt ? (
                    <p className="mt-4 flex items-center gap-2 text-sm text-accent-strong">
                      <CheckCircle size={17} weight="fill" />
                      Hoàn thành vào {formatDate(goal.completedAt)}
                    </p>
                  ) : null}

                  {pendingDeleteId === goal.id ? (
                    <div className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-surface-muted p-3">
                      <p className="text-sm text-heading">Xóa mục tiêu này? Hành động không thể hoàn tác.</p>
                      <div className="flex gap-2">
                        <Button type="button" variant="ghost" size="sm" onClick={() => setPendingDeleteId(null)}>
                          Hủy
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          size="sm"
                          loading={deleteGoal.isPending}
                          onClick={() => void removeGoal(goal.id)}
                        >
                          Xóa mục tiêu
                        </Button>
                      </div>
                    </div>
                  ) : null}
                </article>
              ))}
            </div>
          ) : (
            <EmptyState
              icon={CalendarBlank}
              title={statusFilter === 'ALL' ? 'Chưa có mục tiêu đọc' : 'Không có mục tiêu ở trạng thái này'}
              description={
                statusFilter === 'ALL'
                  ? 'Bắt đầu với một cột mốc nhỏ để xây dựng nhịp đọc bền vững.'
                  : 'Thử đổi bộ lọc hoặc tạo một mục tiêu mới cho khoảng thời gian tiếp theo.'
              }
              action={statusFilter === 'ALL' ? <Button onClick={startCreating}>Tạo mục tiêu đầu tiên</Button> : undefined}
            />
          )}
        </div>
      </section>
    </div>
  )
}
