import { BookOpenText, Flag, UsersThree } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'
import { ActivityCard } from '../../components/community/ActivityCard'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useFeed } from '../../hooks/useCommunity'

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
              {feed.data.items.map((item) => (
                <ActivityCard key={`${item.type}-${item.id}`} item={item} />
              ))}
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
