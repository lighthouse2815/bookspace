import {
  ArrowLeft,
  CalendarBlank,
  CheckCircle,
  Flag,
  Trophy,
  Users,
  UsersThree,
} from '@phosphor-icons/react'
import axios from 'axios'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useChallenge,
  useChallengeLeaderboard,
  useChallengeMembership,
} from '../../hooks/useSocialProduct'
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

function ChallengeLeaderboard({ challengeId }: { challengeId: string }) {
  const { isAuthenticated } = useAuth()
  const [page, setPage] = useState(1)
  const pageSize = 10
  const leaderboard = useChallengeLeaderboard(challengeId, page, pageSize)
  const totalPages = Math.max(leaderboard.data?.totalPages ?? 1, 1)

  useEffect(() => {
    if (leaderboard.data && page > totalPages) setPage(totalPages)
  }, [leaderboard.data, page, totalPages])

  return (
    <section
      className="surface mt-8 min-w-0 p-5 sm:p-7"
      aria-labelledby="challenge-leaderboard-heading"
    >
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="eyebrow">Cùng nhau chinh phục</p>
          <h2 id="challenge-leaderboard-heading" className="mt-2 text-2xl font-bold text-heading">
            Bảng xếp hạng
          </h2>
          <p className="mt-2 text-sm leading-6 text-muted">
            Theo dõi thứ hạng và tiến độ đọc của cộng đồng trong thử thách này.
          </p>
        </div>
        <Trophy size={32} weight="duotone" className="shrink-0 text-accent-strong" />
      </div>

      <div className="mt-6">
        {!isAuthenticated ? (
          <EmptyState
            icon={UsersThree}
            title="Đăng nhập để xem bảng xếp hạng"
            description="Bảng xếp hạng chỉ dành cho thành viên BookSpace đã đăng nhập."
            action={(
              <Link
                to="/login"
                state={{ from: `/challenges/${challengeId}` }}
                className="button button-primary button-md"
              >
                Đăng nhập
              </Link>
            )}
          />
        ) : leaderboard.isPending || leaderboard.isLoading ? (
          <LoadingRows count={4} />
        ) : leaderboard.isError ? (
          <ErrorState
            message="Không thể tải bảng xếp hạng thử thách."
            retry={() => void leaderboard.refetch()}
          />
        ) : leaderboard.data?.items.length ? (
          <ol className="space-y-3" aria-label="Bảng xếp hạng thử thách">
            {leaderboard.data.items.map((participant) => {
              const isCompleted = participant.currentBooks >= participant.targetBooks

              return (
                <li
                  key={participant.user.id}
                  aria-current={participant.isCurrentUser ? 'true' : undefined}
                  className={`grid min-w-0 grid-cols-[2.5rem_2.5rem_minmax(0,1fr)] items-center gap-3 rounded-2xl border p-3 sm:grid-cols-[3rem_2.5rem_minmax(0,1fr)] sm:p-4 ${
                    participant.isCurrentUser
                      ? 'border-accent/40 bg-accent-soft'
                      : 'border-transparent bg-surface-muted/55'
                  }`}
                >
                  <span
                    className={`grid h-10 w-10 place-items-center rounded-xl text-sm font-bold ${
                      participant.rank <= 3
                        ? 'bg-accent text-white'
                        : 'bg-surface text-muted'
                    }`}
                    aria-label={`Hạng ${participant.rank}`}
                  >
                    {participant.rank}
                  </span>
                  <Link
                    to={`/users/${participant.user.id}`}
                    aria-label={`Xem hồ sơ ${participant.user.displayName}`}
                  >
                    <Avatar
                      src={participant.user.avatarUrl}
                      name={participant.user.displayName}
                    />
                  </Link>
                  <div className="min-w-0">
                    <div className="flex min-w-0 flex-wrap items-center justify-between gap-x-3 gap-y-1">
                      <Link
                        to={`/users/${participant.user.id}`}
                        className="min-w-0 truncate text-sm font-semibold text-heading hover:text-accent-strong"
                      >
                        {participant.user.displayName}
                        {participant.isCurrentUser ? ' · Bạn' : ''}
                      </Link>
                      <span
                        className={`inline-flex shrink-0 items-center gap-1 text-xs font-semibold ${
                          isCompleted ? 'text-accent-strong' : 'text-muted'
                        }`}
                      >
                        {isCompleted ? <CheckCircle size={15} weight="fill" aria-hidden /> : null}
                        {isCompleted ? 'Đã hoàn thành' : 'Đang thực hiện'}
                      </span>
                    </div>
                    <Progress
                      value={participant.progressPercent}
                      label={`${participant.currentBooks}/${participant.targetBooks} cuốn`}
                      ariaLabel={`Tiến độ của ${participant.user.displayName}`}
                      ariaValueText={`${participant.currentBooks}/${participant.targetBooks} cuốn, ${Math.round(participant.progressPercent)}%`}
                      className="mt-2"
                    />
                  </div>
                </li>
              )
            })}
          </ol>
        ) : (
          <EmptyState
            icon={UsersThree}
            title="Chưa có thứ hạng hiển thị"
            description="Khi có thành viên chia sẻ hoạt động đọc, bảng xếp hạng sẽ xuất hiện."
          />
        )}

        {isAuthenticated ? (
          <Pagination
            page={page}
            totalPages={totalPages}
            disabled={leaderboard.isFetching}
            onPageChange={setPage}
            className="mt-5 border-t border-border pt-4"
          />
        ) : null}
      </div>
    </section>
  )
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
      <ChallengeLeaderboard challengeId={id} />
    </div>
  )
}
