import { useState } from 'react'
import { getInitials } from '../../lib/format'

export function Avatar({
  src,
  name,
  size = 'md',
}: {
  src?: string
  name?: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
}) {
  const [failed, setFailed] = useState(false)
  const sizes = {
    sm: 'h-8 w-8 text-xs',
    md: 'h-10 w-10 text-sm',
    lg: 'h-14 w-14 text-base',
    xl: 'h-24 w-24 text-xl',
  }

  if (src && !failed) {
    return (
      <img
        src={src}
        alt={name ? `Ảnh đại diện của ${name}` : 'Ảnh đại diện'}
        className={`${sizes[size]} shrink-0 rounded-full bg-surface-muted object-cover`}
        onError={() => setFailed(true)}
      />
    )
  }

  return (
    <span
      className={`${sizes[size]} inline-flex shrink-0 items-center justify-center rounded-full bg-accent-soft font-semibold text-accent-strong`}
      aria-label={name ? `Ảnh đại diện của ${name}` : 'Ảnh đại diện'}
    >
      {getInitials(name)}
    </span>
  )
}
