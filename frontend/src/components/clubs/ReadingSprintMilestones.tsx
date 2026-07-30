import {
  CaretDown,
  CaretUp,
  ChatCircleDots,
  CheckCircle,
  Flag,
  PaperPlaneTilt,
  PencilSimple,
  Plus,
  Trash,
  X,
} from '@phosphor-icons/react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation } from 'react-router-dom'
import {
  useCreateReadingSprintMilestone,
  useCreateReadingSprintMilestoneResponse,
  useDeleteReadingSprintMilestone,
  useDeleteReadingSprintMilestoneResponse,
  useReadingSprintMilestoneResponses,
  useUpdateReadingSprintMilestone,
} from '../../hooks/useReadingSprints'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import { readingSprintUnitNames } from '../../lib/reading-sprint'
import type {
  ReadingSprintDetail,
  ReadingSprintMilestone,
} from '../../types/domain'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { Avatar } from '../ui/Avatar'
import { Button } from '../ui/Button'
import { InputField, TextareaField } from '../ui/FormField'
import { Pagination } from '../ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../ui/States'

interface MilestoneFormState {
  title: string
  description: string
  targetValue: string
}

function milestoneForm(milestone?: ReadingSprintMilestone): MilestoneFormState {
  return {
    title: milestone?.title ?? '',
    description: milestone?.description ?? '',
    targetValue: milestone ? String(milestone.targetValue) : '',
  }
}

function MilestoneDiscussion({
  sprint,
  milestone,
}: {
  sprint: ReadingSprintDetail
  milestone: ReadingSprintMilestone
}) {
  const [expanded, setExpanded] = useState(false)
  const [content, setContent] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 10
  const responses = useReadingSprintMilestoneResponses(
    sprint.clubId,
    sprint.id,
    milestone.id,
    expanded,
    page,
    pageSize,
  )
  const createResponse = useCreateReadingSprintMilestoneResponse(
    sprint.clubId,
    sprint.id,
    milestone.id,
  )
  const deleteResponse = useDeleteReadingSprintMilestoneResponse(
    sprint.clubId,
    sprint.id,
    milestone.id,
  )
  const { isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const location = useLocation()
  const totalPages = Math.max(responses.data?.totalPages ?? 1, 1)

  useEffect(() => {
    if (responses.data && page > totalPages) setPage(totalPages)
  }, [page, responses.data, totalPages])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const value = content.trim()
    if (!value) {
      showToast('Hãy nhập nội dung phản hồi', 'error')
      return
    }
    if (value.length > 2000) {
      showToast('Phản hồi không được vượt quá 2.000 ký tự', 'error')
      return
    }

    try {
      const nextLastPage = Math.max(
        Math.ceil(((responses.data?.totalItems ?? 0) + 1) / pageSize),
        1,
      )
      await createResponse.mutateAsync(value)
      setContent('')
      setPage(nextLastPage)
      showToast('Đã gửi phản hồi', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gửi phản hồi'), 'error')
    }
  }

  const removeResponse = async (responseId: string) => {
    try {
      await deleteResponse.mutateAsync(responseId)
      showToast('Đã xóa phản hồi', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa phản hồi'), 'error')
    }
  }

  return (
    <div className="mt-4 border-t border-border pt-4">
      <button
        type="button"
        className="flex w-full items-center justify-between gap-3 rounded-lg text-left text-sm font-semibold text-heading focus-visible:focus-ring"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
      >
        <span className="inline-flex items-center gap-2">
          <ChatCircleDots size={17} />
          {milestone.responseCount} phản hồi
        </span>
        {expanded ? <CaretUp size={17} /> : <CaretDown size={17} />}
      </button>

      {expanded ? (
        <div className="mt-4">
          {responses.isLoading ? (
            <LoadingRows count={2} />
          ) : responses.isError ? (
            <ErrorState
              message="Không thể tải thảo luận của cột mốc."
              retry={() => void responses.refetch()}
            />
          ) : responses.data?.items.length ? (
            <div className="space-y-3">
              {responses.data.items.map((response) => (
                <article
                  key={response.id}
                  className="rounded-xl border border-border bg-surface-muted/55 p-3.5"
                >
                  <div className="flex items-start gap-3">
                    <Link to={`/users/${response.author.id}`} className="shrink-0">
                      <Avatar
                        src={response.author.avatarUrl}
                        name={response.author.displayName}
                        size="sm"
                      />
                    </Link>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="min-w-0">
                          <Link
                            to={`/users/${response.author.id}`}
                            className="text-sm font-semibold text-heading hover:text-accent-strong"
                          >
                            {response.author.displayName}
                          </Link>
                          <span className="ml-2 text-xs text-muted">
                            {formatRelativeTime(response.createdAt)}
                          </span>
                        </div>
                        {response.canDelete && sprint.status === 'ACTIVE' ? (
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            className="text-red-700 dark:text-red-300"
                            loading={deleteResponse.isPending}
                            icon={<Trash size={15} />}
                            onClick={() => void removeResponse(response.id)}
                            aria-label={`Xóa phản hồi của ${response.author.displayName}`}
                          >
                            Xóa
                          </Button>
                        ) : null}
                      </div>
                      <p className="mt-2 whitespace-pre-line break-words text-sm leading-6 text-body">
                        {response.content}
                      </p>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <p className="rounded-xl bg-surface-muted p-4 text-sm text-muted">
              Chưa có phản hồi. Hãy mở đầu cuộc trò chuyện về cột mốc này.
            </p>
          )}
          <Pagination
            page={page}
            totalPages={totalPages}
            disabled={responses.isFetching}
            onPageChange={setPage}
            className="mt-4 border-t border-border pt-4"
          />

          {sprint.permissions.canDiscuss ? (
            <form onSubmit={submit} className="mt-4">
              <label htmlFor={`milestone-response-${milestone.id}`} className="field-label">
                Chia sẻ ở cột mốc này
              </label>
              <textarea
                id={`milestone-response-${milestone.id}`}
                className="input min-h-24 resize-y"
                value={content}
                maxLength={2000}
                onChange={(event) => setContent(event.target.value)}
                placeholder="Cảm nhận, câu hỏi hoặc chi tiết bạn muốn cùng nhóm thảo luận."
              />
              <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
                <span className="text-xs text-muted">{content.length}/2.000 ký tự</span>
                <Button
                  type="submit"
                  size="sm"
                  loading={createResponse.isPending}
                  icon={<PaperPlaneTilt size={16} />}
                >
                  Gửi phản hồi
                </Button>
              </div>
            </form>
          ) : !isAuthenticated && sprint.status === 'ACTIVE' ? (
            <Link
              to="/login"
              state={{ from: location.pathname }}
              className="button button-secondary button-sm mt-4"
            >
              Đăng nhập để thảo luận
            </Link>
          ) : (
            <p className="mt-4 text-xs leading-5 text-muted">
              {sprint.status === 'PLANNED'
                ? 'Thảo luận sẽ mở khi đợt đọc bắt đầu.'
                : sprint.status === 'ACTIVE'
                  ? 'Chỉ thành viên đang tham gia đợt đọc mới có thể phản hồi.'
                  : 'Thảo luận đã đóng khi đợt đọc kết thúc.'}
            </p>
          )}
        </div>
      ) : null}
    </div>
  )
}

function MilestoneCard({
  sprint,
  milestone,
  onEdit,
}: {
  sprint: ReadingSprintDetail
  milestone: ReadingSprintMilestone
  onEdit: (milestone: ReadingSprintMilestone) => void
}) {
  const [confirmDelete, setConfirmDelete] = useState(false)
  const deleteMilestone = useDeleteReadingSprintMilestone(sprint.clubId, sprint.id)
  const { showToast } = useToast()

  const remove = async () => {
    try {
      await deleteMilestone.mutateAsync(milestone.id)
      showToast('Đã xóa cột mốc', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa cột mốc'), 'error')
    }
  }

  return (
    <article className="surface min-w-0 p-5">
      <div className="flex items-start gap-4">
        <div
          className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${
            milestone.reachedByViewer
              ? 'bg-accent text-white'
              : 'bg-accent-soft text-accent-strong'
          }`}
        >
          {milestone.reachedByViewer ? (
            <CheckCircle size={23} weight="fill" />
          ) : (
            <Flag size={22} weight="duotone" />
          )}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase tracking-[0.12em] text-accent-strong">
                {milestone.targetValue} {readingSprintUnitNames[sprint.targetUnit]}
              </p>
              <h3 className="mt-1 break-words text-lg font-bold text-heading">
                {milestone.title}
              </h3>
            </div>
            {sprint.permissions.canManage &&
            (sprint.status === 'PLANNED' || sprint.status === 'ACTIVE') ? (
              <div className="flex gap-1">
                <button
                  type="button"
                  className="icon-button"
                  onClick={() => onEdit(milestone)}
                  aria-label={`Sửa cột mốc ${milestone.title}`}
                >
                  <PencilSimple size={17} />
                </button>
                <button
                  type="button"
                  className="icon-button text-red-700 dark:text-red-300"
                  onClick={() => setConfirmDelete(true)}
                  aria-label={`Xóa cột mốc ${milestone.title}`}
                >
                  <Trash size={17} />
                </button>
              </div>
            ) : null}
          </div>
          {milestone.description ? (
            <p className="mt-2 whitespace-pre-line break-words text-sm leading-6 text-muted">
              {milestone.description}
            </p>
          ) : null}
          <p className="mt-3 text-xs font-medium text-muted">
            {!sprint.viewerParticipation?.isActive
              ? `Mở tại ${milestone.targetValue} ${readingSprintUnitNames[sprint.targetUnit]}`
              : milestone.reachedByViewer
              ? 'Bạn đã chạm cột mốc này'
              : `Còn ${Math.max(
                  milestone.targetValue -
                    (sprint.viewerParticipation?.progressValue ?? 0),
                  0,
                )} ${readingSprintUnitNames[sprint.targetUnit]}`}
          </p>
        </div>
      </div>

      {confirmDelete ? (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-red-50 p-3 dark:bg-red-950/25">
          <p className="text-sm text-heading">
            Xóa cột mốc và toàn bộ phản hồi bên trong?
          </p>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setConfirmDelete(false)}
            >
              Giữ lại
            </Button>
            <Button
              type="button"
              variant="danger"
              size="sm"
              loading={deleteMilestone.isPending}
              onClick={() => void remove()}
            >
              Xóa cột mốc
            </Button>
          </div>
        </div>
      ) : null}

      <MilestoneDiscussion sprint={sprint} milestone={milestone} />
    </article>
  )
}

export function ReadingSprintMilestones({ sprint }: { sprint: ReadingSprintDetail }) {
  const [showEditor, setShowEditor] = useState(false)
  const [editing, setEditing] = useState<ReadingSprintMilestone | null>(null)
  const [form, setForm] = useState<MilestoneFormState>(() => milestoneForm())
  const [errors, setErrors] = useState<Record<string, string>>({})
  const createMilestone = useCreateReadingSprintMilestone(sprint.clubId, sprint.id)
  const updateMilestone = useUpdateReadingSprintMilestone(sprint.clubId, sprint.id)
  const { showToast } = useToast()
  const milestones = [...sprint.milestones].sort(
    (left, right) => left.targetValue - right.targetValue,
  )
  const canEdit =
    sprint.permissions.canManage &&
    (sprint.status === 'PLANNED' || sprint.status === 'ACTIVE')

  const closeEditor = () => {
    setShowEditor(false)
    setEditing(null)
    setForm(milestoneForm())
    setErrors({})
  }

  const startCreate = () => {
    setEditing(null)
    setForm(milestoneForm())
    setErrors({})
    setShowEditor(true)
  }

  const startEdit = (milestone: ReadingSprintMilestone) => {
    setEditing(milestone)
    setForm(milestoneForm(milestone))
    setErrors({})
    setShowEditor(true)
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const targetValue = Number(form.targetValue)
    const nextErrors: Record<string, string> = {}
    if (!form.title.trim()) {
      nextErrors.title = 'Tên cột mốc không được để trống.'
    } else if (form.title.trim().length > 150) {
      nextErrors.title = 'Tên cột mốc không được vượt quá 150 ký tự.'
    }
    if (form.description.trim().length > 2000) {
      nextErrors.description = 'Mô tả không được vượt quá 2.000 ký tự.'
    }
    if (
      !Number.isInteger(targetValue) ||
      targetValue < 1 ||
      targetValue > sprint.targetValue
    ) {
      nextErrors.targetValue = `Cột mốc phải từ 1 đến ${sprint.targetValue}.`
    }
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return

    const input = {
      title: form.title.trim(),
      description: form.description.trim() || null,
      targetValue,
    }

    try {
      if (editing) {
        await updateMilestone.mutateAsync({ milestoneId: editing.id, input })
        showToast('Đã cập nhật cột mốc', 'success')
      } else {
        await createMilestone.mutateAsync(input)
        showToast('Đã tạo cột mốc', 'success')
      }
      closeEditor()
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu cột mốc'), 'error')
    }
  }

  return (
    <section className="mt-10" aria-labelledby="sprint-milestones-heading">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="eyebrow">Đọc đến đâu, nói đến đó</p>
          <h2 id="sprint-milestones-heading" className="mt-2 text-2xl font-bold text-heading">
            Cột mốc thảo luận
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
            Mỗi mốc mở một không gian trò chuyện gắn với đúng phần sách cả nhóm vừa đi qua.
          </p>
        </div>
        {canEdit ? (
          <Button
            icon={showEditor ? <X size={18} /> : <Plus size={18} />}
            variant={showEditor ? 'secondary' : 'primary'}
            onClick={() => {
              if (showEditor) closeEditor()
              else startCreate()
            }}
          >
            {showEditor ? 'Đóng biểu mẫu' : 'Thêm cột mốc'}
          </Button>
        ) : null}
      </div>

      {showEditor ? (
        <form onSubmit={submit} className="mt-5 surface p-5 sm:p-6" noValidate>
          <div className="flex items-start justify-between gap-4">
            <div>
              <h3 className="text-lg font-bold text-heading">
                {editing ? 'Chỉnh sửa cột mốc' : 'Cột mốc mới'}
              </h3>
              <p className="mt-1 text-sm text-muted">
                Đặt mốc theo tiến độ tuyệt đối, tối đa {sprint.targetValue}{' '}
                {readingSprintUnitNames[sprint.targetUnit]}.
              </p>
            </div>
            <button
              type="button"
              className="icon-button"
              onClick={closeEditor}
              aria-label="Đóng biểu mẫu cột mốc"
            >
              <X size={18} />
            </button>
          </div>
          <div className="mt-5 grid gap-5 md:grid-cols-[minmax(0,1fr)_12rem]">
            <InputField
              label="Tên cột mốc"
              name="milestone-title"
              value={form.title}
              maxLength={150}
              error={errors.title}
              placeholder="Ví dụ: Gặp nhân vật bí ẩn"
              onChange={(event) => {
                setForm({ ...form, title: event.target.value })
                setErrors({ ...errors, title: '' })
              }}
              required
            />
            <InputField
              label={`Tại (${readingSprintUnitNames[sprint.targetUnit]})`}
              name="milestone-target"
              type="number"
              min={1}
              max={sprint.targetValue}
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
            <TextareaField
              label="Gợi ý thảo luận"
              name="milestone-description"
              className="md:min-h-24"
              value={form.description}
              maxLength={2000}
              error={errors.description}
              hint={`${form.description.length}/2.000 ký tự`}
              placeholder="Đặt câu hỏi hoặc gợi ý chủ đề, tránh tiết lộ phần sau."
              onChange={(event) => {
                setForm({ ...form, description: event.target.value })
                setErrors({ ...errors, description: '' })
              }}
            />
            <div className="flex items-end gap-2">
              <Button
                type="submit"
                loading={createMilestone.isPending || updateMilestone.isPending}
              >
                {editing ? 'Lưu thay đổi' : 'Tạo cột mốc'}
              </Button>
              <Button type="button" variant="ghost" onClick={closeEditor}>
                Hủy
              </Button>
            </div>
          </div>
        </form>
      ) : null}

      <div className="mt-5">
        {milestones.length ? (
          <div className="grid gap-4 lg:grid-cols-2">
            {milestones.map((milestone) => (
              <MilestoneCard
                key={milestone.id}
                sprint={sprint}
                milestone={milestone}
                onEdit={startEdit}
              />
            ))}
          </div>
        ) : (
          <EmptyState
            icon={Flag}
            title="Chưa có cột mốc thảo luận"
            description={
              canEdit
                ? 'Thêm những điểm dừng vừa đủ để cuộc trò chuyện không đi trước tiến độ đọc.'
                : 'Quản lý câu lạc bộ chưa thiết lập cột mốc cho đợt đọc này.'
            }
            action={
              canEdit ? (
                <Button icon={<Plus size={18} />} onClick={startCreate}>
                  Tạo cột mốc đầu tiên
                </Button>
              ) : undefined
            }
          />
        )}
      </div>
    </section>
  )
}
