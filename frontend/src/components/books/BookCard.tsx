import { Star } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'
import type { Book } from '../../types/domain'
import { BookCover } from './BookCover'

export function BookCard({ book }: { book: Book }) {
  return (
    <article className="group min-w-0">
      <Link to={`/books/${book.id}`} className="block focus-visible:focus-ring">
        <div className="relative aspect-[2/3] overflow-hidden rounded-2xl bg-surface-muted">
          <BookCover
            src={book.coverImageUrl}
            title={book.title}
            className="h-full w-full transition-transform duration-500 group-hover:scale-[1.035] motion-reduce:transition-none"
          />
          {book.shelf ? (
            <span className="absolute bottom-3 left-3 rounded-full bg-slate-950/80 px-3 py-1 text-xs font-semibold text-white backdrop-blur-sm">
              {book.shelf === 'READING'
                ? 'Đang đọc'
                : book.shelf === 'READ'
                  ? 'Đã đọc'
                  : 'Muốn đọc'}
            </span>
          ) : null}
        </div>
        <h3 className="mt-3 line-clamp-2 font-semibold leading-snug text-heading transition-colors group-hover:text-accent-strong">
          {book.title}
        </h3>
      </Link>
      {book.author ? (
        <Link
          to={`/authors/${book.author.id}`}
          className="mt-1 block truncate text-sm text-muted transition-colors hover:text-accent-strong focus-visible:focus-ring"
        >
          {book.author.name}
        </Link>
      ) : (
        <p className="mt-1 truncate text-sm text-muted">Tác giả đang cập nhật</p>
      )}
      <div className="mt-2 flex items-center gap-1.5 text-xs text-muted">
        <Star size={15} weight="fill" className="text-amber-500" aria-hidden />
        <span className="font-semibold text-heading">{(book.averageRating ?? 0).toFixed(1)}</span>
        <span>({book.reviewCount ?? 0} đánh giá)</span>
      </div>
    </article>
  )
}
