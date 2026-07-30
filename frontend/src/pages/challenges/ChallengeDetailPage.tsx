import { ArrowLeft, CalendarBlank, CheckCircle, Flag, Users } from '@phosphor-icons/react'
import axios from 'axios'
import { Link, useParams } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useChallenge, useChallengeMembership } from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { ApiEnvelope } from '../../types/api'

function challengeErrorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 404) {
      return 'Không tìm thấy thử thách hoặc thử thách chưa được xuất bản.'
    }

    const payload = error.response?.data as Partial<ApiEnvelope<unknown>> | undefined
    if (payload?.message) return payload.message

    return error.response
      ? 'Không thể tải chi tiết thử thách. Vui lòng thử lại.'
      : 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối và thử lại.'
  }

  return 'Không thể tải chi tiết thử thách. Vui lòng thử lại.'
}

export function ChallengeDetailPage() {
  const { id = '' } = useParams()
  const { isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const challenge = useChallenge(id)
  const membership = useChallengeMembership(id, Boolean(challenge.data?.isJoined))

  const toggle = async () => {
    if (!challenge.data) return
    try {
      await membership.mutateAsync()
      showToast(challenge.data.isJoined ? 'Đã rời thử thách' : 'Đã tham gia thử thách', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  if (!id) {
    return (
      <div className="container-page section-space">
        <EmptyState
          icon={Flag}
          title="Thiếu mã thử thách"
          description="Đường dẫn này không chứa mã thử thách hợp lệ."
          action={<Link to="/challenges" className="button button-secondary button-md">Xem danh sách</Link>}
        />
      </div>
    )
  }

  if (challenge.isPending || challenge.isLoading) {
    return <div className="container-page section-space"><LoadingRows count={3} /></div>
  }

  if (challenge.isError) {
    return (
      <div className="container-page section-space">
        <ErrorState
          message={challengeErrorMessage(challenge.error)}
          retry={() => void challenge.refetch()}
        />
      </div>
    )
  }

  if (!challenge.data) {
    return (
      <div className="container-page section-space">
        <EmptyState
          icon={Flag}
          title="Không có dữ liệu thử thách"
          description="Hãy quay lại danh sách để chọn một thử thách khác."
        />
      </div>
    )
  }

  const item = challenge.data
  const percentage = (item.currentBooks / Math.max(item.goalBooks, 1)) * 100

  return (
    <div className="container-page section-space">
      <Link to="/challenges" className="inline-flex items-center gap-2 text-sm font-semibold text-muted hover:text-heading">
        <ArrowLeft size={17} /> Tất cả thử thách
      </Link>
      <article className="surface mt-6 overflow-hidden">
        <div
          className="min-h-64 bg-cover bg-center p-8 md:p-12"
          style={item.coverImageUrl
            ? { backgroundImage: `linear-gradient(90deg,rgba(2,6,23,.82),rgba(2,6,23,.28)),url("${item.coverImageUrl}")` }
            : { backgroundImage: 'linear-gradient(135deg,#052e2b,#134e4a)' }}
        >
          <p className="eyebrow text-emerald-200">Thử thách đọc sách</p>
          <h1 className="mt-4 max-w-3xl text-3xl font-bold text-white md:text-5xl">{item.title}</h1>
          <div className="mt-6 flex flex-wrap gap-5 text-sm text-slate-200">
            <span className="inline-flex items-center gap-2"><CalendarBlank size={18} /> {formatDate(item.startDate)} – {formatDate(item.endDate)}</span>
            <span className="inline-flex items-center gap-2"><Users size={18} /> {item.participantCount} người tham gia</span>
          </div>
        </div>
        <div className="grid gap-8 p-6 md:grid-cols-[1fr_20rem] md:p-10">
          <div>
            <h2 className="text-xl font-bold text-heading">Về thử thách</h2>
            <p className="mt-3 whitespace-pre-line leading-7 text-muted">
              {item.description || 'Thử thách chưa có mô tả chi tiết.'}
            </p>
          </div>
          <aside className="rounded-2xl bg-surface-muted p-5">
            <Progress
              value={percentage}
              label={item.isJoined
                ? `${item.currentBooks}/${item.goalBooks} cuốn đã hoàn thành`
                : `Mục tiêu ${item.goalBooks} cuốn`}
            />
            {item.isJoined ? (
              <p className="mt-3 text-sm leading-6 text-muted">
                Tiến độ được tự động tính từ sách bạn đánh dấu đã đọc trong thời gian thử thách.
              </p>
            ) : null}
            <div className="mt-5">
              {isAuthenticated ? (
                <Button
                  className="w-full"
                  variant={item.isJoined ? 'secondary' : 'primary'}
                  loading={membership.isPending}
                  icon={item.isJoined ? <CheckCircle size={18} /> : <Flag size={18} />}
                  onClick={() => void toggle()}
                >
                  {item.isJoined ? 'Rời thử thách' : 'Tham gia thử thách'}
                </Button>
              ) : (
                <Link
                  to="/login"
                  state={{ from: `/challenges/${id}` }}
                  className="button button-primary button-md w-full"
                >
                  Đăng nhập để tham gia
                </Link>
              )}
            </div>
          </aside>
        </div>
      </article>
    </div>
  )
}
