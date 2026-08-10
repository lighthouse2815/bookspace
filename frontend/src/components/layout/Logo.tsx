import { BookOpenText } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'

export function Logo({
  compact = false,
  compactOnMobile = false,
}: {
  compact?: boolean
  compactOnMobile?: boolean
}) {
  return (
    <Link
      to="/"
      aria-label="BookSpace"
      className="inline-flex shrink-0 items-center gap-2.5 rounded-lg focus-visible:focus-ring"
    >
      <span className="inline-flex h-9 w-9 items-center justify-center rounded-[10px] bg-accent text-white shadow-accent">
        <BookOpenText size={21} weight="bold" aria-hidden />
      </span>
      {!compact ? (
        <span
          className={`${compactOnMobile ? 'hidden min-[400px]:inline' : ''} text-lg font-bold tracking-tight text-heading`}
        >
          BookSpace
        </span>
      ) : null}
    </Link>
  )
}
