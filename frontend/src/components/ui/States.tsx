import { ArrowClockwise, Books, type Icon } from '@phosphor-icons/react'
import { Button } from './Button'

export function LoadingGrid({ count = 8 }: { count?: number }) {
  return (
    <div className="book-grid" aria-label="Đang tải dữ liệu" aria-busy="true">
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className="animate-pulse">
          <div className="aspect-[2/3] rounded-2xl bg-surface-muted" />
          <div className="mt-4 h-4 w-4/5 rounded bg-surface-muted" />
          <div className="mt-2 h-3 w-2/5 rounded bg-surface-muted" />
        </div>
      ))}
    </div>
  )
}

export function LoadingRows({ count = 4 }: { count?: number }) {
  return (
    <div className="space-y-3" aria-label="Đang tải dữ liệu" aria-busy="true">
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className="surface flex animate-pulse gap-4 p-4">
          <div className="h-12 w-12 rounded-xl bg-surface-muted" />
          <div className="flex-1 space-y-3 py-1">
            <div className="h-4 w-2/5 rounded bg-surface-muted" />
            <div className="h-3 w-4/5 rounded bg-surface-muted" />
          </div>
        </div>
      ))}
    </div>
  )
}

interface EmptyStateProps {
  title: string
  description: string
  icon?: Icon
  action?: React.ReactNode
}

export function EmptyState({
  title,
  description,
  icon: EmptyIcon = Books,
  action,
}: EmptyStateProps) {
  return (
    <div className="empty-state">
      <div className="empty-icon">
        <EmptyIcon size={28} weight="duotone" aria-hidden />
      </div>
      <h2 className="mt-4 text-lg font-semibold text-heading">{title}</h2>
      <p className="mt-2 max-w-md text-sm leading-6 text-muted">{description}</p>
      {action ? <div className="mt-5">{action}</div> : null}
    </div>
  )
}

export function ErrorState({
  message,
  retry,
}: {
  message: string
  retry?: () => void
}) {
  return (
    <div className="error-state" role="alert">
      <p className="font-semibold text-heading">Không thể tải dữ liệu</p>
      <p className="mt-1 text-sm text-muted">{message}</p>
      {retry ? (
        <Button
          variant="secondary"
          size="sm"
          icon={<ArrowClockwise size={16} />}
          onClick={retry}
          className="mt-4"
        >
          Thử lại
        </Button>
      ) : null}
    </div>
  )
}
