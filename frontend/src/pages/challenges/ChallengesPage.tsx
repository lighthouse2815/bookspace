import { CalendarBlank, CheckCircle, Flag, Users } from '@phosphor-icons/react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useChallengeMembership,
  useChallengeProgress,
  useChallenges,
} from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { Challenge } from '../../types/domain'

function ChallengeCard({ challenge }: { challenge: Challenge }) {
  const { isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const membership = useChallengeMembership(challenge.id, challenge.isJoined)
  const progress = useChallengeProgress()
  const [currentBooks, setCurrentBooks] = useState(String(challenge.currentBooks))
  const percentage = (challenge.currentBooks / Math.max(challenge.goalBooks, 1)) * 100

  const toggle = async () => {
    try {
      await membership.mutateAsync()
      showToast(challenge.isJoined ? 'Đã rời thử thách' : 'Đã tham gia thử thách', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  const update = async () => {
    const value = Math.max(0, Math.min(Number(currentBooks), challenge.goalBooks))
    try {
      await progress.mutateAsync({ id: challenge.id, currentBooks: value })
      showToast('Đã cập nhật tiến độ thử thách', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  return (
    <article className="surface flex flex-col overflow-hidden">
      <div
        className="h-36 bg-cover bg-center"
        style={
          challenge.coverImageUrl
            ? { backgroundImage: `linear-gradient(90deg,rgba(2,6,23,.38),rgba(2,6,23,.08)),url("${challenge.coverImageUrl}")` }
            : {
                backgroundImage:
                  'radial-gradient(circle at 20% 30%,rgba(16,185,129,.35),transparent 35%),linear-gradient(135deg,var(--surface-muted),var(--surface))',
              }
        }
      />
      <div className="flex flex-1 flex-col p-5">
        <div className="flex flex-wrap gap-x-4 gap-y-2 text-xs text-muted">
          <span className="inline-flex items-center gap-1.5">
            <CalendarBlank size={15} />
            {formatDate(challenge.endDate)}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <Users size={15} />
            {challenge.participantCount} người
          </span>
        </div>
        <h2 className="mt-4 text-xl font-bold text-heading">{challenge.title}</h2>
        <p className="mt-2 line-clamp-3 text-sm leading-6 text-muted">{challenge.description}</p>
        <div className="mt-6">
          <Progress
            value={percentage}
            label={
              challenge.isJoined
                ? `${challenge.currentBooks}/${challenge.goalBooks} cuốn`
                : `Mục tiêu ${challenge.goalBooks} cuốn`
            }
          />
        </div>
        <div className="mt-auto pt-6">
          {isAuthenticated ? (
            <>
              <Button
                variant={challenge.isJoined ? 'secondary' : 'primary'}
                className="w-full"
                loading={membership.isPending}
                icon={challenge.isJoined ? <CheckCircle size={18} /> : <Flag size={18} />}
                onClick={() => void toggle()}
              >
                {challenge.isJoined ? 'Đang tham gia' : 'Tham gia thử thách'}
              </Button>
              {challenge.isJoined ? (
                <div className="mt-3 flex items-end gap-2">
                  <label className="field flex-1">
                    <span className="field-label">Số cuốn đã hoàn thành</span>
                    <input
                      type="number"
                      className="input py-2"
                      min={0}
                      max={challenge.goalBooks}
                      value={currentBooks}
                      onChange={(event) => setCurrentBooks(event.target.value)}
                    />
                  </label>
                  <Button
                    size="sm"
                    variant="secondary"
                    loading={progress.isPending}
                    onClick={() => void update()}
                  >
                    Lưu
                  </Button>
                </div>
              ) : null}
            </>
          ) : (
            <Link to="/login" className="button button-primary button-md w-full">
              Đăng nhập để tham gia
            </Link>
          )}
        </div>
      </div>
    </article>
  )
}

export function ChallengesPage() {
  const challenges = useChallenges()

  return (
    <div className="container-page section-space">
      <div className="max-w-3xl">
        <p className="eyebrow">Mục tiêu có cộng đồng</p>
        <h1 className="page-title mt-4">Thử thách đọc sách</h1>
        <p className="mt-3 max-w-2xl leading-7 text-muted">
          Chọn một nhịp vừa sức, ghi nhận tiến độ và hoàn thành cùng những người đọc khác.
        </p>
      </div>
      <div className="mt-10">
        {challenges.isLoading ? (
          <LoadingRows count={6} />
        ) : challenges.isError ? (
          <ErrorState message="Không thể tải thử thách." retry={() => void challenges.refetch()} />
        ) : challenges.data?.items.length ? (
          <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
            {challenges.data.items.map((challenge) => (
              <ChallengeCard key={challenge.id} challenge={challenge} />
            ))}
          </div>
        ) : (
          <EmptyState
            icon={Flag}
            title="Chưa có thử thách đang mở"
            description="Các thử thách mới sẽ xuất hiện tại đây khi được công bố."
          />
        )}
      </div>
    </div>
  )
}
