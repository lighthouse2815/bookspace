import {
  BookOpen,
  Check,
  EnvelopeSimple,
  GearSix,
  MagnifyingGlass,
  Trash,
  X,
} from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useBooks } from '../../hooks/useCatalog'
import {
  useClearClubCurrentBook,
  useClubInvitations,
  useInviteToClub,
  useRevokeClubInvitation,
  useSetClubCurrentBook,
  useUpdateClub,
} from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import type { Club } from '../../types/domain'
import { useToast } from '../../contexts/ToastContext'
import { BookCover } from '../books/BookCover'
import { Avatar } from '../ui/Avatar'
import { Button } from '../ui/Button'
import { EmptyState, ErrorState, LoadingRows } from '../ui/States'
import { ClubForm } from './ClubForm'

type ManagementTab = 'settings' | 'invitations' | 'current-book'

export function ClubManagementPanel({ club }: { club: Club }) {
  const permissions = club.permissions
  const tabs = [
    permissions.canEdit
      ? { value: 'settings' as const, label: 'Thiết lập', icon: GearSix }
      : null,
    permissions.canInvite
      ? { value: 'invitations' as const, label: 'Lời mời', icon: EnvelopeSimple }
      : null,
    permissions.canManageCurrentBook
      ? { value: 'current-book' as const, label: 'Sách đọc chung', icon: BookOpen }
      : null,
  ].filter((item): item is NonNullable<typeof item> => Boolean(item))

  const [tab, setTab] = useState<ManagementTab>(tabs[0]?.value ?? 'settings')

  if (!tabs.length) return null

  return (
    <section id="club-management" className="mt-8 surface overflow-hidden" aria-labelledby="club-management-title">
      <div className="border-b border-border p-5 sm:px-7 sm:py-6">
        <p className="eyebrow">Dành cho ban điều hành</p>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <h2 id="club-management-title" className="text-xl font-bold text-heading">
            Quản lý câu lạc bộ
          </h2>
          <span className="rounded-full bg-accent-soft px-3 py-1 text-xs font-semibold text-accent-strong">
            {club.viewerRole === 'OWNER' ? 'Chủ nhiệm' : 'Điều phối viên'}
          </span>
        </div>
      </div>

      <div className="border-b border-border px-3 sm:px-5">
        <div className="flex gap-1 overflow-x-auto py-2" role="tablist" aria-label="Quản lý câu lạc bộ">
          {tabs.map(({ value, label, icon: Icon }) => (
            <button
              key={value}
              type="button"
              role="tab"
              aria-selected={tab === value}
              className={`filter-tab ${tab === value ? 'filter-active' : ''}`}
              onClick={() => setTab(value)}
            >
              <Icon size={17} />
              {label}
            </button>
          ))}
        </div>
      </div>

      <div className="p-5 sm:p-7">
        {tab === 'settings' && permissions.canEdit ? <ClubSettings club={club} /> : null}
        {tab === 'invitations' && permissions.canInvite ? (
          <ClubInvitationManager club={club} />
        ) : null}
        {tab === 'current-book' && permissions.canManageCurrentBook ? (
          <ClubCurrentBookManager club={club} />
        ) : null}
      </div>
    </section>
  )
}

function ClubSettings({ club }: { club: Club }) {
  const updateClub = useUpdateClub(club.id)
  const { showToast } = useToast()

  return (
    <div>
      <h3 className="text-lg font-bold text-heading">Thông tin và quyền riêng tư</h3>
      <p className="mt-1 text-sm leading-6 text-muted">
        Những thay đổi này được hiển thị ngay trên trang câu lạc bộ.
      </p>
      <div className="mt-6">
        <ClubForm
          initialValue={{
            name: club.name,
            description: club.description ?? '',
            coverImageUrl: club.coverImageUrl ?? '',
            isPrivate: club.isPrivate,
          }}
          submitLabel="Lưu thay đổi"
          loading={updateClub.isPending}
          onSubmit={async (input) => {
            try {
              await updateClub.mutateAsync(input)
              showToast('Đã cập nhật câu lạc bộ', 'success')
            } catch (error) {
              showToast(errorMessage(error, 'Không thể cập nhật câu lạc bộ.'), 'error')
            }
          }}
        />
      </div>
    </div>
  )
}

function ClubInvitationManager({ club }: { club: Club }) {
  const invitations = useClubInvitations(club.id)
  const invite = useInviteToClub(club.id)
  const revoke = useRevokeClubInvitation(club.id)
  const { showToast } = useToast()
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState('')
  const [pendingRevokeId, setPendingRevokeId] = useState<string | null>(null)

  const submitInvite = async (event: FormEvent) => {
    event.preventDefault()
    const normalizedEmail = email.trim().toLowerCase()
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) {
      setEmailError('Nhập email hợp lệ của người bạn muốn mời.')
      return
    }

    setEmailError('')
    try {
      await invite.mutateAsync(normalizedEmail)
      setEmail('')
      showToast('Đã gửi lời mời tham gia câu lạc bộ', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gửi lời mời.'), 'error')
    }
  }

  const revokeInvitation = async (invitationId: string) => {
    setPendingRevokeId(invitationId)
    try {
      await revoke.mutateAsync(invitationId)
      showToast('Đã thu hồi lời mời', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể thu hồi lời mời.'), 'error')
    } finally {
      setPendingRevokeId(null)
    }
  }

  return (
    <div>
      <h3 className="text-lg font-bold text-heading">Mời thành viên</h3>
      <p className="mt-1 text-sm leading-6 text-muted">
        Gửi lời mời qua email tài khoản BookSpace. Lời mời có hiệu lực trong 7 ngày.
      </p>

      <form onSubmit={submitInvite} className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-start">
        <div className="min-w-0 flex-1">
          <label htmlFor="club-invite-email" className="sr-only">
            Email người được mời
          </label>
          <input
            id="club-invite-email"
            type="email"
            value={email}
            className={`input ${emailError ? 'input-error' : ''}`}
            onChange={(event) => {
              setEmail(event.target.value)
              if (emailError) setEmailError('')
            }}
            placeholder="ban.doc@example.com"
            autoComplete="off"
            required
          />
          {emailError ? (
            <p className="field-error" role="alert">
              {emailError}
            </p>
          ) : null}
        </div>
        <Button
          type="submit"
          loading={invite.isPending}
          icon={<EnvelopeSimple size={17} />}
        >
          Gửi lời mời
        </Button>
      </form>

      <div className="mt-8 border-t border-border pt-6">
        <div className="flex items-center justify-between gap-3">
          <h4 className="font-bold text-heading">Đang chờ phản hồi</h4>
          {invitations.data ? (
            <span className="text-xs font-semibold text-muted">
              {invitations.data.totalItems} lời mời
            </span>
          ) : null}
        </div>

        <div className="mt-4">
          {invitations.isLoading ? (
            <LoadingRows count={3} />
          ) : invitations.isError ? (
            <ErrorState
              message="Không thể tải danh sách lời mời."
              retry={() => void invitations.refetch()}
            />
          ) : invitations.data?.items.length ? (
            <div className="divide-y divide-border">
              {invitations.data.items.map((invitation) => (
                <div
                  key={invitation.id}
                  className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-center"
                >
                  <div className="flex min-w-0 flex-1 items-center gap-3">
                    <Avatar
                      src={invitation.invitedUser.avatarUrl}
                      name={invitation.invitedUser.displayName}
                      size="sm"
                    />
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-heading">
                        {invitation.invitedUser.displayName}
                      </p>
                      <p className="mt-0.5 truncate text-xs text-muted">
                        Gửi {formatRelativeTime(invitation.createdAt)}
                      </p>
                    </div>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    loading={revoke.isPending && pendingRevokeId === invitation.id}
                    disabled={revoke.isPending && pendingRevokeId !== invitation.id}
                    icon={<Trash size={15} />}
                    onClick={() => void revokeInvitation(invitation.id)}
                  >
                    Thu hồi
                  </Button>
                </div>
              ))}
            </div>
          ) : (
            <EmptyState
              icon={EnvelopeSimple}
              title="Không có lời mời đang chờ"
              description="Lời mời mới sẽ xuất hiện ở đây để bạn theo dõi hoặc thu hồi."
            />
          )}
        </div>
      </div>
    </div>
  )
}

function ClubCurrentBookManager({ club }: { club: Club }) {
  const [draft, setDraft] = useState('')
  const [search, setSearch] = useState('')
  const books = useBooks({ search, page: 1, pageSize: 8, sort: 'popular' })
  const setCurrentBook = useSetClubCurrentBook(club.id)
  const clearCurrentBook = useClearClubCurrentBook(club.id)
  const { showToast } = useToast()

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    setSearch(draft.trim())
  }

  const chooseBook = async (bookId: string, title: string) => {
    try {
      await setCurrentBook.mutateAsync(bookId)
      showToast(`Đã chọn “${title}” làm sách đọc chung`, 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể chọn sách đọc chung.'), 'error')
    }
  }

  const clearBook = async () => {
    try {
      await clearCurrentBook.mutateAsync()
      showToast('Đã gỡ sách đọc chung', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gỡ sách đọc chung.'), 'error')
    }
  }

  return (
    <div>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h3 className="text-lg font-bold text-heading">Sách đang đọc chung</h3>
          <p className="mt-1 text-sm leading-6 text-muted">
            Chọn một cuốn từ catalog độc lập của BookSpace để cả nhóm cùng theo dõi.
          </p>
        </div>
        {club.currentBook ? (
          <Button
            variant="secondary"
            size="sm"
            loading={clearCurrentBook.isPending}
            disabled={setCurrentBook.isPending}
            icon={<X size={16} />}
            onClick={() => void clearBook()}
          >
            Gỡ sách hiện tại
          </Button>
        ) : null}
      </div>

      {club.currentBook ? (
        <Link
          to={`/books/${club.currentBook.id}`}
          className="mt-5 flex gap-4 rounded-xl border border-accent/25 bg-accent-soft p-4 transition-colors hover:border-accent"
        >
          <BookCover
            src={club.currentBook.coverImageUrl}
            title={club.currentBook.title}
            className="h-24 w-16 shrink-0 rounded-lg"
          />
          <div className="min-w-0 self-center">
            <span className="inline-flex items-center gap-1 text-xs font-bold uppercase tracking-wider text-accent-strong">
              <Check size={14} weight="bold" /> Đang cùng đọc
            </span>
            <p className="mt-2 font-bold text-heading">{club.currentBook.title}</p>
            {club.currentBook.author ? (
              <p className="mt-1 text-sm text-muted">{club.currentBook.author.name}</p>
            ) : null}
          </div>
        </Link>
      ) : null}

      <form onSubmit={submitSearch} className="relative mt-7">
        <label htmlFor="club-book-search" className="sr-only">
          Tìm sách trong catalog
        </label>
        <MagnifyingGlass
          size={18}
          className="absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
        />
        <input
          id="club-book-search"
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          className="input pl-11 pr-20"
          placeholder="Tìm theo tên sách..."
        />
        <button
          type="submit"
          className="absolute right-1.5 top-1/2 -translate-y-1/2 rounded-lg bg-surface-muted px-3 py-1.5 text-xs font-semibold text-heading hover:bg-accent-soft hover:text-accent-strong"
        >
          Tìm
        </button>
      </form>

      <div className="mt-5">
        {books.isLoading ? (
          <LoadingRows count={4} />
        ) : books.isError ? (
          <ErrorState message="Không thể tải catalog sách." retry={() => void books.refetch()} />
        ) : books.data?.items.length ? (
          <div className="grid gap-3 sm:grid-cols-2">
            {books.data.items.map((book) => {
              const selected = club.currentBook?.id === book.id
              return (
                <button
                  key={book.id}
                  type="button"
                  disabled={selected || setCurrentBook.isPending || clearCurrentBook.isPending}
                  className={`flex min-w-0 gap-3 rounded-xl border p-3 text-left transition-colors disabled:cursor-default ${
                    selected
                      ? 'border-accent bg-accent-soft'
                      : 'border-border bg-surface hover:border-accent/60 hover:bg-surface-muted'
                  }`}
                  onClick={() => void chooseBook(book.id, book.title)}
                >
                  <BookCover
                    src={book.coverImageUrl}
                    title={book.title}
                    className="h-20 w-14 shrink-0 rounded-md"
                  />
                  <span className="min-w-0 self-center">
                    <strong className="line-clamp-2 text-sm text-heading">{book.title}</strong>
                    <span className="mt-1 block truncate text-xs text-muted">
                      {book.author?.name || 'Chưa cập nhật tác giả'}
                    </span>
                    <span className="mt-2 block text-xs font-semibold text-accent-strong">
                      {selected ? 'Đang được chọn' : 'Chọn sách này'}
                    </span>
                  </span>
                </button>
              )
            })}
          </div>
        ) : (
          <EmptyState
            icon={BookOpen}
            title="Không tìm thấy sách"
            description="Thử tên sách khác hoặc khám phá toàn bộ catalog."
            action={
              <Link to="/books" className="button button-secondary button-sm">
                Mở catalog
              </Link>
            }
          />
        )}
      </div>
    </div>
  )
}
