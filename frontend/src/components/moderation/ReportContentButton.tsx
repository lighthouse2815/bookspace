import { Flag, X } from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage } from '../../lib/api'
import { moderationService } from '../../services/moderation.service'
import type { ContentReportReason, ContentReportTargetType } from '../../types/domain'
import { Button } from '../ui/Button'

const reasons: Array<{ value: ContentReportReason; label: string }> = [
  { value: 'SPAM', label: 'Spam hoặc quảng cáo' },
  { value: 'HARASSMENT', label: 'Quấy rối hoặc bắt nạt' },
  { value: 'HATEFUL_CONTENT', label: 'Nội dung thù ghét' },
  { value: 'INAPPROPRIATE_CONTENT', label: 'Nội dung không phù hợp' },
  { value: 'MISINFORMATION', label: 'Thông tin sai lệch' },
  { value: 'OTHER', label: 'Lý do khác' },
]

interface ReportContentButtonProps {
  targetType: ContentReportTargetType
  targetId: string
  ownerId: string
  label?: string
  compact?: boolean
}

export function ReportContentButton({
  targetType,
  targetId,
  ownerId,
  label = 'Báo cáo',
  compact = false,
}: ReportContentButtonProps) {
  const { user, isAuthenticated } = useAuth()
  const [open, setOpen] = useState(false)

  if (!isAuthenticated || !user || user.id === ownerId) return null

  return (
    <>
      <button
        type="button"
        className={compact ? 'reaction-button shrink-0' : 'button button-ghost button-sm'}
        aria-label={`${label} nội dung`}
        onClick={() => setOpen(true)}
      >
        <Flag size={compact ? 15 : 16} />
        {!compact ? label : null}
      </button>
      {open ? (
        <ReportDialog
          targetType={targetType}
          targetId={targetId}
          label={label}
          onClose={() => setOpen(false)}
        />
      ) : null}
    </>
  )
}

function ReportDialog({
  targetType,
  targetId,
  label,
  onClose,
}: {
  targetType: ContentReportTargetType
  targetId: string
  label: string
  onClose: () => void
}) {
  const { showToast } = useToast()
  const [reason, setReason] = useState<ContentReportReason>('SPAM')
  const [details, setDetails] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (submitting) return
    setSubmitting(true)
    try {
      await moderationService.create({
        targetType,
        targetId,
        reason,
        details: details.trim() || undefined,
      })
      showToast('Đã gửi báo cáo đến đội ngũ quản trị', 'success')
      onClose()
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gửi báo cáo'), 'error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-[80] grid place-items-center bg-black/55 p-4 backdrop-blur-sm"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby={`report-title-${targetId}`}
        className="surface w-full max-w-lg p-5 shadow-2xl sm:p-7"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="eyebrow">An toàn cộng đồng</p>
            <h2 id={`report-title-${targetId}`} className="mt-2 text-xl font-bold text-heading">
              {label}
            </h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Báo cáo được giữ riêng tư và chỉ quản trị viên có quyền xem.
            </p>
          </div>
          <button type="button" className="reaction-button" aria-label="Đóng báo cáo" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <form onSubmit={submit} className="mt-6 space-y-5">
          <label className="block">
            <span className="mb-2 block text-sm font-semibold text-heading">Lý do báo cáo</span>
            <select
              className="input w-full"
              value={reason}
              onChange={(event) => setReason(event.target.value as ContentReportReason)}
            >
              {reasons.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-2 block text-sm font-semibold text-heading">
              Mô tả thêm <span className="font-normal text-muted">(không bắt buộc)</span>
            </span>
            <textarea
              className="input min-h-28 w-full resize-y"
              value={details}
              maxLength={1000}
              placeholder="Cho quản trị viên biết điều gì cần được xem xét…"
              onChange={(event) => setDetails(event.target.value)}
            />
            <span className="mt-1 block text-right text-xs text-muted">{details.length}/1000</span>
          </label>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={onClose}>
              Hủy
            </Button>
            <Button type="submit" loading={submitting} icon={<Flag size={17} />}>
              Gửi báo cáo
            </Button>
          </div>
        </form>
      </section>
    </div>
  )
}
