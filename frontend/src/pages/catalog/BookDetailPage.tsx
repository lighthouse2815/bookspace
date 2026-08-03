import {
  BookOpen,
  BookmarkSimple,
  CalendarBlank,
  Check,
  Copy,
  ArrowSquareOut,
  NotePencil,
  Play,
  Trash,
} from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { BookCover } from '../../components/books/BookCover'
import { BookListPickerDialog } from '../../components/book-lists/BookListPickerDialog'
import { ReviewCard } from '../../components/community/ReviewCard'
import { Button } from '../../components/ui/Button'
import { Rating } from '../../components/ui/Rating'
import { ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useBook } from '../../hooks/useCatalog'
import { useCreateReview, useReviews } from '../../hooks/useCommunity'
import {
  useAddToLibrary,
  useLibrary,
  useRemoveFromLibrary,
  useUpdateLibrary,
} from '../../hooks/useReading'
import { errorMessage } from '../../lib/api'
import { shelfLabel } from '../../lib/format'
import type { Shelf } from '../../types/domain'

export function BookDetailPage() {
  const { id } = useParams()
  const { isAuthenticated, user } = useAuth()
  const { showToast } = useToast()
  const book = useBook(id)
  const reviews = useReviews(id)
  const library = useLibrary(undefined, isAuthenticated)
  const addToLibrary = useAddToLibrary()
  const updateLibrary = useUpdateLibrary()
  const removeFromLibrary = useRemoveFromLibrary()
  const createReview = useCreateReview(id ?? '')
  const [rating, setRating] = useState(0)
  const [reviewContent, setReviewContent] = useState('')
  const [containsSpoilers, setContainsSpoilers] = useState(false)
  const [listPickerOpen, setListPickerOpen] = useState(false)

  if (book.isLoading) {
    return (
      <div className="container-page section-space grid animate-pulse gap-10 md:grid-cols-[16rem_1fr]">
        <div className="aspect-[2/3] rounded-2xl bg-surface-muted" />
        <div className="space-y-5">
          <div className="h-8 w-4/5 rounded bg-surface-muted" />
          <div className="h-4 w-2/5 rounded bg-surface-muted" />
          <div className="h-32 rounded bg-surface-muted" />
        </div>
      </div>
    )
  }

  if (book.isError || !book.data) {
    return (
      <div className="container-page section-space">
        <ErrorState message="Không thể tải thông tin cuốn sách." retry={() => void book.refetch()} />
      </div>
    )
  }

  const entry = library.data?.items.find((item) => item.bookId === book.data.id)
  const ownReview = reviews.data?.items.find((review) => review.user.id === user?.id)

  const setShelf = async (shelf: Shelf) => {
    try {
      if (entry) await updateLibrary.mutateAsync({ id: entry.id, input: { shelf } })
      else await addToLibrary.mutateAsync({ bookId: book.data.id, shelf })
      showToast(`Đã chuyển vào kệ ${shelfLabel(shelf).toLowerCase()}`, 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật thư viện'), 'error')
    }
  }

  const remove = async () => {
    if (!entry) return
    try {
      await removeFromLibrary.mutateAsync(entry.id)
      showToast('Đã xóa sách khỏi thư viện', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  const submitReview = async (event: FormEvent) => {
    event.preventDefault()
    if (!rating) {
      showToast('Hãy chọn số sao cho cuốn sách', 'error')
      return
    }
    if (reviewContent.trim().length < 20) {
      showToast('Đánh giá cần ít nhất 20 ký tự', 'error')
      return
    }
    try {
      await createReview.mutateAsync({
        rating,
        content: reviewContent.trim(),
        containsSpoilers,
      })
      setRating(0)
      setReviewContent('')
      setContainsSpoilers(false)
      showToast('Đánh giá của bạn đã được đăng', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể đăng đánh giá'), 'error')
    }
  }

  const copyLink = async () => {
    await navigator.clipboard.writeText(window.location.href)
    showToast('Đã sao chép liên kết cuốn sách', 'success')
  }

  return (
    <div className="container-page section-space">
      <div className="grid gap-10 md:grid-cols-[15rem_1fr] lg:grid-cols-[18rem_1fr] lg:gap-14">
        <div>
          <BookCover
            src={book.data.coverImageUrl}
            title={book.data.title}
            className="aspect-[2/3] w-full rounded-2xl shadow-cover"
          />
          <Button
            variant="secondary"
            className="mt-4 w-full"
            icon={<Copy size={17} />}
            onClick={copyLink}
          >
            Sao chép liên kết
          </Button>
        </div>
        <div className="min-w-0">
          <div className="flex flex-wrap gap-2">
            {book.data.categories?.map((category) => (
              <Link
                key={category.id}
                to={`/books?categoryId=${category.id}`}
                className="rounded-full bg-accent-soft px-3 py-1 text-xs font-semibold text-accent-strong"
              >
                {category.name}
              </Link>
            ))}
          </div>
          <h1 className="mt-5 text-4xl font-bold leading-tight tracking-tight text-heading sm:text-5xl">
            {book.data.title}
          </h1>
          <p className="mt-3 text-lg text-muted">{book.data.author?.name || 'Tác giả đang cập nhật'}</p>
          <div className="mt-5 flex flex-wrap items-center gap-3">
            <Rating value={book.data.averageRating ?? 0} />
            <span className="text-sm font-semibold text-heading">
              {(book.data.averageRating ?? 0).toFixed(1)}
            </span>
            <span className="text-sm text-muted">{book.data.reviewCount ?? 0} đánh giá</span>
          </div>
          <p className="mt-7 max-w-3xl whitespace-pre-line text-base leading-8 text-body">
            {book.data.description || 'Mô tả cuốn sách đang được cập nhật.'}
          </p>
          <dl className="mt-8 grid gap-4 border-y border-border py-6 sm:grid-cols-3">
            <div>
              <dt className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-muted">
                <BookOpen size={16} />
                Số trang
              </dt>
              <dd className="mt-2 font-semibold text-heading">{book.data.pageCount ?? 'Chưa rõ'}</dd>
            </div>
            <div>
              <dt className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-muted">
                <CalendarBlank size={16} />
                Năm xuất bản
              </dt>
              <dd className="mt-2 font-semibold text-heading">{book.data.publishedYear ?? 'Chưa rõ'}</dd>
            </div>
            <div>
              <dt className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-muted">
                <BookmarkSimple size={16} />
                ISBN
              </dt>
              <dd className="mt-2 font-semibold text-heading">{book.data.isbn || 'Chưa rõ'}</dd>
            </div>
          </dl>

          {isAuthenticated ? (
            <div className="mt-8">
              <p className="text-sm font-semibold text-heading">
                {entry ? `Đang ở kệ: ${shelfLabel(entry.shelf)}` : 'Thêm vào thư viện cá nhân'}
              </p>
              <div className="mt-3 flex flex-wrap gap-2">
                {(['WANT_TO_READ', 'READING', 'READ'] as Shelf[]).map((shelf) => (
                  <Button
                    key={shelf}
                    variant={entry?.shelf === shelf ? 'primary' : 'secondary'}
                    size="sm"
                    loading={
                      (addToLibrary.isPending || updateLibrary.isPending) &&
                      entry?.shelf !== shelf
                    }
                    icon={entry?.shelf === shelf ? <Check size={16} /> : undefined}
                    onClick={() => void setShelf(shelf)}
                  >
                    {shelfLabel(shelf)}
                  </Button>
                ))}
                {entry ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    icon={<Trash size={16} />}
                    loading={removeFromLibrary.isPending}
                    onClick={() => void remove()}
                  >
                    Xóa khỏi thư viện
                  </Button>
                ) : null}
                <Link to={`/notes?bookId=${book.data.id}`} className="button button-secondary button-sm">
                  <NotePencil size={16} />
                  Ghi chú
                </Link>
                <Button
                  variant="secondary"
                  size="sm"
                  icon={<BookmarkSimple size={16} />}
                  onClick={() => setListPickerOpen(true)}
                >
                  Lưu vào bộ sưu tập
                </Button>
                {entry?.shelf === 'READING' || book.data.shelf === 'READING' ? (
                  <Link
                    to={`/journal?bookId=${book.data.id}`}
                    className="button button-primary button-sm"
                  >
                    <Play size={16} weight="fill" />
                    Bắt đầu phiên đọc
                  </Link>
                ) : null}
              </div>
            </div>
          ) : (
            <div className="mt-8 rounded-2xl border border-border bg-surface p-5">
              <p className="font-semibold text-heading">Muốn lưu cuốn sách này?</p>
              <p className="mt-1 text-sm text-muted">Đăng nhập để thêm sách và theo dõi tiến độ đọc.</p>
              <Link to="/login" className="button button-primary button-sm mt-4">
                Đăng nhập
              </Link>
            </div>
          )}
          <BookListPickerDialog
            bookId={book.data.id}
            open={listPickerOpen}
            onClose={() => setListPickerOpen(false)}
          />
          {book.data.externalOffer?.purchaseUrl ? (
            <div className="mt-6 border-t border-border pt-6">
              <a
                href={book.data.externalOffer.purchaseUrl}
                target="_blank"
                rel="noreferrer"
                className="button button-secondary button-md"
              >
                Xem tại {book.data.externalOffer.providerName}
                <ArrowSquareOut size={17} />
              </a>
              <p className="mt-2 text-xs text-muted">
                Liên kết đến nhà cung cấp bên ngoài. BookSpace vẫn hoạt động độc lập.
              </p>
            </div>
          ) : null}
        </div>
      </div>

      <section className="mt-16 grid gap-10 border-t border-border pt-12 lg:grid-cols-[22rem_1fr]">
        <div>
          <h2 className="text-2xl font-bold tracking-tight text-heading">Góc nhìn người đọc</h2>
          <p className="mt-3 text-sm leading-6 text-muted">
            Chia sẻ điều cuốn sách đã gợi mở cho bạn. Tập trung vào trải nghiệm đọc, không tiết lộ nội dung quan trọng.
          </p>
          {isAuthenticated && !reviews.isLoading && !ownReview ? (
            <form onSubmit={submitReview} className="mt-6 surface p-5">
              <label className="text-sm font-semibold text-heading">Đánh giá của bạn</label>
              <div className="mt-2">
                <Rating value={rating} onChange={setRating} size={23} />
              </div>
              <label htmlFor="review-content" className="mt-5 block text-sm font-semibold text-heading">
                Nội dung
              </label>
              <textarea
                id="review-content"
                value={reviewContent}
                onChange={(event) => setReviewContent(event.target.value)}
                className="input mt-2 min-h-36 resize-y"
                maxLength={3000}
                placeholder="Điều gì ở cuốn sách này ở lại với bạn?"
              />
              <label className="mt-3 flex cursor-pointer items-center gap-2 text-sm text-muted">
                <input
                  type="checkbox"
                  checked={containsSpoilers}
                  onChange={(event) => setContainsSpoilers(event.target.checked)}
                />
                Nội dung có tiết lộ tình tiết quan trọng
              </label>
              <div className="mt-3 flex items-center justify-between gap-3">
                <span className="text-xs text-muted">{reviewContent.length}/3000</span>
                <Button
                  type="submit"
                  size="sm"
                  loading={createReview.isPending}
                  icon={<NotePencil size={16} />}
                >
                  Đăng đánh giá
                </Button>
              </div>
            </form>
          ) : isAuthenticated && ownReview ? (
            <div className="mt-6 rounded-2xl border border-accent/20 bg-accent/5 p-5 text-sm leading-6 text-muted">
              Bạn đã đăng đánh giá cho cuốn sách này. Dùng nút <strong className="text-heading">Sửa</strong> hoặc{' '}
              <strong className="text-heading">Xóa</strong> trên đánh giá của bạn để quản lý nội dung.
            </div>
          ) : null}
        </div>
        <div>
          {reviews.isLoading ? (
            <LoadingRows count={4} />
          ) : reviews.isError ? (
            <ErrorState message="Không thể tải đánh giá." retry={() => void reviews.refetch()} />
          ) : reviews.data?.items.length ? (
            <div className="space-y-4">
              {reviews.data.items.map((review) => (
                <ReviewCard key={review.id} review={review} bookId={book.data.id} />
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <NotePencil size={30} className="text-accent-strong" />
              <h3 className="mt-4 font-semibold text-heading">Chưa có đánh giá</h3>
              <p className="mt-2 text-sm text-muted">Hãy là người đầu tiên chia sẻ góc nhìn về cuốn sách.</p>
            </div>
          )}
        </div>
      </section>
    </div>
  )
}
