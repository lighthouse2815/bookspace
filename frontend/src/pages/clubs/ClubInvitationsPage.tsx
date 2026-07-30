import {
  Check,
  Clock,
  EnvelopeOpen,
  LockSimple,
  UsersThree,
  X,
} from '@phosphor-icons/react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import {
  useMyClubInvitations,
  useRespondToClubInvitation,
} from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatDate, formatRelativeTime } from '../../lib/format'
import type { ClubInvitationStatus } from '../../types/domain'

const filters: Array<{ value: ClubInvitationStatus; label: string }> = [
  { value: 'PENDING', label: 'Đang chờ' },
  { value: 'ACCEPTED', label: 'Đã tham gia' },
  { value: 'DECLINED', label: 'Đã từ chối' },
  { value: 'REVOKED', label: 'Đã thu hồi' },
  { value: 'EXPIRED', label: 'Đã hết hạn' },
]

const statusLabels: Record<ClubInvitationStatus, string> = {
  PENDING: 'Đang chờ phản hồi',
  ACCEPTED: 'Đã tham gia',
  DECLINED: 'Đã từ chối',
  REVOKED: 'Đã thu hồi',
  EXPIRED: 'Đã hết hạn',
}

export function ClubInvitationsPage() {
  const [status, setStatus] = useState<ClubInvitationStatus>('PENDING')
  const [pendingResponse, setPendingResponse] = useState<{
    id: string
    action: 'accept' | 'decline'
  } | null>(null)
  const invitations = useMyClubInvitations(status)
  const respond = useRespondToClubInvitation()
  const { showToast } = useToast()

  const handleResponse = async (
    invitationId: string,
    clubId: string,
    action: 'accept' | 'decline',
  ) => {
    setPendingResponse({ id: invitationId, action })
    try {
      await respond.mutateAsync({ invitationId, clubId, action })
      showToast(
        action === 'accept' ? 'Bạn đã tham gia câu lạc bộ' : 'Đã từ chối lời mời',
        'success',
      )
    } catch (error) {
      showToast(errorMessage(error, 'Không thể phản hồi lời mời.'), 'error')
    } finally {
      setPendingResponse(null)
    }
  }

  return (
    <div className="container-page section-space max-w-5xl">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Lời mời dành cho bạn</p>
          <h1 className="page-title mt-4">Hộp thư câu lạc bộ</h1>
          <p className="mt-3 max-w-2xl leading-7 text-muted">
            Xem ai đang mời bạn cùng đọc và quyết định không gian nào phù hợp.
          </p>
        </div>
        <Link to="/clubs" className="button button-secondary button-md">
          Khám phá câu lạc bộ
        </Link>
      </div>

      <div className="mt-8 flex gap-2 overflow-x-auto pb-1" role="tablist" aria-label="Trạng thái lời mời">
        {filters.map((filter) => (
          <button
            key={filter.value}
            type="button"
            role="tab"
            aria-selected={status === filter.value}
            className={`filter-tab ${status === filter.value ? 'filter-active' : ''}`}
            onClick={() => setStatus(filter.value)}
          >
            {filter.label}
          </button>
        ))}
      </div>

      <div className="mt-6">
        {invitations.isLoading ? (
          <LoadingRows count={4} />
        ) : invitations.isError ? (
          <ErrorState
            message="Không thể tải lời mời câu lạc bộ."
            retry={() => void invitations.refetch()}
          />
        ) : invitations.data?.items.length ? (
          <div className="space-y-4">
            {invitations.data.items.map((invitation) => (
              <article key={invitation.id} className="surface overflow-hidden">
                <div className="grid sm:grid-cols-[10rem_1fr]">
                  <div
                    className="min-h-36 bg-[linear-gradient(145deg,var(--accent-soft),var(--surface-muted))] bg-cover bg-center"
                    style={
                      invitation.club.coverImageUrl
                        ? {
                            backgroundImage: `linear-gradient(135deg,rgba(5,15,18,.36),rgba(5,15,18,.08)),url("${invitation.club.coverImageUrl}")`,
                          }
                        : undefined
                    }
                    aria-hidden
                  />
                  <div className="p-5 sm:p-6">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <div className="flex flex-wrap items-center gap-2 text-xs font-semibold text-muted">
                          <span
                            className={`rounded-full px-2.5 py-1 ${
                              invitation.status === 'PENDING'
                                ? 'bg-accent-soft text-accent-strong'
                                : 'bg-surface-muted'
                            }`}
                          >
                            {statusLabels[invitation.status]}
                          </span>
                          {invitation.club.isPrivate ? (
                            <span className="inline-flex items-center gap-1">
                              <LockSimple size={14} /> Riêng tư
                            </span>
                          ) : null}
                        </div>
                        {invitation.club.isPrivate && !invitation.club.isJoined ? (
                          <h2 className="mt-3 text-xl font-bold text-heading">
                            {invitation.club.name}
                          </h2>
                        ) : (
                          <Link
                            to={`/clubs/${invitation.club.id}`}
                            className="mt-3 block text-xl font-bold text-heading hover:text-accent-strong"
                          >
                            {invitation.club.name}
                          </Link>
                        )}
                        <p className="mt-1 line-clamp-2 text-sm leading-6 text-muted">
                          {invitation.club.description || 'Một không gian đọc đang chờ bạn tham gia.'}
                        </p>
                      </div>
                      <span className="inline-flex items-center gap-1.5 text-xs text-muted">
                        <UsersThree size={15} />
                        {invitation.club.memberCount} thành viên
                      </span>
                    </div>

                    <div className="mt-5 flex flex-wrap items-center justify-between gap-4 border-t border-border pt-4">
                      <div className="flex min-w-0 items-center gap-3">
                        <Avatar
                          src={invitation.inviter.avatarUrl}
                          name={invitation.inviter.displayName}
                          size="sm"
                        />
                        <div className="min-w-0 text-sm">
                          <p className="truncate text-body">
                            <strong className="text-heading">{invitation.inviter.displayName}</strong>{' '}
                            đã mời bạn
                          </p>
                          <p className="mt-0.5 flex items-center gap-1 text-xs text-muted">
                            <Clock size={13} />
                            {invitation.status === 'PENDING'
                              ? `Hết hạn ${formatDate(invitation.expiresAt)}`
                              : formatRelativeTime(invitation.respondedAt || invitation.createdAt)}
                          </p>
                        </div>
                      </div>

                      {invitation.status === 'PENDING' ? (
                        <div className="flex w-full gap-2 sm:w-auto">
                          <Button
                            variant="secondary"
                            className="flex-1 sm:flex-none"
                            loading={
                              respond.isPending &&
                              pendingResponse?.id === invitation.id &&
                              pendingResponse.action === 'decline'
                            }
                            disabled={
                              respond.isPending &&
                              (pendingResponse?.id !== invitation.id ||
                                pendingResponse.action !== 'decline')
                            }
                            icon={<X size={17} />}
                            onClick={() =>
                              void handleResponse(invitation.id, invitation.club.id, 'decline')
                            }
                          >
                            Từ chối
                          </Button>
                          <Button
                            className="flex-1 sm:flex-none"
                            loading={
                              respond.isPending &&
                              pendingResponse?.id === invitation.id &&
                              pendingResponse.action === 'accept'
                            }
                            disabled={
                              respond.isPending &&
                              (pendingResponse?.id !== invitation.id ||
                                pendingResponse.action !== 'accept')
                            }
                            icon={<Check size={17} />}
                            onClick={() =>
                              void handleResponse(invitation.id, invitation.club.id, 'accept')
                            }
                          >
                            Tham gia
                          </Button>
                        </div>
                      ) : null}
                    </div>
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <EmptyState
            icon={EnvelopeOpen}
            title={status === 'PENDING' ? 'Không có lời mời đang chờ' : 'Chưa có lời mời ở trạng thái này'}
            description={
              status === 'PENDING'
                ? 'Lời mời mới từ chủ nhiệm hoặc điều phối viên sẽ xuất hiện tại đây.'
                : 'Bạn có thể chuyển sang mục Đang chờ để xem những lời mời cần phản hồi.'
            }
          />
        )}
      </div>
    </div>
  )
}
