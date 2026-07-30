import { Star } from '@phosphor-icons/react'

export function Rating({
  value,
  onChange,
  size = 18,
}: {
  value: number
  onChange?: (value: number) => void
  size?: number
}) {
  return (
    <div className="flex items-center gap-0.5" aria-label={`${value} trên 5 sao`}>
      {[1, 2, 3, 4, 5].map((star) =>
        onChange ? (
          <button
            key={star}
            type="button"
            className="rounded p-0.5 text-amber-500 transition-transform hover:scale-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            onClick={() => onChange(star)}
            aria-label={`Chọn ${star} sao`}
          >
            <Star size={size} weight={star <= value ? 'fill' : 'regular'} />
          </button>
        ) : (
          <Star
            key={star}
            size={size}
            weight={star <= Math.round(value) ? 'fill' : 'regular'}
            className="text-amber-500"
            aria-hidden
          />
        ),
      )}
    </div>
  )
}
