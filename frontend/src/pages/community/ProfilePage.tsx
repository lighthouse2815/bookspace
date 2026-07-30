import { BookOpenText, CalendarBlank, UserMinus, UserPlus, Users } from '@phosphor-icons/react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Navigate, useParams } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { ErrorState } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useUser } from '../../hooks/useCommunity'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import { communityService } from '../../services/community.service'

export function CurrentProfileRedirect() {
  const { user } = useAuth()
  return user ? <Navigate to={`/users/${user.id}`} replace /> : <Navigate to="/login" replace />
}

export function ProfilePage() {
  const { id } = useParams()
  const { user: currentUser, isAuthenticated } = useAuth()
  const profile = useUser(id)
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const ownProfile = currentUser?.id === id
  const follow = useMutation({
    mutationFn: () =>
      profile.data?.isFollowing ? communityService.unfollow(id!) : communityService.follow(id!),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users', id] })
      showToast(profile.data?.isFollowing ? 'Đã bỏ theo dõi' : 'Đã theo dõi người đọc này', 'success')
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  if (profile.isLoading) {
    return (
      <div className="container-page section-space">
        <div className="h-48 animate-pulse rounded-2xl bg-surface-muted" />
      </div>
    )
  }

  if (profile.isError || !profile.data) {
    return (
      <div className="container-page section-space">
        <ErrorState message="Không thể tải hồ sơ người đọc." retry={() => void profile.refetch()} />
      </div>
    )
  }

  return (
    <div className="container-page section-space">
      <section className="surface overflow-hidden">
        <div className="h-32 bg-[radial-gradient(circle_at_20%_20%,rgba(16,185,129,.28),transparent_35%),linear-gradient(135deg,var(--surface-muted),var(--surface))]" />
        <div className="px-5 pb-7 sm:px-8">
          <div className="-mt-12 flex flex-col gap-5 sm:flex-row sm:items-end">
            <div className="rounded-full border-4 border-surface bg-surface">
              <Avatar src={profile.data.avatarUrl} name={profile.data.displayName} size="xl" />
            </div>
            <div className="min-w-0 flex-1 sm:pb-1">
              <h1 className="text-2xl font-bold tracking-tight text-heading">{profile.data.displayName}</h1>
              <p className="mt-1 text-sm text-muted">@{profile.data.username || profile.data.id.slice(0, 8)}</p>
            </div>
            {!ownProfile ? (
              isAuthenticated ? (
                <Button
                  variant={profile.data.isFollowing ? 'secondary' : 'primary'}
                  loading={follow.isPending}
                  icon={
                    profile.data.isFollowing ? <UserMinus size={18} /> : <UserPlus size={18} />
                  }
                  onClick={() => follow.mutate()}
                >
                  {profile.data.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
                </Button>
              ) : null
            ) : null}
          </div>
          {profile.data.bio ? (
            <p className="mt-6 max-w-2xl whitespace-pre-line text-sm leading-6 text-body">{profile.data.bio}</p>
          ) : ownProfile ? (
            <p className="mt-6 text-sm text-muted">Bạn có thể thêm giới thiệu trong phần cài đặt.</p>
          ) : null}
          <div className="mt-6 flex flex-wrap gap-x-6 gap-y-3 text-sm">
            <span className="inline-flex items-center gap-2 text-muted">
              <Users size={17} />
              <strong className="text-heading">{profile.data.followerCount ?? 0}</strong> người theo dõi
            </span>
            <span className="text-muted">
              <strong className="text-heading">{profile.data.followingCount ?? 0}</strong> đang theo dõi
            </span>
            <span className="inline-flex items-center gap-2 text-muted">
              <CalendarBlank size={17} />
              Tham gia {formatDate(profile.data.joinedAt)}
            </span>
          </div>
        </div>
      </section>

      <section className="mt-8 grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-3">
        <div className="bg-surface p-6">
          <BookOpenText size={23} weight="duotone" className="text-accent-strong" />
          <p className="mt-4 text-3xl font-bold text-heading">{profile.data.booksReadCount ?? 0}</p>
          <p className="mt-1 text-sm text-muted">cuốn đã đọc</p>
        </div>
        <div className="bg-surface p-6 sm:col-span-2">
          <h2 className="font-semibold text-heading">Hành trình công khai</h2>
          <p className="mt-2 max-w-xl text-sm leading-6 text-muted">
            Các bài đánh giá công khai của người đọc này xuất hiện trong bảng tin cộng đồng và trang từng cuốn sách.
          </p>
        </div>
      </section>
    </div>
  )
}
