import { ArrowLeft, ArrowRight } from '@phosphor-icons/react'
import { Button } from './Button'

export function Pagination({
  page,
  totalPages,
  onPageChange,
  disabled = false,
  className = '',
}: {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
  disabled?: boolean
  className?: string
}) {
  const normalizedTotalPages = Math.max(totalPages, 1)
  if (normalizedTotalPages <= 1) return null

  return (
    <nav
      className={`flex flex-wrap items-center justify-between gap-3 ${className}`}
      aria-label="Phân trang"
    >
      <Button
        type="button"
        variant="secondary"
        size="sm"
        icon={<ArrowLeft size={16} />}
        disabled={disabled || page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        Trang trước
      </Button>
      <span className="text-sm font-semibold text-muted" aria-live="polite">
        Trang {page} / {normalizedTotalPages}
      </span>
      <Button
        type="button"
        variant="secondary"
        size="sm"
        icon={<ArrowRight size={16} />}
        disabled={disabled || page >= normalizedTotalPages}
        onClick={() => onPageChange(page + 1)}
      >
        Trang sau
      </Button>
    </nav>
  )
}
