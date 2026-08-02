import { CheckCircle, EyeSlash, LockKey, ShieldWarning, XCircle } from '@phosphor-icons/react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { AdminNav } from '../../components/admin/AdminNav'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage } from '../../lib/api'
import { formatDate, formatRelativeTime } from '../../lib/format'
import {
  moderationService,
  type ResolveContentReportInput,
} from '../../services/moderation.service'
import type {
  ContentReport,
  ContentReportReason,
  ContentReportStatus,
  ContentReportTargetType,
} from '../../types/domain'

const statusLabels: Record<ContentReportStatus, string> = {
  PENDING: 'Đang chờ',
  RESOLVED: 'Đã xử lý',
  DISMISSED: 'Đã bác bỏ',
}

const targetLabels: Record<ContentReportTargetType, string> = {
  USER: 'Hồ sơ',
  REVIEW: 'Đánh giá',
  REVIEW_COMMENT: 'Bình luận đánh giá',
  CLUB_POST: 'Bài viết câu lạc bộ',
  CLUB_POST_COMMENT: 'Bình luận câu lạc bộ',
  CLUB_CHAT_MESSAGE: 'Tin nhắn câu lạc bộ',
}

const reasonLabels: Record<ContentReportReason, string> = {
  SPAM: 'Spam hoặc quảng cáo',
  HARASSMENT: 'Quấy rối hoặc bắt nạt',
  HATEFUL_CONTENT: 'Nội dung thù ghét',
  INAPPROPRIATE_CONTENT: 'Nội dung không phù hợp',
  MISINFORMATION: 'Thông tin sai lệch',
  OTHER: 'Lý do khác',
}

type FilterValue<T extends string> = T | ''

export function AdminModerationPage() {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [status, setStatus] = useState<FilterValue<ContentReportStatus>>('PENDING')
  const [targetType, setTargetType] = useState<FilterValue<ContentReportTargetType>>('')
  const [reason, setReason] = useState<FilterValue<ContentReportReason>>('')
  const [page, setPage] = useState(1)
  const [notes, setNotes] = useState<Record<string, string>>({})

  const reports = useQuery({
    queryKey: ['admin', 'content-reports', status, targetType, reason, page],
    queryFn: () =>
      moderationService.reports({
        status: status || undefined,
        targetType: targetType || undefined,
        reason: reason || undefined,
        page,
      }),
  })

  const resolve = useMutation({
    mutationFn: ({ id, input }: { id: string; input: ResolveContentReportInput }) =>
      moderationService.resolve(id, input),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'content-reports'] })
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: ['club'] })
      setNotes((current) => ({ ...current, [variables.id]: '' }))
      showToast('Đã lưu quyết định kiểm duyệt', 'success')
    },
    onError: (error) => showToast(errorMessage(error, 'Không thể xử lý báo cáo'), 'error'),
  })

  const decide = (report: ContentReport, input: ResolveContentReportInput) => {
    const prompt =
      input.action === 'USER_LOCKED'
        ? `Khóa tài khoản ${report.targetOwner.displayName}? Token hiện tại của tài khoản sẽ không còn dùng được.`
        : input.action === 'CONTENT_REMOVED'
          ? 'Ẩn nội dung này khỏi BookSpace? Các báo cáo đang chờ cho cùng nội dung cũng sẽ được đóng.'
          : 'Bác bỏ báo cáo này?'
    if (!window.confirm(prompt)) return
    resolve.mutate({
      id: report.id,
      input: { ...input, resolutionNote: notes[report.id]?.trim() || undefined },
    })
  }

  return (
    <div className="container-page section-space">
      <p className="eyebrow">Quản trị BookSpace</p>
      <div className="mt-4 max-w-3xl">
        <h1 className="page-title">An toàn cộng đồng</h1>
        <p className="mt-3 leading-7 text-muted">
          Xem snapshot nội dung bị báo cáo, lưu dấu quyết định và xử lý vi phạm mà không làm lộ người báo cáo.
        </p>
      </div>
      <AdminNav />

      <section className="surface mb-6 grid gap-4 p-4 md:grid-cols-3" aria-label="Bộ lọc báo cáo">
        <FilterSelect
          label="Trạng thái"
          value={status}
          onChange={(value) => {
            setStatus(value as FilterValue<ContentReportStatus>)
            setPage(1)
          }}
          options={Object.entries(statusLabels)}
        />
        <FilterSelect
          label="Loại nội dung"
          value={targetType}
          onChange={(value) => {
            setTargetType(value as FilterValue<ContentReportTargetType>)
            setPage(1)
          }}
          options={Object.entries(targetLabels)}
        />
        <FilterSelect
          label="Lý do"
          value={reason}
          onChange={(value) => {
            setReason(value as FilterValue<ContentReportReason>)
            setPage(1)
          }}
          options={Object.entries(reasonLabels)}
        />
      </section>

      {reports.isLoading ? <LoadingRows count={4} /> : null}
      {reports.isError ? (
        <ErrorState message="Không thể tải hàng đợi kiểm duyệt." retry={() => void reports.refetch()} />
      ) : null}
      {reports.data && reports.data.items.length === 0 ? (
        <EmptyState
          icon={ShieldWarning}
          title="Không có báo cáo phù hợp"
          description="Hàng đợi hiện không có mục nào khớp với bộ lọc đã chọn."
        />
      ) : null}

      {reports.data?.items.length ? (
        <div className="space-y-4">
          {reports.data.items.map((report) => (
            <article key={report.id} className="surface overflow-hidden">
              <div className="grid gap-5 p-5 lg:grid-cols-[minmax(0,1fr)_19rem] lg:p-6">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="rounded-full bg-accent-soft px-3 py-1 text-xs font-bold text-accent-strong">
                      {targetLabels[report.targetType]}
                    </span>
                    <span className="rounded-full bg-surface-muted px-3 py-1 text-xs font-semibold text-heading">
                      {reasonLabels[report.reason]}
                    </span>
                    <span className="text-xs text-muted">{formatRelativeTime(report.createdAt)}</span>
                  </div>
                  <blockquote className="mt-4 border-l-2 border-accent pl-4 text-sm leading-7 text-body">
                    {report.targetPreview}
                  </blockquote>
                  {report.details ? (
                    <div className="mt-4 rounded-xl bg-surface-muted p-4">
                      <p className="text-xs font-bold uppercase tracking-wide text-muted">Mô tả của người báo cáo</p>
                      <p className="mt-2 whitespace-pre-line text-sm leading-6 text-body">{report.details}</p>
                    </div>
                  ) : null}
                  <div className="mt-4 flex flex-wrap items-center gap-4 text-sm">
                    <Link to={report.targetLink} className="font-semibold text-accent-strong hover:underline">
                      Mở vị trí nội dung
                    </Link>
                    <span className="text-muted">Mã {report.targetId.slice(0, 8)}</span>
                  </div>
                </div>

                <aside className="rounded-2xl border border-border p-4">
                  <p className="text-xs font-bold uppercase tracking-wide text-muted">Chủ nội dung</p>
                  <div className="mt-3 flex items-center gap-3">
                    <Avatar
                      src={report.targetOwner.avatarUrl}
                      name={report.targetOwner.displayName}
                      size="sm"
                    />
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-heading">
                        {report.targetOwner.displayName}
                      </p>
                      <Link
                        to={`/users/${report.targetOwner.id}`}
                        className="text-xs text-accent-strong hover:underline"
                      >
                        Xem hồ sơ
                      </Link>
                    </div>
                  </div>
                  <div className="mt-4 border-t border-border pt-4 text-xs leading-6 text-muted">
                    <p>Người báo cáo: {report.reporter.displayName}</p>
                    <p>Ngày gửi: {formatDate(report.createdAt)}</p>
                    <p>Trạng thái: {statusLabels[report.status]}</p>
                  </div>
                </aside>
              </div>

              {report.status === 'PENDING' ? (
                <div className="border-t border-border bg-surface-muted/40 p-5 lg:p-6">
                  <label className="block">
                    <span className="mb-2 block text-sm font-semibold text-heading">
                      Ghi chú quyết định <span className="font-normal text-muted">(không bắt buộc)</span>
                    </span>
                    <textarea
                      className="input min-h-20 w-full resize-y"
                      maxLength={1000}
                      value={notes[report.id] ?? ''}
                      onChange={(event) =>
                        setNotes((current) => ({ ...current, [report.id]: event.target.value }))
                      }
                    />
                  </label>
                  <div className="mt-4 flex flex-wrap justify-end gap-2">
                    <Button
                      variant="secondary"
                      icon={<XCircle size={17} />}
                      loading={resolve.isPending && resolve.variables?.id === report.id}
                      onClick={() => decide(report, { status: 'DISMISSED', action: 'NONE' })}
                    >
                      Bác bỏ
                    </Button>
                    {report.targetType !== 'USER' ? (
                      <Button
                        variant="danger"
                        icon={<EyeSlash size={17} />}
                        loading={resolve.isPending && resolve.variables?.id === report.id}
                        onClick={() =>
                          decide(report, { status: 'RESOLVED', action: 'CONTENT_REMOVED' })
                        }
                      >
                        Ẩn nội dung
                      </Button>
                    ) : null}
                    <Button
                      variant="danger"
                      icon={<LockKey size={17} />}
                      loading={resolve.isPending && resolve.variables?.id === report.id}
                      onClick={() => decide(report, { status: 'RESOLVED', action: 'USER_LOCKED' })}
                    >
                      Khóa tài khoản
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="flex flex-wrap items-start gap-3 border-t border-border bg-surface-muted/40 p-5 text-sm lg:p-6">
                  <CheckCircle size={20} className="mt-0.5 text-accent-strong" />
                  <div>
                    <p className="font-semibold text-heading">
                      {report.status === 'DISMISSED' ? 'Báo cáo đã bị bác bỏ' : 'Báo cáo đã được xử lý'}
                    </p>
                    <p className="mt-1 text-muted">
                      {report.moderator?.displayName ?? 'Quản trị viên'} ·{' '}
                      {report.resolvedAt ? formatDate(report.resolvedAt) : 'Không rõ thời điểm'}
                    </p>
                    {report.resolutionNote ? <p className="mt-2 text-body">{report.resolutionNote}</p> : null}
                  </div>
                </div>
              )}
            </article>
          ))}
          <Pagination
            page={reports.data.page}
            totalPages={reports.data.totalPages}
            onPageChange={setPage}
            disabled={reports.isFetching}
            className="pt-4"
          />
        </div>
      ) : null}
    </div>
  )
}

function FilterSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string
  value: string
  options: Array<[string, string]>
  onChange: (value: string) => void
}) {
  return (
    <label>
      <span className="mb-2 block text-xs font-bold uppercase tracking-wide text-muted">{label}</span>
      <select className="input w-full" value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Tất cả</option>
        {options.map(([optionValue, optionLabel]) => (
          <option key={optionValue} value={optionValue}>
            {optionLabel}
          </option>
        ))}
      </select>
    </label>
  )
}
