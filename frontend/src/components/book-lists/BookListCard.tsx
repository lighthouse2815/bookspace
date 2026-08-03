import { GlobeHemisphereWest, LockKey } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'
import type { BookListSummary } from '../../types/domain'
import { BookCover } from '../books/BookCover'

export function BookListCard({
  list,
  actions,
}: {
  list: BookListSummary
  actions?: React.ReactNode
}) {
  return (
    <article className="surface flex h-full flex-col p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-muted px-2.5 py-1 text-xs font-semibold text-muted">
              {list.visibility === 'PRIVATE' ? <LockKey size={13} /> : <GlobeHemisphereWest size={13} />}
              {list.visibility === 'PRIVATE' ? 'Riêng tư' : 'Công khai'}
            </span>
            <span className="text-xs font-semibold text-muted">{list.bookCount} sách</span>
          </div>
          <Link
            to={`/lists/${list.id}`}
            className="mt-3 block break-words text-xl font-bold text-heading hover:text-accent-strong"
          >
            {list.name}
          </Link>
        </div>
        {actions ? <div className="flex shrink-0 gap-1">{actions}</div> : null}
      </div>

      <p className="mt-2 line-clamp-2 min-h-10 text-sm leading-5 text-muted">
        {list.description || 'Một góc đọc được tuyển chọn bởi chủ bộ sưu tập.'}
      </p>

      <Link
        to={`/lists/${list.id}`}
        className="mt-5 grid min-h-36 grid-cols-4 gap-2 rounded-2xl bg-surface-muted p-3 focus-visible:focus-ring"
        aria-label={`Mở bộ sưu tập ${list.name}`}
      >
        {list.previewBooks.length ? (
          list.previewBooks.map((book) => (
            <BookCover
              key={book.id}
              src={book.coverImageUrl}
              title={book.title}
              className="aspect-[2/3] h-full min-h-0 w-full rounded-lg"
            />
          ))
        ) : (
          <span className="col-span-4 grid place-items-center text-center text-sm text-muted">
            Chưa có sách trong bộ sưu tập
          </span>
        )}
      </Link>
    </article>
  )
}
