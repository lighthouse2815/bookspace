import {
  ArrowLeft,
  BellRinging,
  CalendarBlank,
  CheckCircle,
  ClockCounterClockwise,
  Medal,
  NotePencil,
  PencilSimple,
  Prohibit,
  SignIn,
  SignOut,
  Trophy,
  UsersThree,
  X,
} from '@phosphor-icons/react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { BookCover } from '../../components/books/BookCover'
import { ReadingSprintMilestones } from '../../components/clubs/ReadingSprintMilestones'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { InputField, SelectField, TextareaField } from '../../components/ui/FormField'
import { Pagination } from '../../components/ui/Pagination'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useCancelReadingSprint,
  useCheckInReadingSprint,
  useCompleteReadingSprint,
  useJoinReadingSprint,
  useLeaveReadingSprint,
  useReadingSprint,
  useReadingSprintLeaderboard,
  useReadingSprintTimeline,
  useSendReadingSprintReminder,
  useUpdateReadingSprint,
} from '../../hooks/useReadingSprints'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import {
  formatReadingSprintDateTime,
  readingSprintStatusClass,
  readingSprintStatusLabels,
  readingSprintUnitLabels,
  readingSprintUnitNames,
  toDateTimeLocal,
} from '../../lib/reading-sprint'
import type {
  ReadingSprintDetail,
  ReadingSprintTargetUnit,
} from '../../types/domain'

function isTerminal(sprint: ReadingSprintDetail) {
  return sprint.status === 'COMPLETED' || sprint.status === 'CANCELLED'
}

function SprintParticipationPanel({ sprint }: { sprint: ReadingSprintDetail }) {
  const { isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const location = useLocation()
  const joinSprint = useJoinReadingSprint(sprint.clubId, sprint.id)
  const leaveSprint = useLeaveReadingSprint(sprint.clubId, sprint.id)
  const checkIn = useCheckInReadingSprint(sprint.clubId, sprint.id)
  const [confirmLeave, setConfirmLeave] = useState(false)
  const [progressValue, setProgressValue] = useState(
    String(
      Math.min(
        (sprint.viewerParticipation?.progressValue ?? 0) + 1,
        sprint.targetValue,
      ),
    ),
  )
  const [note, setNote] = useState('')
  const [progressError, setProgressError] = useState('')
  const participation = sprint.viewerParticipation
  const currentProgress = participation?.progressValue ?? 0

  useEffect(() => {
    setProgressValue(
      String(
        Math.min(
          (sprint.viewerParticipation?.progressValue ?? 0) + 1,
          sprint.targetValue,
        ),
      ),
    )
  }, [
    sprint.targetValue,
    sprint.viewerParticipation?.id,
    sprint.viewerParticipation?.progressValue,
  ])

  const join = async () => {
    try {
      await joinSprint.mutateAsync()
      showToast('Bạn đã tham gia đợt đọc', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể tham gia đợt đọc'), 'error')
    }
  }

  const leave = async () => {
    try {
      await leaveSprint.mutateAsync()
      setConfirmLeave(false)
      showToast('Bạn đã rời đợt đọc', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể rời đợt đọc'), 'error')
    }
  }

  const submitProgress = async (event: FormEvent) => {
    event.preventDefault()
    const value = Number(progressValue)
    if (!Number.isInteger(value)) {
      setProgressError('Tiến độ phải là số nguyên.')
      return
    }
    if (value < currentProgress) {
      setProgressError(`Tiến độ mới không thể thấp hơn ${currentProgress}.`)
      return
    }
    if (value === currentProgress) {
      setProgressError('Hãy tăng tiến độ ít nhất 1 đơn vị để tạo check-in mới.')
      return
    }
    if (value > sprint.targetValue) {
      setProgressError(`Tiến độ không thể vượt mục tiêu ${sprint.targetValue}.`)
      return
    }
    if (note.trim().length > 1000) {
      setProgressError('Ghi chú không được vượt quá 1.000 ký tự.')
      return
    }

    try {
      await checkIn.mutateAsync({
        progressValue: value,
        note: note.trim() || null,
      })
      setNote('')
      setProgressError('')
      showToast('Đã ghi nhận tiến độ của bạn', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật tiến độ'), 'error')
    }
  }

  return (
    <section className="surface p-5 sm:p-6" aria-labelledby="participation-heading">
      <div>
        <p className="eyebrow">Nhịp của bạn</p>
        <h2 id="participation-heading" className="mt-2 text-xl font-bold text-heading">
          Tham gia đợt đọc
        </h2>
      </div>

      {participation?.isActive ? (
        <>
          <div className="mt-5 rounded-xl bg-accent-soft p-4">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <div>
                <p className="text-sm font-semibold text-accent-strong">Tiến độ hiện tại</p>
                <p className="mt-1 text-2xl font-bold text-heading">
                  {participation.progressValue}
                  <span className="ml-1 text-sm font-medium text-muted">
                    / {sprint.targetValue} {readingSprintUnitNames[sprint.targetUnit]}
                  </span>
                </p>
              </div>
              <span className="text-sm font-bold text-accent-strong">
                {Math.round(participation.progressPercent)}%
              </span>
            </div>
            <Progress value={participation.progressPercent} className="mt-3" />
          </div>

          {sprint.permissions.canCheckIn &&
          participation.progressValue < sprint.targetValue ? (
            <form onSubmit={submitProgress} className="mt-5" noValidate>
              <div className="grid gap-4 sm:grid-cols-[11rem_minmax(0,1fr)]">
                <InputField
                  label={`Đã đọc (${readingSprintUnitNames[sprint.targetUnit]})`}
                  name="sprint-progress"
                  type="number"
                  min={currentProgress + 1}
                  max={sprint.targetValue}
                  step={1}
                  inputMode="numeric"
                  value={progressValue}
                  error={progressError}
                  onChange={(event) => {
                    setProgressValue(event.target.value)
                    setProgressError('')
                  }}
                  required
                />
                <TextareaField
                  label="Ghi chú check-in"
                  name="sprint-progress-note"
                  className="min-h-20"
                  value={note}
                  maxLength={1000}
                  hint={`${note.length}/1.000 ký tự · Không bắt buộc`}
                  placeholder="Một ý hay, cảm nhận ngắn hoặc điều bạn muốn nhớ."
                  onChange={(event) => setNote(event.target.value)}
                />
              </div>
              <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
                <p className="text-xs leading-5 text-muted">
                  Tiến độ là số tuyệt đối và phải tăng ít nhất 1 đơn vị.
                </p>
                <Button
                  type="submit"
                  loading={checkIn.isPending}
                  icon={<NotePencil size={17} />}
                >
                  Lưu check-in
                </Button>
              </div>
            </form>
          ) : (
            <p className="mt-4 rounded-xl bg-surface-muted p-4 text-sm leading-6 text-muted">
              {participation.progressValue >= sprint.targetValue
                ? 'Bạn đã hoàn thành mục tiêu của đợt đọc. Thành tích vẫn được giữ trên bảng xếp hạng.'
                : sprint.status === 'PLANNED'
                ? 'Check-in sẽ mở khi đợt đọc bắt đầu.'
                : 'Đợt đọc đã đóng check-in. Tiến độ cuối cùng của bạn vẫn được lưu trong bảng xếp hạng.'}
            </p>
          )}

          {sprint.permissions.canLeave ? (
            <div className="mt-5 border-t border-border pt-4">
              {confirmLeave ? (
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-surface-muted p-3">
                  <p className="text-sm text-heading">
                    Rời đợt đọc? Lịch sử check-in của bạn vẫn được bảo toàn.
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setConfirmLeave(false)}
                    >
                      Ở lại
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      loading={leaveSprint.isPending}
                      onClick={() => void leave()}
                    >
                      Xác nhận rời
                    </Button>
                  </div>
                </div>
              ) : (
                <Button
                  variant="ghost"
                  size="sm"
                  icon={<SignOut size={16} />}
                  onClick={() => setConfirmLeave(true)}
                >
                  Rời đợt đọc
                </Button>
              )}
            </div>
          ) : null}
        </>
      ) : sprint.permissions.canJoin ? (
        <div className="mt-5">
          <p className="text-sm leading-6 text-muted">
            Tham gia để ghi tiến độ, xuất hiện trên bảng xếp hạng và phản hồi tại từng cột mốc.
          </p>
          <Button
            className="mt-4 w-full"
            loading={joinSprint.isPending}
            icon={<SignIn size={18} />}
            onClick={() => void join()}
          >
            Tham gia đợt đọc
          </Button>
        </div>
      ) : !isAuthenticated && !isTerminal(sprint) ? (
        <div className="mt-5">
          <p className="text-sm leading-6 text-muted">
            Đăng nhập bằng tài khoản BookSpace để tham gia và đồng bộ tiến độ của bạn.
          </p>
          <Link
            to="/login"
            state={{ from: location.pathname }}
            className="button button-primary button-md mt-4 w-full"
          >
            <SignIn size={18} />
            Đăng nhập để tham gia
          </Link>
        </div>
      ) : (
        <div className="mt-5 rounded-xl bg-surface-muted p-4">
          <p className="text-sm leading-6 text-muted">
            {isTerminal(sprint)
              ? 'Đợt đọc này đã khép lại và hiện chỉ còn ở chế độ xem lại.'
              : 'Bạn cần là thành viên của câu lạc bộ để tham gia đợt đọc này.'}
          </p>
        </div>
      )}
    </section>
  )
}

interface EditSprintForm {
  title: string
  description: string
  startsAt: string
  endsAt: string
  targetUnit: ReadingSprintTargetUnit
  targetValue: string
}

function editFormFromSprint(sprint: ReadingSprintDetail): EditSprintForm {
  return {
    title: sprint.title,
    description: sprint.description ?? '',
    startsAt: toDateTimeLocal(sprint.startsAt),
    endsAt: toDateTimeLocal(sprint.endsAt),
    targetUnit: sprint.targetUnit,
    targetValue: String(sprint.targetValue),
  }
}

type TerminalAction = 'COMPLETE' | 'CANCEL' | null

function SprintManagerPanel({ sprint }: { sprint: ReadingSprintDetail }) {
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<EditSprintForm>(() => editFormFromSprint(sprint))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [confirmAction, setConfirmAction] = useState<TerminalAction>(null)
  const updateSprint = useUpdateReadingSprint(sprint.clubId, sprint.id)
  const sendReminder = useSendReadingSprintReminder(sprint.clubId, sprint.id)
  const completeSprint = useCompleteReadingSprint(sprint.clubId, sprint.id)
  const cancelSprint = useCancelReadingSprint(sprint.clubId, sprint.id)
  const { showToast } = useToast()

  useEffect(() => {
    if (!editing) setForm(editFormFromSprint(sprint))
  }, [editing, sprint])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const targetValue = Number(form.targetValue)
    const nextErrors: Record<string, string> = {}
    if (!form.title.trim()) {
      nextErrors.title = 'Tên đợt đọc không được để trống.'
    } else if (form.title.trim().length > 200) {
      nextErrors.title = 'Tên đợt đọc không được vượt quá 200 ký tự.'
    }
    if (form.description.trim().length > 2000) {
      nextErrors.description = 'Mô tả không được vượt quá 2.000 ký tự.'
    }
    if (!form.startsAt) nextErrors.startsAt = 'Chọn thời gian bắt đầu.'
    if (!form.endsAt) nextErrors.endsAt = 'Chọn thời gian kết thúc.'
    if (form.startsAt && form.endsAt && form.endsAt <= form.startsAt) {
      nextErrors.endsAt = 'Thời gian kết thúc phải sau thời gian bắt đầu.'
    } else if (form.endsAt && new Date(form.endsAt).getTime() <= Date.now()) {
      nextErrors.endsAt = 'Thời gian kết thúc phải ở tương lai.'
    }
    if (!Number.isInteger(targetValue) || targetValue < 1) {
      nextErrors.targetValue = 'Mục tiêu phải là số nguyên lớn hơn 0.'
    } else if (form.targetUnit === 'CHAPTERS' && targetValue > 500) {
      nextErrors.targetValue = 'Mục tiêu theo chương không được vượt quá 500.'
    } else if (
      form.targetUnit === 'PAGES' &&
      sprint.book.pageCount &&
      targetValue > sprint.book.pageCount
    ) {
      nextErrors.targetValue = `Sách này có ${sprint.book.pageCount} trang.`
    }
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return

    try {
      await updateSprint.mutateAsync({
        bookId: sprint.book.id,
        title: form.title.trim(),
        description: form.description.trim() || null,
        startsAt: new Date(form.startsAt).toISOString(),
        endsAt: new Date(form.endsAt).toISOString(),
        targetUnit: form.targetUnit,
        targetValue,
      })
      setEditing(false)
      showToast('Đã cập nhật đợt đọc', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật đợt đọc'), 'error')
    }
  }

  const remind = async () => {
    try {
      await sendReminder.mutateAsync()
      showToast('Đã gửi nhắc tiến độ đến thành viên', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gửi lời nhắc'), 'error')
    }
  }

  const runTerminalAction = async () => {
    try {
      if (confirmAction === 'COMPLETE') {
        await completeSprint.mutateAsync()
        showToast('Đã tổng kết đợt đọc', 'success')
      } else if (confirmAction === 'CANCEL') {
        await cancelSprint.mutateAsync()
        showToast('Đã hủy đợt đọc', 'success')
      }
      setConfirmAction(null)
    } catch (error) {
      showToast(
        errorMessage(
          error,
          confirmAction === 'COMPLETE'
            ? 'Không thể tổng kết đợt đọc'
            : 'Không thể hủy đợt đọc',
        ),
        'error',
      )
    }
  }

  if (!sprint.permissions.canManage && !sprint.permissions.canSendReminder) return null

  return (
    <section className="mt-6 surface p-5 sm:p-6" aria-labelledby="sprint-manager-heading">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="eyebrow">Dành cho quản lý</p>
          <h2 id="sprint-manager-heading" className="mt-2 text-xl font-bold text-heading">
            Điều phối đợt đọc
          </h2>
          {sprint.lastReminderAt ? (
            <p className="mt-2 text-xs text-muted">
              Nhắc tiến độ gần nhất {formatRelativeTime(sprint.lastReminderAt)}.
            </p>
          ) : null}
        </div>
        {sprint.permissions.canManage && sprint.status === 'PLANNED' ? (
          <Button
            variant="secondary"
            size="sm"
            icon={editing ? <X size={17} /> : <PencilSimple size={17} />}
            onClick={() => {
              setEditing((value) => !value)
              setErrors({})
            }}
          >
            {editing ? 'Đóng chỉnh sửa' : 'Sửa thông tin'}
          </Button>
        ) : null}
      </div>

      {editing ? (
        <form onSubmit={submit} className="mt-5 grid gap-5 md:grid-cols-2" noValidate>
          <InputField
            label="Tên đợt đọc"
            name="edit-sprint-title"
            value={form.title}
            maxLength={200}
            error={errors.title}
            onChange={(event) => {
              setForm({ ...form, title: event.target.value })
              setErrors({ ...errors, title: '' })
            }}
            required
          />
          <div className="grid grid-cols-[minmax(0,1fr)_minmax(7rem,0.7fr)] gap-3">
            <SelectField
              label="Đơn vị"
              name="edit-sprint-unit"
              value={form.targetUnit}
              onChange={(event) =>
                setForm({
                  ...form,
                  targetUnit: event.target.value as ReadingSprintTargetUnit,
                })
              }
            >
              <option value="PAGES">Trang</option>
              <option value="CHAPTERS">Chương</option>
            </SelectField>
            <InputField
              label="Mục tiêu"
              name="edit-sprint-target"
              type="number"
              min={1}
              max={
                form.targetUnit === 'CHAPTERS'
                  ? 500
                  : sprint.book.pageCount
              }
              step={1}
              value={form.targetValue}
              error={errors.targetValue}
              onChange={(event) => {
                setForm({ ...form, targetValue: event.target.value })
                setErrors({ ...errors, targetValue: '' })
              }}
              required
            />
          </div>
          <InputField
            label="Bắt đầu"
            name="edit-sprint-start"
            type="datetime-local"
            value={form.startsAt}
            error={errors.startsAt}
            onChange={(event) => {
              setForm({ ...form, startsAt: event.target.value })
              setErrors({ ...errors, startsAt: '' })
            }}
            required
          />
          <InputField
            label="Kết thúc"
            name="edit-sprint-end"
            type="datetime-local"
            value={form.endsAt}
            error={errors.endsAt}
            onChange={(event) => {
              setForm({ ...form, endsAt: event.target.value })
              setErrors({ ...errors, endsAt: '' })
            }}
            required
          />
          <TextareaField
            label="Mô tả"
            name="edit-sprint-description"
            value={form.description}
            maxLength={2000}
            error={errors.description}
            hint={`${form.description.length}/2.000 ký tự`}
            onChange={(event) => {
              setForm({ ...form, description: event.target.value })
              setErrors({ ...errors, description: '' })
            }}
          />
          <div className="flex items-end gap-3">
            <Button type="submit" loading={updateSprint.isPending}>
              Lưu thay đổi
            </Button>
            <Button type="button" variant="ghost" onClick={() => setEditing(false)}>
              Hủy
            </Button>
          </div>
        </form>
      ) : (
        <div className="mt-5 flex flex-wrap gap-2">
          {sprint.permissions.canSendReminder ? (
            <Button
              variant="secondary"
              size="sm"
              loading={sendReminder.isPending}
              icon={<BellRinging size={17} />}
              onClick={() => void remind()}
            >
              Nhắc tiến độ
            </Button>
          ) : null}
          {sprint.permissions.canManage && !isTerminal(sprint) ? (
            <>
              {sprint.status === 'ACTIVE' || sprint.status === 'ENDED' ? (
                <Button
                  variant="secondary"
                  size="sm"
                  icon={<CheckCircle size={17} />}
                  onClick={() => setConfirmAction('COMPLETE')}
                >
                  Tổng kết
                </Button>
              ) : null}
              <Button
                variant="ghost"
                size="sm"
                className="text-red-700 dark:text-red-300"
                icon={<Prohibit size={17} />}
                onClick={() => setConfirmAction('CANCEL')}
              >
                Hủy đợt đọc
              </Button>
            </>
          ) : null}
        </div>
      )}

      {confirmAction ? (
        <div
          className={`mt-5 flex flex-wrap items-center justify-between gap-3 rounded-xl p-4 ${
            confirmAction === 'CANCEL'
              ? 'bg-red-50 dark:bg-red-950/25'
              : 'bg-accent-soft'
          }`}
        >
          <div>
            <p className="text-sm font-semibold text-heading">
              {confirmAction === 'COMPLETE'
                ? 'Tổng kết đợt đọc ngay bây giờ?'
                : 'Xác nhận hủy đợt đọc?'}
            </p>
            <p className="mt-1 text-xs leading-5 text-muted">
              {confirmAction === 'COMPLETE'
                ? 'Tiến độ sẽ được chốt và đợt đọc chuyển sang chế độ xem lại.'
                : 'Thành viên sẽ không thể check-in thêm. Hành động này không thể hoàn tác.'}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="ghost" size="sm" onClick={() => setConfirmAction(null)}>
              Quay lại
            </Button>
            <Button
              variant={confirmAction === 'CANCEL' ? 'danger' : 'primary'}
              size="sm"
              loading={completeSprint.isPending || cancelSprint.isPending}
              onClick={() => void runTerminalAction()}
            >
              {confirmAction === 'COMPLETE' ? 'Xác nhận tổng kết' : 'Xác nhận hủy'}
            </Button>
          </div>
        </div>
      ) : null}
    </section>
  )
}

function SprintLeaderboard({ sprint }: { sprint: ReadingSprintDetail }) {
  const [page, setPage] = useState(1)
  const pageSize = 10
  const leaderboard = useReadingSprintLeaderboard(
    sprint.clubId,
    sprint.id,
    page,
    pageSize,
  )
  const { user } = useAuth()
  const totalPages = Math.max(leaderboard.data?.totalPages ?? 1, 1)

  useEffect(() => {
    if (leaderboard.data && page > totalPages) setPage(totalPages)
  }, [leaderboard.data, page, totalPages])

  return (
    <section className="surface min-w-0 p-5 sm:p-6" aria-labelledby="leaderboard-heading">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="eyebrow">Cùng tiến lên</p>
          <h2 id="leaderboard-heading" className="mt-2 text-xl font-bold text-heading">
            Bảng xếp hạng
          </h2>
        </div>
        <Trophy size={28} weight="duotone" className="text-accent-strong" />
      </div>

      <div className="mt-5">
        {leaderboard.isLoading ? (
          <LoadingRows count={4} />
        ) : leaderboard.isError ? (
          <ErrorState
            message="Không thể tải bảng xếp hạng."
            retry={() => void leaderboard.refetch()}
          />
        ) : leaderboard.data?.items.length ? (
          <ol className="space-y-2">
            {leaderboard.data.items.map((participant) => (
              <li
                key={participant.id}
                className={`grid min-w-0 grid-cols-[2rem_2.5rem_minmax(0,1fr)] items-center gap-3 rounded-xl p-3 ${
                  participant.user.id === user?.id ? 'bg-accent-soft' : 'bg-surface-muted/55'
                }`}
              >
                <span
                  className={`grid h-8 w-8 place-items-center rounded-lg text-sm font-bold ${
                    participant.rank <= 3
                      ? 'bg-accent text-white'
                      : 'bg-surface text-muted'
                  }`}
                  aria-label={`Hạng ${participant.rank}`}
                >
                  {participant.rank <= 3 ? (
                    <Medal size={18} weight="fill" />
                  ) : (
                    participant.rank
                  )}
                </span>
                <Link to={`/users/${participant.user.id}`}>
                  <Avatar
                    src={participant.user.avatarUrl}
                    name={participant.user.displayName}
                  />
                </Link>
                <div className="min-w-0">
                  <div className="flex min-w-0 items-center justify-between gap-3">
                    <Link
                      to={`/users/${participant.user.id}`}
                      className="truncate text-sm font-semibold text-heading hover:text-accent-strong"
                    >
                      {participant.user.displayName}
                      {participant.user.id === user?.id ? ' · Bạn' : ''}
                    </Link>
                    <span className="shrink-0 text-xs font-bold text-accent-strong">
                      {participant.progressValue}/{sprint.targetValue}
                    </span>
                  </div>
                  <Progress value={participant.progressPercent} className="mt-2" />
                </div>
              </li>
            ))}
          </ol>
        ) : (
          <EmptyState
            icon={UsersThree}
            title="Chưa có người tham gia"
            description="Bảng xếp hạng sẽ xuất hiện khi thành viên bắt đầu đợt đọc."
          />
        )}
        <Pagination
          page={page}
          totalPages={totalPages}
          disabled={leaderboard.isFetching}
          onPageChange={setPage}
          className="mt-5 border-t border-border pt-4"
        />
      </div>
    </section>
  )
}

function SprintTimeline({ sprint }: { sprint: ReadingSprintDetail }) {
  const [page, setPage] = useState(1)
  const pageSize = 10
  const timeline = useReadingSprintTimeline(
    sprint.clubId,
    sprint.id,
    page,
    pageSize,
  )
  const totalPages = Math.max(timeline.data?.totalPages ?? 1, 1)

  useEffect(() => {
    if (timeline.data && page > totalPages) setPage(totalPages)
  }, [page, timeline.data, totalPages])

  return (
    <section className="surface min-w-0 p-5 sm:p-6" aria-labelledby="timeline-heading">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="eyebrow">Dòng chuyển động</p>
          <h2 id="timeline-heading" className="mt-2 text-xl font-bold text-heading">
            Check-in gần đây
          </h2>
        </div>
        <ClockCounterClockwise size={27} weight="duotone" className="text-accent-strong" />
      </div>

      <div className="mt-5">
        {timeline.isLoading ? (
          <LoadingRows count={4} />
        ) : timeline.isError ? (
          <ErrorState
            message="Không thể tải dòng check-in."
            retry={() => void timeline.refetch()}
          />
        ) : timeline.data?.items.length ? (
          <div className="space-y-4">
            {timeline.data.items.map((checkIn, index) => (
              <article
                key={checkIn.id}
                className="relative grid min-w-0 grid-cols-[2.5rem_minmax(0,1fr)] gap-3"
              >
                {index < timeline.data.items.length - 1 ? (
                  <span
                    className="absolute bottom-[-1rem] left-5 top-10 w-px bg-border"
                    aria-hidden
                  />
                ) : null}
                <Link to={`/users/${checkIn.user.id}`} className="relative z-[1]">
                  <Avatar
                    src={checkIn.user.avatarUrl}
                    name={checkIn.user.displayName}
                  />
                </Link>
                <div className="min-w-0 rounded-xl bg-surface-muted/60 p-3.5">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <Link
                      to={`/users/${checkIn.user.id}`}
                      className="text-sm font-semibold text-heading hover:text-accent-strong"
                    >
                      {checkIn.user.displayName}
                    </Link>
                    <span className="text-xs text-muted">
                      {formatRelativeTime(checkIn.createdAt)}
                    </span>
                  </div>
                  <p className="mt-1 text-sm font-semibold text-accent-strong">
                    {checkIn.progressValue}/{sprint.targetValue}{' '}
                    {readingSprintUnitNames[sprint.targetUnit]} ·{' '}
                    {Math.round(checkIn.progressPercent)}%
                  </p>
                  {checkIn.note ? (
                    <p className="mt-2 whitespace-pre-line break-words text-sm leading-6 text-body">
                      {checkIn.note}
                    </p>
                  ) : null}
                </div>
              </article>
            ))}
          </div>
        ) : (
          <EmptyState
            icon={ClockCounterClockwise}
            title="Chưa có check-in"
            description="Những lần cập nhật tiến độ đầu tiên sẽ xuất hiện tại đây."
          />
        )}
        <Pagination
          page={page}
          totalPages={totalPages}
          disabled={timeline.isFetching}
          onPageChange={setPage}
          className="mt-5 border-t border-border pt-4"
        />
      </div>
    </section>
  )
}

function SprintHero({ sprint }: { sprint: ReadingSprintDetail }) {
  const viewerProgress = sprint.viewerParticipation?.progressPercent
  const progress = viewerProgress ?? sprint.averageProgressPercent
  const progressLabel =
    viewerProgress !== undefined
      ? 'Tiến độ của bạn'
      : 'Tiến độ trung bình của nhóm'

  return (
    <section className="surface overflow-hidden">
      <div className="relative isolate overflow-hidden bg-[linear-gradient(135deg,var(--surface-muted),var(--surface))] p-5 sm:p-8 lg:p-10">
        <div
          className="absolute -right-24 -top-32 h-72 w-72 rounded-full bg-accent/12 blur-3xl"
          aria-hidden
        />
        <Link
          to={`/clubs/${sprint.clubId}`}
          className="relative inline-flex items-center gap-2 text-sm font-semibold text-muted hover:text-heading"
        >
          <ArrowLeft size={17} />
          Về câu lạc bộ
        </Link>
        <div className="relative mt-7 grid min-w-0 gap-7 sm:grid-cols-[9rem_minmax(0,1fr)] lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-10">
          <Link to={`/books/${sprint.book.id}`} className="w-fit">
            <BookCover
              src={sprint.book.coverImageUrl}
              title={sprint.book.title}
              className="aspect-[2/3] w-32 rounded-2xl shadow-cover sm:w-36 lg:w-44"
            />
          </Link>
          <div className="min-w-0 self-center">
            <div className="flex flex-wrap items-center gap-2">
              <span
                className={`rounded-full px-3 py-1.5 text-xs font-bold ${readingSprintStatusClass(sprint.status)}`}
              >
                {readingSprintStatusLabels[sprint.status]}
              </span>
              <span className="rounded-full bg-surface px-3 py-1.5 text-xs font-semibold text-muted">
                {readingSprintUnitLabels[sprint.targetUnit]} · {sprint.targetValue}
              </span>
            </div>
            <h1 className="mt-4 break-words text-3xl font-bold tracking-[-0.035em] text-heading sm:text-4xl lg:text-5xl">
              {sprint.title}
            </h1>
            <Link
              to={`/books/${sprint.book.id}`}
              className="mt-3 inline-block text-sm font-semibold text-accent-strong hover:underline"
            >
              {sprint.book.title}
              {sprint.book.author?.name ? ` · ${sprint.book.author.name}` : ''}
            </Link>
            {sprint.description ? (
              <p className="mt-4 max-w-3xl whitespace-pre-line break-words text-sm leading-7 text-muted sm:text-base">
                {sprint.description}
              </p>
            ) : null}
            <div className="mt-5 flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted">
              <span className="inline-flex items-center gap-2">
                <CalendarBlank size={17} />
                {formatReadingSprintDateTime(sprint.startsAt)} —{' '}
                {formatReadingSprintDateTime(sprint.endsAt)}
              </span>
              <span className="inline-flex items-center gap-2">
                <UsersThree size={17} />
                Tạo bởi {sprint.createdBy.displayName}
              </span>
            </div>
            <div className="mt-6 max-w-2xl">
              <Progress value={progress} label={progressLabel} />
            </div>
          </div>
        </div>
      </div>
      <div className="grid gap-px bg-border sm:grid-cols-3">
        {[
          {
            label: 'Người tham gia',
            value: sprint.participantCount.toLocaleString('vi-VN'),
          },
          {
            label: 'Đã hoàn thành',
            value: sprint.completedCount.toLocaleString('vi-VN'),
          },
          {
            label: 'Tiến độ trung bình',
            value: `${Math.round(sprint.averageProgressPercent)}%`,
          },
        ].map((metric) => (
          <div key={metric.label} className="bg-surface p-5 sm:p-6">
            <p className="text-2xl font-bold tracking-tight text-heading">{metric.value}</p>
            <p className="mt-1 text-sm text-muted">{metric.label}</p>
          </div>
        ))}
      </div>
    </section>
  )
}

export function ReadingSprintPage() {
  const { clubId = '', sprintId = '' } = useParams()
  const sprint = useReadingSprint(clubId, sprintId)

  if (sprint.isLoading) {
    return (
      <div className="container-page section-space">
        <LoadingRows count={6} />
      </div>
    )
  }

  if (sprint.isError || !sprint.data) {
    return (
      <div className="container-page section-space">
        <ErrorState
          message="Không thể tải đợt đọc. Đợt đọc có thể không tồn tại hoặc thuộc một câu lạc bộ riêng tư."
          retry={() => void sprint.refetch()}
        />
      </div>
    )
  }

  return (
    <div className="container-page section-space min-w-0">
      <SprintHero sprint={sprint.data} />

      <div className="mt-7 grid min-w-0 gap-6 lg:grid-cols-[minmax(0,1fr)_24rem] lg:items-start">
        <div className="min-w-0">
          <div className="grid min-w-0 gap-6 xl:grid-cols-2">
            <SprintLeaderboard sprint={sprint.data} />
            <SprintTimeline sprint={sprint.data} />
          </div>
        </div>
        <aside className="min-w-0 lg:sticky lg:top-24">
          <SprintParticipationPanel sprint={sprint.data} />
          <SprintManagerPanel sprint={sprint.data} />
        </aside>
      </div>

      <ReadingSprintMilestones sprint={sprint.data} />

      {sprint.data.status === 'COMPLETED' ? (
        <section className="mt-10 rounded-2xl bg-accent-soft p-6 text-center sm:p-8">
          <CheckCircle size={32} weight="fill" className="mx-auto text-accent-strong" />
          <h2 className="mt-3 text-xl font-bold text-heading">Một hành trình đã được lưu lại</h2>
          <p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-muted">
            Đợt đọc đã tổng kết
            {sprint.data.completedAt
              ? ` vào ${formatReadingSprintDateTime(sprint.data.completedAt)}`
              : ''}
            . Bảng xếp hạng, check-in và các cuộc thảo luận vẫn ở đây để cả nhóm nhìn lại.
          </p>
        </section>
      ) : sprint.data.status === 'CANCELLED' ? (
        <section className="mt-10 rounded-2xl bg-surface-muted p-6 text-center sm:p-8">
          <Prohibit size={30} className="mx-auto text-muted" />
          <h2 className="mt-3 text-xl font-bold text-heading">Đợt đọc đã dừng</h2>
          <p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-muted">
            Hoạt động mới đã đóng
            {sprint.data.cancelledAt
              ? ` từ ${formatReadingSprintDateTime(sprint.data.cancelledAt)}`
              : ''}
            , nhưng dữ liệu trước đó vẫn được giữ để thành viên tra cứu.
          </p>
        </section>
      ) : null}
    </div>
  )
}
