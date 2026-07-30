import { Crown, ShieldCheck, Trash, UserCircle, UsersThree, X } from '@phosphor-icons/react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useClubMembers,
  useRemoveClubMember,
  useUpdateClubMemberRole,
} from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { Club, ClubMember, ClubMemberRole } from '../../types/domain'
import { Avatar } from '../ui/Avatar'
import { Button } from '../ui/Button'
import { EmptyState, ErrorState, LoadingRows } from '../ui/States'

const roleLabels: Record<ClubMemberRole, string> = {
  OWNER: 'Chủ nhiệm',
  MODERATOR: 'Điều phối viên',
  MEMBER: 'Thành viên',
}

function roleIcon(role: ClubMemberRole) {
  if (role === 'OWNER') return <Crown size={14} weight="fill" />
  if (role === 'MODERATOR') return <ShieldCheck size={14} weight="fill" />
  return <UserCircle size={14} weight="fill" />
}

export function ClubRoster({ club }: { club: Club }) {
  const { user } = useAuth()
  const canViewRoster = !club.isPrivate || club.isJoined
  const members = useClubMembers(club.id, canViewRoster)
  const updateRole = useUpdateClubMemberRole(club.id)
  const removeMember = useRemoveClubMember(club.id)
  const { showToast } = useToast()
  const [confirmRemove, setConfirmRemove] = useState<ClubMember | null>(null)
  const [pendingUserId, setPendingUserId] = useState<string | null>(null)

  const viewerRole = club.viewerRole
  const canManage = club.permissions.canManageMembers

  const canActOn = (member: ClubMember) => {
    if (!canManage || member.user.id === user?.id || member.role === 'OWNER') return false
    if (viewerRole === 'OWNER') return true
    return viewerRole === 'MODERATOR' && member.role === 'MEMBER'
  }

  const changeRole = async (member: ClubMember, role: 'MODERATOR' | 'MEMBER') => {
    setPendingUserId(member.user.id)
    try {
      await updateRole.mutateAsync({ userId: member.user.id, role })
      showToast(
        role === 'MODERATOR'
          ? `Đã giao quyền điều phối cho ${member.user.displayName}`
          : `Đã chuyển ${member.user.displayName} về vai trò thành viên`,
        'success',
      )
    } catch (error) {
      showToast(errorMessage(error, 'Không thể thay đổi vai trò thành viên.'), 'error')
    } finally {
      setPendingUserId(null)
    }
  }

  const remove = async (member: ClubMember) => {
    setPendingUserId(member.user.id)
    try {
      await removeMember.mutateAsync(member.user.id)
      setConfirmRemove(null)
      showToast(`Đã xóa ${member.user.displayName} khỏi câu lạc bộ`, 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa thành viên.'), 'error')
    } finally {
      setPendingUserId(null)
    }
  }

  if (!canViewRoster) return null

  return (
    <section className="surface p-5 sm:p-6" aria-labelledby="club-roster-title">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="eyebrow">Con người</p>
          <h2 id="club-roster-title" className="mt-2 text-xl font-bold text-heading">
            Thành viên
          </h2>
        </div>
        <span className="rounded-full bg-surface-muted px-3 py-1 text-xs font-semibold text-muted">
          {club.memberCount}
        </span>
      </div>

      <div className="mt-5">
        {members.isLoading ? (
          <LoadingRows count={4} />
        ) : members.isError ? (
          <ErrorState
            message="Không thể tải danh sách thành viên."
            retry={() => void members.refetch()}
          />
        ) : members.data?.items.length ? (
          <div className="divide-y divide-border">
            {members.data.items.map((member) => {
              const canAct = canActOn(member)
              const pending = pendingUserId === member.user.id
              return (
                <div key={member.user.id} className="py-4 first:pt-0 last:pb-0">
                  <div className="flex items-start gap-3">
                    <Avatar
                      src={member.user.avatarUrl}
                      name={member.user.displayName}
                      size="sm"
                    />
                    <div className="min-w-0 flex-1">
                      <Link
                        to={`/users/${member.user.id}`}
                        className="block truncate text-sm font-semibold text-heading hover:text-accent-strong"
                      >
                        {member.user.displayName}
                        {member.user.id === user?.id ? ' (Bạn)' : ''}
                      </Link>
                      <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
                        <span
                          className={`inline-flex items-center gap-1 font-semibold ${
                            member.role === 'OWNER' || member.role === 'MODERATOR'
                              ? 'text-accent-strong'
                              : ''
                          }`}
                        >
                          {roleIcon(member.role)}
                          {roleLabels[member.role]}
                        </span>
                        <span>·</span>
                        <span>Từ {formatDate(member.joinedAt)}</span>
                      </div>
                    </div>
                  </div>

                  {canAct ? (
                    confirmRemove?.user.id === member.user.id ? (
                      <div className="mt-3 rounded-xl bg-surface-muted p-3">
                        <p className="text-xs leading-5 text-muted">
                          Xóa <strong className="text-heading">{member.user.displayName}</strong> khỏi
                          câu lạc bộ?
                        </p>
                        <div className="mt-2 flex gap-2">
                          <Button
                            size="sm"
                            variant="danger"
                            loading={removeMember.isPending && pending}
                            onClick={() => void remove(member)}
                          >
                            Xác nhận
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            icon={<X size={15} />}
                            onClick={() => setConfirmRemove(null)}
                          >
                            Hủy
                          </Button>
                        </div>
                      </div>
                    ) : (
                      <div className="mt-3 flex flex-wrap gap-2 pl-11">
                        {viewerRole === 'OWNER' ? (
                          <Button
                            size="sm"
                            variant="ghost"
                            loading={updateRole.isPending && pending}
                            disabled={
                              removeMember.isPending || (updateRole.isPending && !pending)
                            }
                            icon={<ShieldCheck size={15} />}
                            onClick={() =>
                              void changeRole(
                                member,
                                member.role === 'MODERATOR' ? 'MEMBER' : 'MODERATOR',
                              )
                            }
                          >
                            {member.role === 'MODERATOR' ? 'Gỡ điều phối' : 'Giao điều phối'}
                          </Button>
                        ) : null}
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={updateRole.isPending || removeMember.isPending}
                          icon={<Trash size={15} />}
                          onClick={() => setConfirmRemove(member)}
                        >
                          Xóa
                        </Button>
                      </div>
                    )
                  ) : null}
                </div>
              )
            })}
          </div>
        ) : (
          <EmptyState
            icon={UsersThree}
            title="Chưa có thành viên"
            description="Danh sách thành viên sẽ xuất hiện ở đây."
          />
        )}
      </div>
    </section>
  )
}
