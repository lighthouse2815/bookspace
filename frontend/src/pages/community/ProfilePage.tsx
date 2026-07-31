import {
  BookOpenText,
  CalendarBlank,
  UserCircle,
  UserMinus,
  UserPlus,
  Users,
} from '@phosphor-icons/react'
import { Link, Navigate, useLocation, useParams } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { EmptyState, ErrorState } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useFollowUser, useUser } from '../../hooks/useCommunity'
import { errorMessage, isNotFoundError } from '../../lib/api'
import { formatDate } from '../../lib/format'

export function CurrentProfileRedirect() {
  const { user } = useAuth()
  return user ? <Navigate to={`/users/${user.id}`} replace /> : <Navigate to="/login" replace />
}

export function ProfilePage() {
  const { id } = useParams()
  const { user: currentUser, isAuthenticated, isLoading: isAuthLoading } = useAuth()
  const location = useLocation()
  const profile = useUser(id)
  const { showToast } = useToast()
  const ownProfile = currentUser?.id === id
  const follow = useFollowUser(id ?? '', Boolean(profile.data?.isFollowing))

  if (isAuthLoading || profile.isPending) {
    return (
      <div className="container-page section-space">
        <div className="h-48 animate-pulse rounded-2xl bg-surface-muted" />
      </div>
    )
  }

  if (profile.isError && isNotFoundError(profile.error)) {
    return (
      <div className="container-page section-space">
        <EmptyState
          title="Không tìm thấy hồ sơ"
          description="Độc giả này không còn hoạt động hoặc đường dẫn không chính xác."
          icon={UserCircle}
          action={
            <Link to="/people" className="button button-secondary button-sm">
              Xem danh sách độc giả
            </Link>
          }
        />
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
              <h1 className="break-words text-2xl font-bold tracking-tight text-heading">
                {profile.data.displayName}
              </h1>
              <p className="mt-1 text-sm text-muted">Hồ sơ độc giả BookSpace</p>
            </div>
            {!ownProfile ? (
              isAuthenticated ? (
                <Button
                  variant={profile.data.isFollowing ? 'secondary' : 'primary'}
                  loading={follow.isPending}
                  disabled={follow.isPending}
                  aria-label={`${profile.data.isFollowing ? 'Bỏ theo dõi' : 'Theo dõi'} ${profile.data.displayName}`}
                  icon={
                    profile.data.isFollowing ? <UserMinus size={18} /> : <UserPlus size={18} />
                  }
                  onClick={() =>
                    follow.mutate(undefined, {
                      onSuccess: () =>
                        showToast(
                          profile.data.isFollowing
                            ? 'Đã bỏ theo dõi'
                            : 'Đã theo dõi người đọc này',
                          'success',
                        ),
                      onError: (error) => showToast(errorMessage(error), 'error'),
                    })
                  }
                >
                  {profile.data.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
                </Button>
              ) : (
                <Link
                  to="/login"
                  state={{ from: `${location.pathname}${location.search}` }}
                  aria-label={`Đăng nhập để theo dõi ${profile.data.displayName}`}
                  className="button button-primary button-md"
                >
                  Đăng nhập để theo dõi
                </Link>
              )
            ) : null}
          </div>
          {profile.data.bio ? (
            <p className="mt-6 max-w-2xl whitespace-pre-line break-words text-sm leading-6 text-body">
              {profile.data.bio}
            </p>
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
