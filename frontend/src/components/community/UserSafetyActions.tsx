import { EyeSlash, Prohibit, WarningCircle, X } from '@phosphor-icons/react'
import { useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useBlockUser, useMuteUser } from '../../hooks/useCommunity'
import { errorMessage } from '../../lib/api'
import { Button } from '../ui/Button'

export function MuteUserButton({
  targetId,
  displayName,
  isMuted = false,
  compact = false,
}: {
  targetId: string
  displayName: string
  isMuted?: boolean
  compact?: boolean
}) {
  const { user } = useAuth()
  const { showToast } = useToast()
  const mutation = useMuteUser(targetId, isMuted)
  if (!user || user.id === targetId) return null

  const label = isMuted ? `Bỏ ẩn nội dung của ${displayName}` : `Ẩn nội dung của ${displayName}`
  if (compact) {
    return (
      <button
        type="button"
        className="reaction-button shrink-0"
        aria-label={label}
        disabled={mutation.isPending}
        onClick={() =>
          mutation.mutate(undefined, {
            onSuccess: () =>
              showToast(
                isMuted ? 'Đã hiển thị lại nội dung' : 'Đã ẩn nội dung từ người đọc này',
                'success',
              ),
            onError: (error) => showToast(errorMessage(error), 'error'),
          })
        }
      >
        <EyeSlash size={15} />
      </button>
    )
  }

  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      loading={mutation.isPending}
      icon={<EyeSlash size={17} />}
      aria-label={label}
      onClick={() =>
        mutation.mutate(undefined, {
          onSuccess: () =>
            showToast(
              isMuted ? 'Đã hiển thị lại nội dung' : 'Đã ẩn nội dung từ người đọc này',
              'success',
            ),
          onError: (error) => showToast(errorMessage(error), 'error'),
        })
      }
    >
      {isMuted ? 'Bỏ ẩn' : 'Ẩn nội dung'}
    </Button>
  )
}

export function BlockUserButton({
  targetId,
  displayName,
  onBlocked,
}: {
  targetId: string
  displayName: string
  onBlocked?: () => void
}) {
  const { user } = useAuth()
  const [open, setOpen] = useState(false)
  if (!user || user.id === targetId) return null

  return (
    <>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        icon={<Prohibit size={17} />}
        aria-label={`Chặn ${displayName}`}
        onClick={() => setOpen(true)}
      >
        Chặn
      </Button>
      {open ? (
        <BlockDialog
          targetId={targetId}
          displayName={displayName}
          onClose={() => setOpen(false)}
          onBlocked={onBlocked}
        />
      ) : null}
    </>
  )
}

function BlockDialog({
  targetId,
  displayName,
  onClose,
  onBlocked,
}: {
  targetId: string
  displayName: string
  onClose: () => void
  onBlocked?: () => void
}) {
  const { showToast } = useToast()
  const block = useBlockUser(targetId)

  return (
    <div
      className="fixed inset-0 z-[80] grid place-items-center bg-black/55 p-4 backdrop-blur-sm"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !block.isPending) onClose()
      }}
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby={`block-title-${targetId}`}
        className="surface w-full max-w-md p-5 shadow-2xl sm:p-7"
      >
        <div className="flex items-start justify-between gap-4">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-red-500/10 text-red-600 dark:text-red-300">
            <WarningCircle size={23} weight="duotone" />
          </div>
          <button
            type="button"
            className="reaction-button"
            aria-label="Đóng xác nhận chặn"
            disabled={block.isPending}
            onClick={onClose}
          >
            <X size={18} />
          </button>
        </div>
        <h2 id={`block-title-${targetId}`} className="mt-5 text-xl font-bold text-heading">
          Chặn {displayName}?
        </h2>
        <p className="mt-3 text-sm leading-6 text-muted">
          Hai tài khoản sẽ không còn nhìn thấy hồ sơ hoặc tương tác với nhau. Mọi kết nối theo
          dõi giữa hai bên cũng sẽ bị gỡ.
        </p>
        <p className="mt-3 rounded-xl bg-surface-muted p-3 text-xs leading-5 text-muted">
          Bạn có thể bỏ chặn sau trong Cài đặt, nhưng kết nối theo dõi sẽ không tự khôi phục.
        </p>
        <div className="mt-6 flex justify-end gap-2">
          <Button type="button" variant="secondary" disabled={block.isPending} onClick={onClose}>
            Hủy
          </Button>
          <Button
            type="button"
            variant="danger"
            loading={block.isPending}
            icon={<Prohibit size={17} />}
            onClick={() =>
              block.mutate(undefined, {
                onSuccess: () => {
                  showToast('Đã chặn người đọc này', 'success')
                  onClose()
                  onBlocked?.()
                },
                onError: (error) => showToast(errorMessage(error, 'Không thể chặn người dùng'), 'error'),
              })
            }
          >
            Xác nhận chặn
          </Button>
        </div>
      </section>
    </div>
  )
}
