import { X } from '@phosphor-icons/react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useUserConnections } from '../../hooks/useCommunity'
import { Avatar } from '../ui/Avatar'
import { Pagination } from '../ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../ui/States'

export function ProfileConnectionsDialog({
  userId,
  kind,
  open,
  onClose,
}: {
  userId: string
  kind: 'followers' | 'following'
  open: boolean
  onClose: () => void
}) {
  const [page, setPage] = useState(1)
  const connections = useUserConnections(userId, kind, page, open)
  const title = kind === 'followers' ? 'Người theo dõi' : 'Đang theo dõi'

  useEffect(() => {
    if (!open) return
    setPage(1)
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [kind, onClose, open])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center p-4" role="presentation">
      <button
        type="button"
        className="absolute inset-0 bg-slate-950/55 backdrop-blur-sm"
        aria-label="Đóng danh sách"
        onClick={onClose}
      />
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="profile-connections-title"
        className="surface relative z-10 flex max-h-[min(42rem,85dvh)] w-full max-w-lg flex-col overflow-hidden shadow-2xl"
      >
        <header className="flex items-center justify-between border-b border-border px-5 py-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Kết nối</p>
            <h2 id="profile-connections-title" className="mt-1 text-lg font-bold text-heading">
              {title}
            </h2>
          </div>
          <button type="button" className="icon-button" aria-label="Đóng" onClick={onClose}>
            <X size={20} />
          </button>
        </header>

        <div className="min-h-64 overflow-y-auto p-5">
          {connections.isLoading ? (
            <LoadingRows count={4} />
          ) : connections.isError ? (
            <ErrorState
              message={`Không thể tải danh sách ${title.toLocaleLowerCase('vi-VN')}.`}
              retry={() => void connections.refetch()}
            />
          ) : connections.data?.items.length ? (
            <div className="space-y-2">
              {connections.data.items.map((person) => (
                <Link
                  key={person.id}
                  to={`/users/${person.id}`}
                  onClick={onClose}
                  aria-label={`Xem hồ sơ ${person.displayName}`}
                  className="flex items-center gap-3 rounded-xl p-3 transition-colors hover:bg-surface-muted focus-visible:focus-ring"
                >
                  <Avatar src={person.avatarUrl} name={person.displayName} />
                  <span className="min-w-0 flex-1 break-words font-semibold text-heading">
                    {person.displayName}
                  </span>
                </Link>
              ))}
            </div>
          ) : (
            <EmptyState
              title={kind === 'followers' ? 'Chưa có người theo dõi' : 'Chưa theo dõi ai'}
              description="Các kết nối công khai sẽ xuất hiện tại đây."
            />
          )}
        </div>

        {connections.data ? (
          <Pagination
            page={connections.data.page}
            totalPages={connections.data.totalPages}
            onPageChange={setPage}
            disabled={connections.isFetching}
            className="border-t border-border px-5 py-4"
          />
        ) : null}
      </section>
    </div>
  )
}
