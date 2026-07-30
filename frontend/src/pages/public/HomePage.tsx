import {
  ArrowRight,
  BookBookmark,
  Books,
  ChartLineUp,
  ChatCircleDots,
  Quotes,
  UsersThree,
} from '@phosphor-icons/react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { BookCard } from '../../components/books/BookCard'
import { ErrorState, LoadingGrid } from '../../components/ui/States'
import { catalogService } from '../../services/catalog.service'

export function HomePage() {
  const featured = useQuery({
    queryKey: ['catalog', 'featured'],
    queryFn: catalogService.featured,
  })

  return (
    <>
      <section className="container-page grid min-h-[calc(100dvh-4rem)] items-center gap-10 py-12 lg:grid-cols-[0.9fr_1.1fr] lg:py-16">
        <div className="max-w-xl">
          <p className="eyebrow">Đọc có hành trình</p>
          <h1 className="mt-5 text-5xl font-bold leading-[0.98] tracking-[-0.055em] text-heading sm:text-6xl lg:text-7xl">
            Mỗi trang sách để lại một dấu vết.
          </h1>
          <p className="mt-6 max-w-lg text-lg leading-8 text-muted">
            Ghi lại tiến độ, chia sẻ góc nhìn và gặp những người đọc cùng tần số trong một không gian của riêng bạn.
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <Link to="/register" className="button button-primary button-lg">
              Tạo không gian đọc
              <ArrowRight size={19} />
            </Link>
            <Link to="/explore" className="button button-secondary button-lg">
              Khám phá cộng đồng
            </Link>
          </div>
        </div>
        <div className="relative lg:pl-6">
          <div className="absolute -left-3 top-12 hidden h-28 w-28 rounded-full bg-accent-soft blur-3xl lg:block" />
          <img
            src="/images/bookspace-hero.png"
            alt="Sổ đọc sách, bút và chồng sách trên bàn cạnh cửa sổ."
            width={1536}
            height={1024}
            fetchPriority="high"
            className="relative aspect-[3/2] w-full rounded-2xl object-cover shadow-hero"
          />
        </div>
      </section>

      <section className="border-y border-border bg-surface">
        <div className="container-page grid gap-px bg-border sm:grid-cols-3">
          {[
            { value: 'Một nơi', label: 'cho toàn bộ hành trình đọc', icon: BookBookmark },
            { value: 'Đúng người', label: 'cho mỗi cuộc trò chuyện', icon: UsersThree },
            { value: 'Thật hơn', label: 'từng mục tiêu bạn hoàn thành', icon: ChartLineUp },
          ].map(({ value, label, icon: Icon }) => (
            <div key={value} className="bg-surface px-6 py-8 sm:px-8">
              <Icon size={24} weight="duotone" className="text-accent-strong" aria-hidden />
              <p className="mt-5 text-2xl font-bold tracking-tight text-heading">{value}</p>
              <p className="mt-1 text-sm text-muted">{label}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="section-space container-page">
        <div className="max-w-2xl">
          <p className="eyebrow">Được cộng đồng quan tâm</p>
          <h2 className="section-title mt-4">Bắt đầu từ một cuốn sách khiến bạn tò mò.</h2>
          <p className="section-copy mt-4">
            Những tựa sách đang được độc giả BookSpace đọc, đánh giá và thảo luận nhiều nhất.
          </p>
        </div>
        <div className="mt-10">
          {featured.isLoading ? (
            <LoadingGrid count={8} />
          ) : featured.isError ? (
            <ErrorState message="Danh sách nổi bật chưa tải được." retry={() => void featured.refetch()} />
          ) : featured.data?.items.length ? (
            <div className="book-grid">
              {featured.data.items.map((book) => (
                <BookCard key={book.id} book={book} />
              ))}
            </div>
          ) : (
            <p className="rounded-2xl border border-border p-6 text-sm text-muted">
              Catalog đang được cập nhật. Hãy quay lại sau để xem các tựa sách mới.
            </p>
          )}
        </div>
        <Link to="/books" className="mt-9 inline-flex items-center gap-2 font-semibold text-accent-strong hover:underline">
          Xem toàn bộ catalog
          <ArrowRight size={18} />
        </Link>
      </section>

      <section className="section-space bg-slate-950 text-slate-100 dark:bg-surface-muted dark:text-heading">
        <div className="container-page grid gap-12 lg:grid-cols-[0.85fr_1.15fr] lg:items-start">
          <div className="lg:sticky lg:top-28">
            <Quotes size={38} weight="fill" className="text-accent-strong" aria-hidden />
            <h2 className="mt-5 max-w-lg text-4xl font-bold tracking-tight sm:text-5xl">
              Không chỉ đếm sách. Hãy nhớ mình đã đổi khác thế nào.
            </h2>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            {[
              {
                icon: Books,
                title: 'Thư viện cá nhân',
                text: 'Sắp xếp sách muốn đọc, đang đọc và đã đọc. Tiến độ luôn ở đúng chỗ.',
              },
              {
                icon: BookBookmark,
                title: 'Nhật ký đọc',
                text: 'Ghi số trang, thời lượng và suy nghĩ sau mỗi phiên đọc.',
              },
              {
                icon: ChatCircleDots,
                title: 'Góc nhìn thật',
                text: 'Viết đánh giá, bình luận và trao đổi mà không bị chi phối bởi việc bán sách.',
              },
              {
                icon: UsersThree,
                title: 'Nhóm cùng tần số',
                text: 'Tham gia câu lạc bộ, cùng đọc một tựa sách và giữ cuộc trò chuyện đi xa.',
              },
            ].map(({ icon: Icon, title, text }) => (
              <article key={title} className="rounded-2xl border border-white/10 bg-white/[0.04] p-6 dark:border-border dark:bg-surface">
                <Icon size={25} weight="duotone" className="text-accent-strong" aria-hidden />
                <h3 className="mt-6 text-lg font-semibold">{title}</h3>
                <p className="mt-2 text-sm leading-6 text-slate-400 dark:text-muted">{text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="section-space container-page">
        <div className="relative overflow-hidden rounded-2xl border border-accent/20 bg-accent-soft px-6 py-12 sm:px-12">
          <div className="max-w-2xl">
            <h2 className="text-3xl font-bold tracking-tight text-heading sm:text-4xl">
              Cuốn sách tiếp theo của bạn đang ở đâu đó trong cộng đồng này.
            </h2>
            <p className="mt-4 max-w-xl leading-7 text-muted">
              BookSpace hoạt động độc lập, tập trung hoàn toàn vào trải nghiệm đọc và kết nối giữa những người yêu sách.
            </p>
            <Link to="/register" className="button button-primary button-lg mt-7">
              Tham gia BookSpace
              <ArrowRight size={19} />
            </Link>
          </div>
        </div>
      </section>
    </>
  )
}
