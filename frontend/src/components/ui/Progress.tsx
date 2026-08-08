import { clamp } from '../../lib/format'

export function Progress({
  value,
  label,
  ariaLabel,
  ariaValueText,
  className = '',
}: {
  value: number
  label?: string
  ariaLabel?: string
  ariaValueText?: string
  className?: string
}) {
  const safe = clamp(value, 0, 100)
  return (
    <div className={className}>
      {label ? (
        <div className="mb-2 flex items-center justify-between gap-3 text-xs font-medium">
          <span className="text-muted">{label}</span>
          <span className="text-heading">{Math.round(safe)}%</span>
        </div>
      ) : null}
      <div
        className="h-2 overflow-hidden rounded-full bg-surface-muted"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(safe)}
        aria-valuetext={ariaValueText}
        aria-label={ariaLabel ?? label}
      >
        <div
          className="h-full rounded-full bg-accent transition-[width] duration-500 motion-reduce:transition-none"
          style={{ width: `${safe}%` }}
        />
      </div>
    </div>
  )
}
