import { BookOpenText, Flag, UsersThree } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'
import { ReviewCard } from '../../components/community/ReviewCard'
import { Avatar } from '../../components/ui/Avatar'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useFeed } from '../../hooks/useCommunity'
import { formatRelativeTime } from '../../lib/format'

export function FeedPage() {
  const feed = useFeed()

  return (
    <div className="container-page section-space">
      <div className="max-w-2xl">
        <p className="eyebrow">Cộng đồng</p>
        <h1 className="page-title mt-4">Những gì người đọc đang nghĩ tới.</h1>
        <p className="mt-3 leading-7 text-muted">
          Bài đánh giá, tiến độ và cột mốc mới từ những người bạn theo dõi.
        </p>
      </div>

      <div className="mt-10 grid gap-8 lg:grid-cols-[1fr_18rem]">
        <section>
          {feed.isLoading ? (
            <LoadingRows count={5} />
          ) : feed.isError ? (
            <ErrorState message="Không thể tải bảng tin." retry={() => void feed.refetch()} />
          ) : feed.data?.items.length ? (
            <div className="space-y-4">
              {feed.data.items.map((item) =>
                item.type === 'REVIEW' && item.review ? (
                  <ReviewCard key={item.id} review={item.review} bookId={item.review.bookId} />
                ) : (
                  <article key={item.id} className="surface p-5 sm:p-6">
                    <div className="flex gap-3">
                      <Link to={`/users/${item.actor.id}`}>
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
                          {item.type === 'READING_PROGRESS'
                            ? 'đã cập nhật tiến độ đọc'
                            : item.type === 'CHALLENGE'
                              ? 'đã đạt một cột mốc thử thách'
                              : 'đã đăng bài trong câu lạc bộ'}
                        </p>
                        <p className="mt-1 text-xs text-muted">{formatRelativeTime(item.createdAt)}</p>
                        {item.book ? (
                          <Link
                            to={`/books/${item.book.id}`}
                            className="mt-4 block rounded-xl bg-surface-muted p-4"
                          >
                            <p className="font-semibold text-heading">{item.book.title}</p>
                            <p className="mt-1 text-sm text-muted">{item.book.author?.name}</p>
                            {typeof item.progressPercent === 'number' ? (
                              <p className="mt-3 text-sm font-semibold text-accent-strong">
                                {Math.round(item.progressPercent)}% hoàn thành
                              </p>
                            ) : null}
                          </Link>
                        ) : null}
                        {item.challenge ? (
                          <Link to="/challenges" className="mt-4 block rounded-xl bg-surface-muted p-4">
                            <p className="font-semibold text-heading">{item.challenge.title}</p>
                            <p className="mt-1 text-sm text-muted">
                              {item.challenge.currentBooks}/{item.challenge.goalBooks} cuốn
                            </p>
                          </Link>
                        ) : null}
                        {item.club ? (
                          <Link to={`/clubs/${item.club.id}`} className="mt-4 block rounded-xl bg-surface-muted p-4">
                            <p className="font-semibold text-heading">{item.club.name}</p>
                            {item.content ? <p className="mt-2 text-sm leading-6 text-body">{item.content}</p> : null}
                          </Link>
                        ) : null}
                      </div>
                    </div>
                  </article>
                ),
              )}
            </div>
          ) : (
            <EmptyState
              icon={UsersThree}
              title="Bảng tin của bạn còn yên ắng"
              description="Theo dõi những người đọc thú vị để thấy bài đánh giá và hành trình mới của họ."
              action={
                <Link to="/explore" className="button button-primary button-md">
                  Khám phá cộng đồng
                </Link>
              }
            />
          )}
        </section>

        <aside className="space-y-4">
          <div className="surface p-5">
            <BookOpenText size={23} weight="duotone" className="text-accent-strong" />
            <h2 className="mt-4 font-semibold text-heading">Viết từ trải nghiệm thật</h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Đánh giá xuất hiện từ trang chi tiết sách để cuộc trò chuyện luôn có ngữ cảnh.
            </p>
            <Link to="/books" className="mt-4 inline-block text-sm font-semibold text-accent-strong hover:underline">
              Chọn một cuốn sách
            </Link>
          </div>
          <div className="surface p-5">
            <Flag size={23} weight="duotone" className="text-accent-strong" />
            <h2 className="mt-4 font-semibold text-heading">Tôn trọng người đọc khác</h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Tránh tiết lộ nội dung quan trọng và tập trung vào góc nhìn của riêng bạn.
            </p>
          </div>
        </aside>
      </div>
    </div>
  )
}
