import { Tag, UserCircle, type Icon } from '@phosphor-icons/react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { CatalogFollowButton } from '../../components/catalog/CatalogFollowButton'
import { Avatar } from '../../components/ui/Avatar'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useCatalogFollowing } from '../../hooks/useCatalog'
import { errorMessage } from '../../lib/api'

export function CatalogFollowingPage() {
  const following = useCatalogFollowing()

  return (
    <div className="container-page section-space">
      <p className="eyebrow">Sở thích catalog</p>
      <h1 className="page-title mt-4">Nội dung đang theo dõi</h1>
      <p className="mt-4 max-w-2xl text-base leading-7 text-muted">
        Các tác giả và thể loại ở đây được ưu tiên trong gợi ý sách. Khi có sách mới,
        BookSpace sẽ gửi thông báo theo tùy chọn của bạn.
      </p>

      {following.isLoading ? (
        <div className="mt-10"><LoadingRows count={4} /></div>
      ) : null}
      {following.isError ? (
        <div className="mt-10">
          <ErrorState
            message={errorMessage(following.error, 'Không thể tải nội dung đang theo dõi.')}
            retry={() => void following.refetch()}
          />
        </div>
      ) : null}

      {following.data ? (
        <div className="mt-10 grid gap-10 lg:grid-cols-2">
          <FollowingSection
            title="Tác giả"
            emptyTitle="Bạn chưa theo dõi tác giả nào"
            emptyDescription="Khám phá danh sách tác giả và chọn những người bạn muốn cập nhật."
            browseTo="/authors"
            browseLabel="Khám phá tác giả"
            icon={UserCircle}
            hasItems={following.data.authors.length > 0}
          >
            {following.data.authors.map((author) => (
              <article key={author.id} className="surface flex items-center gap-4 p-4">
                <Avatar src={author.avatarUrl} name={author.name} size="md" />
                <div className="min-w-0 flex-1">
                  <Link to={`/authors/${author.id}`} className="font-bold text-heading hover:text-accent-strong">
                    {author.name}
                  </Link>
                  <p className="mt-1 text-xs font-semibold text-muted">{author.bookCount ?? 0} cuốn sách</p>
                </div>
                <CatalogFollowButton kind="author" id={author.id} compact />
              </article>
            ))}
          </FollowingSection>

          <FollowingSection
            title="Thể loại"
            emptyTitle="Bạn chưa theo dõi thể loại nào"
            emptyDescription="Chọn các chủ đề đọc để gợi ý sách phản ánh đúng sở thích hơn."
            browseTo="/categories"
            browseLabel="Khám phá thể loại"
            icon={Tag}
            hasItems={following.data.categories.length > 0}
          >
            {following.data.categories.map((category) => (
              <article key={category.id} className="surface flex items-center gap-4 p-4">
                <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                  <Tag size={21} weight="duotone" aria-hidden />
                </span>
                <div className="min-w-0 flex-1">
                  <Link to={`/categories/${category.id}`} className="font-bold text-heading hover:text-accent-strong">
                    {category.name}
                  </Link>
                  <p className="mt-1 text-xs font-semibold text-muted">{category.bookCount ?? 0} cuốn sách</p>
                </div>
                <CatalogFollowButton kind="category" id={category.id} compact />
              </article>
            ))}
          </FollowingSection>
        </div>
      ) : null}
    </div>
  )
}

function FollowingSection({
  title,
  emptyTitle,
  emptyDescription,
  browseTo,
  browseLabel,
  icon,
  hasItems,
  children,
}: {
  title: string
  emptyTitle: string
  emptyDescription: string
  browseTo: string
  browseLabel: string
  icon: Icon
  hasItems: boolean
  children: ReactNode
}) {
  return (
    <section aria-labelledby={`following-${title}`}>
      <div className="flex items-end justify-between gap-3">
        <h2 id={`following-${title}`} className="text-2xl font-bold tracking-tight text-heading">{title}</h2>
        <Link to={browseTo} className="text-sm font-semibold text-accent-strong">{browseLabel}</Link>
      </div>
      <div className="mt-5 space-y-3">
        {hasItems ? children : (
          <EmptyState
            title={emptyTitle}
            description={emptyDescription}
            icon={icon}
            action={<Link to={browseTo} className="button button-secondary button-sm">{browseLabel}</Link>}
          />
        )}
      </div>
    </section>
  )
}
