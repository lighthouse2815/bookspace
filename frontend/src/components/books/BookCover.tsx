import { useState } from 'react'
import { imageFallback } from '../../lib/format'

export function BookCover({
  src,
  title,
  className = '',
}: {
  src?: string
  title: string
  className?: string
}) {
  const [current, setCurrent] = useState(src || imageFallback(title))
  const [failed, setFailed] = useState(false)
  if (failed) {
    return (
      <div
        className={`grid place-items-center bg-[linear-gradient(145deg,var(--accent-soft),var(--surface-muted))] px-3 text-center text-sm font-semibold text-heading ${className}`}
        role="img"
        aria-label={`Bìa sách ${title} chưa có ảnh`}
      >
        {title}
      </div>
    )
  }
  return (
    <img
      src={current}
      alt={`Bìa sách ${title}`}
      loading="lazy"
      className={`bg-surface-muted object-cover ${className}`}
      onError={() => {
        const fallback = imageFallback(`${title}-fallback`)
        if (current !== fallback) setCurrent(fallback)
        else setFailed(true)
      }}
    />
  )
}
