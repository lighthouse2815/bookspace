import {
  ChatCircle,
  Eye,
  Heart,
  PaperPlaneTilt,
  PencilSimple,
  Trash,
  WarningCircle,
} from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import type { Review } from '../../types/domain'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useCommentReview,
  useDeleteReview,
  useDeleteReviewComment,
  useLikeReview,
  useReviewComments,
  useUpdateReview,
} from '../../hooks/useCommunity'
import { Avatar } from '../ui/Avatar'
import { Button } from '../ui/Button'
import { Rating } from '../ui/Rating'
import { ReportContentButton } from '../moderation/ReportContentButton'

export function ReviewCard({ review, bookId }: { review: Review; bookId?: string }) {
  const [showComments, setShowComments] = useState(false)
  const [showSpoiler, setShowSpoiler] = useState(!review.containsSpoilers)
  const [comment, setComment] = useState('')
  const [isEditing, setIsEditing] = useState(false)
  const [editRating, setEditRating] = useState(review.rating)
  const [editContent, setEditContent] = useState(review.content)
  const [editContainsSpoilers, setEditContainsSpoilers] = useState(review.containsSpoilers)
  const { isAuthenticated, user } = useAuth()
  const { showToast } = useToast()
  const like = useLikeReview(bookId)
  const addComment = useCommentReview(bookId)
  const updateReview = useUpdateReview(bookId)
  const deleteReview = useDeleteReview(bookId)
  const deleteComment = useDeleteReviewComment(bookId)
  const comments = useReviewComments(review.id, showComments)
  const canEditReview = user?.id === review.user.id
  const canDeleteReview = canEditReview || user?.role === 'ADMIN'

  const submitComment = async (event: FormEvent) => {
    event.preventDefault()
    if (!comment.trim()) return
    try {
      await addComment.mutateAsync({ reviewId: review.id, content: comment.trim() })
      setComment('')
      showToast('Bình luận đã được đăng', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể đăng bình luận'), 'error')
    }
  }

  const startEditing = () => {
    setEditRating(review.rating)
    setEditContent(review.content)
    setEditContainsSpoilers(review.containsSpoilers)
    setIsEditing(true)
  }

  const submitReviewUpdate = async (event: FormEvent) => {
    event.preventDefault()
    if (editRating < 1 || editContent.trim().length < 20) {
      showToast('Đánh giá cần 1–5 sao và ít nhất 20 ký tự', 'error')
      return
    }
    try {
      await updateReview.mutateAsync({
        reviewId: review.id,
        rating: editRating,
        content: editContent.trim(),
        containsSpoilers: editContainsSpoilers,
      })
      setShowSpoiler(!editContainsSpoilers)
      setIsEditing(false)
      showToast('Đã cập nhật đánh giá', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật đánh giá'), 'error')
    }
  }

  const removeReview = async () => {
    if (!window.confirm('Xóa đánh giá này? Hành động này không thể hoàn tác.')) return
    try {
      await deleteReview.mutateAsync(review.id)
      showToast('Đã xóa đánh giá', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa đánh giá'), 'error')
    }
  }

  const removeComment = async (commentId: string) => {
    try {
      await deleteComment.mutateAsync({ reviewId: review.id, commentId })
      showToast('Đã xóa bình luận', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa bình luận'), 'error')
    }
  }

  return (
    <article className="surface p-5 sm:p-6">
      <div className="flex items-start gap-3">
        <Link to={`/users/${review.user.id}`} aria-label={`Xem hồ sơ ${review.user.displayName}`}>
          <Avatar src={review.user.avatarUrl} name={review.user.displayName} />
        </Link>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <Link
                to={`/users/${review.user.id}`}
                className="font-semibold text-heading hover:text-accent-strong"
              >
                {review.user.displayName}
              </Link>
              <p className="text-xs text-muted">{formatRelativeTime(review.createdAt)}</p>
            </div>
            <div className="flex items-center gap-1">
              {isEditing ? <Rating value={editRating} onChange={setEditRating} size={16} /> : <Rating value={review.rating} size={16} />}
              {canEditReview && !isEditing ? (
                <Button type="button" variant="ghost" size="sm" icon={<PencilSimple size={16} />} onClick={startEditing}>
                  Sửa
                </Button>
              ) : null}
              {canDeleteReview ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  icon={<Trash size={16} />}
                  loading={deleteReview.isPending}
                  onClick={() => void removeReview()}
                >
                  Xóa
                </Button>
              ) : null}
              <ReportContentButton
                targetType="REVIEW"
                targetId={review.id}
                ownerId={review.user.id}
                label="Báo cáo đánh giá"
              />
            </div>
          </div>
          {review.book ? (
            <Link
              to={`/books/${review.book.id}`}
              className="mt-3 inline-block text-sm font-semibold text-accent-strong hover:underline"
            >
              {review.book.title}
            </Link>
          ) : null}
          {isEditing ? (
            <form onSubmit={submitReviewUpdate} className="mt-3 space-y-3">
              <textarea
                value={editContent}
                onChange={(event) => setEditContent(event.target.value)}
                className="input min-h-28 w-full resize-y"
                maxLength={5000}
                aria-label="Nội dung đánh giá"
              />
              <label className="flex items-center gap-2 text-sm text-muted">
                <input
                  type="checkbox"
                  checked={editContainsSpoilers}
                  onChange={(event) => setEditContainsSpoilers(event.target.checked)}
                />
                Đánh dấu nội dung tiết lộ tình tiết
              </label>
              <div className="flex flex-wrap justify-end gap-2">
                <Button type="button" variant="secondary" size="sm" onClick={() => setIsEditing(false)}>
                  Hủy
                </Button>
                <Button type="submit" size="sm" loading={updateReview.isPending}>
                  Lưu đánh giá
                </Button>
              </div>
            </form>
          ) : review.containsSpoilers && !showSpoiler ? (
            <div className="mt-3 rounded-xl border border-amber-500/25 bg-amber-500/10 p-4">
              <div className="flex items-start gap-2.5">
                <WarningCircle size={20} className="mt-0.5 shrink-0 text-amber-600 dark:text-amber-300" />
                <div>
                  <p className="text-sm font-semibold text-heading">Đánh giá này có tiết lộ nội dung</p>
                  <p className="mt-1 text-sm leading-6 text-muted">Chỉ mở khi bạn sẵn sàng đọc phần có thể làm lộ tình tiết.</p>
                  <button
                    type="button"
                    className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline"
                    onClick={() => setShowSpoiler(true)}
                  >
                    <Eye size={16} /> Hiển thị nội dung
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <p className="mt-3 whitespace-pre-line text-sm leading-6 text-body">{review.content}</p>
          )}
          <div className="mt-4 flex items-center gap-1">
            <button
              type="button"
              className={`reaction-button ${review.likedByCurrentUser ? 'reaction-active' : ''}`}
              disabled={!isAuthenticated || like.isPending}
              onClick={() =>
                like
                  .mutateAsync({ reviewId: review.id, liked: review.likedByCurrentUser })
                  .catch((error) => showToast(errorMessage(error), 'error'))
              }
              aria-label={review.likedByCurrentUser ? 'Bỏ thích đánh giá' : 'Thích đánh giá'}
            >
              <Heart size={18} weight={review.likedByCurrentUser ? 'fill' : 'regular'} />
              <span>{review.likeCount}</span>
            </button>
            <button
              type="button"
              className="reaction-button"
              onClick={() => setShowComments((value) => !value)}
              aria-expanded={showComments}
            >
              <ChatCircle size={18} />
              <span>{review.commentCount}</span>
            </button>
          </div>
          {showComments ? (
            <div className="mt-4 border-t border-border pt-4">
              {comments.isLoading ? <p className="mb-4 text-sm text-muted">Đang tải bình luận...</p> : null}
              {comments.isError ? (
                <p className="mb-4 text-sm text-red-600">Không thể tải bình luận. Hãy thử lại.</p>
              ) : null}
              {comments.data?.items.length ? (
                <div className="mb-4 space-y-3">
                  {comments.data.items.map((item) => (
                    <div key={item.id} className="flex gap-2.5">
                      <Avatar src={item.user.avatarUrl} name={item.user.displayName} size="sm" />
                      <div className="flex min-w-0 flex-1 items-start gap-1">
                        <div className="min-w-0 flex-1 rounded-xl bg-surface-muted px-3 py-2">
                          <p className="text-xs font-semibold text-heading">{item.user.displayName}</p>
                          <p className="mt-0.5 text-sm text-body">{item.content}</p>
                        </div>
                        {user?.id === item.user.id || user?.role === 'ADMIN' ? (
                          <button
                            type="button"
                            className="reaction-button shrink-0"
                            aria-label="Xóa bình luận"
                            disabled={deleteComment.isPending}
                            onClick={() => void removeComment(item.id)}
                          >
                            <Trash size={16} />
                          </button>
                        ) : null}
                        <ReportContentButton
                          targetType="REVIEW_COMMENT"
                          targetId={item.id}
                          ownerId={item.user.id}
                          label="Báo cáo bình luận"
                          compact
                        />
                      </div>
                    </div>
                  ))}
                </div>
              ) : null}
              {!comments.isLoading && !comments.isError && comments.data?.items.length === 0 ? (
                <p className="mb-4 text-sm text-muted">Chưa có bình luận. Hãy bắt đầu cuộc trò chuyện.</p>
              ) : null}
              {isAuthenticated ? (
                <form onSubmit={submitComment} className="flex items-end gap-2">
                  <label className="sr-only" htmlFor={`comment-${review.id}`}>
                    Viết bình luận
                  </label>
                  <textarea
                    id={`comment-${review.id}`}
                    value={comment}
                    onChange={(event) => setComment(event.target.value)}
                    className="input min-h-10 flex-1 resize-none py-2"
                    rows={1}
                    maxLength={2000}
                    placeholder="Viết bình luận..."
                  />
                  <Button
                    type="submit"
                    size="sm"
                    loading={addComment.isPending}
                    icon={<PaperPlaneTilt size={16} />}
                    aria-label="Đăng bình luận"
                  >
                    Đăng
                  </Button>
                </form>
              ) : (
                <Link to="/login" className="text-sm font-semibold text-accent-strong hover:underline">
                  Đăng nhập để bình luận
                </Link>
              )}
            </div>
          ) : null}
        </div>
      </div>
    </article>
  )
}
