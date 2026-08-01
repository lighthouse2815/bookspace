import { Link } from 'react-router-dom'
import { formatRelativeTime } from '../../lib/format'
import type { FeedItem } from '../../types/domain'
import { Avatar } from '../ui/Avatar'
import { ReviewCard } from './ReviewCard'

const activityLabel: Record<Exclude<FeedItem['type'], 'REVIEW'>, string> = {
  READING_PROGRESS: 'đã cập nhật tiến độ đọc',
  CHALLENGE: 'đã hoàn thành một thử thách',
  CLUB_POST: 'đã đăng bài trong câu lạc bộ',
}

export function ActivityCard({ item }: { item: FeedItem }) {
  if (item.type === 'REVIEW' && item.review) {
    return <ReviewCard review={item.review} bookId={item.review.bookId} />
  }

  return (
    <article className="surface p-5 sm:p-6">
      <div className="flex gap-3">
        <Link to={`/users/${item.actor.id}`} aria-label={`Xem hồ sơ ${item.actor.displayName}`}>
          <Avatar src={item.actor.avatarUrl} name={item.actor.displayName} />
        </Link>
        <div className="min-w-0 flex-1">
          <p className="text-sm text-body">
            <Link
              to={`/users/${item.actor.id}`}
              className="font-semibold text-heading hover:text-accent-strong"
            >
              {item.actor.displayName}
            </Link>{' '}
            {item.type === 'REVIEW' ? 'đã chia sẻ một đánh giá' : activityLabel[item.type]}
          </p>
          <p className="mt-1 text-xs text-muted">{formatRelativeTime(item.createdAt)}</p>

          {item.book ? (
            <Link
              to={`/books/${item.book.id}`}
              className="mt-4 block rounded-xl bg-surface-muted p-4 transition-colors hover:bg-accent-soft"
            >
              <p className="font-semibold text-heading">{item.book.title}</p>
              <p className="mt-1 text-sm text-muted">{item.book.author?.name}</p>
              {typeof item.progressPercent === 'number' ? (
                <div className="mt-3">
                  <div className="mb-1 flex items-center justify-between text-xs font-semibold text-muted">
                    <span>Tiến độ</span>
                    <span>{Math.round(item.progressPercent)}%</span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded-full bg-border">
                    <span
                      className="block h-full rounded-full bg-accent"
                      style={{ width: `${Math.min(100, Math.max(0, item.progressPercent))}%` }}
                    />
                  </div>
                </div>
              ) : null}
            </Link>
          ) : null}

          {item.challenge ? (
            <Link
              to={`/challenges/${item.challenge.id}`}
              className="mt-4 block rounded-xl bg-surface-muted p-4 transition-colors hover:bg-accent-soft"
            >
              <p className="font-semibold text-heading">{item.challenge.title}</p>
              <p className="mt-1 text-sm text-muted">
                {item.challenge.currentBooks}/{item.challenge.goalBooks} cuốn
              </p>
            </Link>
          ) : null}

          {item.club ? (
            <Link
              to={`/clubs/${item.club.id}`}
              className="mt-4 block rounded-xl bg-surface-muted p-4 transition-colors hover:bg-accent-soft"
            >
              <p className="font-semibold text-heading">{item.club.name}</p>
              {item.content ? (
                <p className="mt-2 whitespace-pre-line break-words text-sm leading-6 text-body">
                  {item.content}
                </p>
              ) : null}
            </Link>
          ) : null}
        </div>
      </div>
    </article>
  )
}
