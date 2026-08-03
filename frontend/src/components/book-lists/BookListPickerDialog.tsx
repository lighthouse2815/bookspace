import { Check, FolderPlus, X } from '@phosphor-icons/react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useToast } from '../../contexts/ToastContext'
import { useMyBookLists, useToggleBookInList } from '../../hooks/useBookLists'
import { errorMessage } from '../../lib/api'
import { Button } from '../ui/Button'
import { Pagination } from '../ui/Pagination'
import { ErrorState, LoadingRows } from '../ui/States'

export function BookListPickerDialog({
  bookId,
  open,
  onClose,
}: {
  bookId: string
  open: boolean
  onClose: () => void
}) {
  const [page, setPage] = useState(1)
  const lists = useMyBookLists(page, bookId, open)
  const toggle = useToggleBookInList()
  const { showToast } = useToast()

  useEffect(() => {
    if (!open) return
    setPage(1)
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose, open])

  if (!open) return null

  const toggleList = async (listId: string, containsBook: boolean) => {
    try {
      await toggle.mutateAsync({ listId, bookId, containsBook })
      showToast(
        containsBook ? 'Đã bỏ sách khỏi bộ sưu tập' : 'Đã thêm sách vào bộ sưu tập',
        'success',
      )
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật bộ sưu tập'), 'error')
    }
  }

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center p-4" role="presentation">
      <button
        type="button"
        className="absolute inset-0 bg-slate-950/55 backdrop-blur-sm"
        aria-label="Đóng danh sách bộ sưu tập"
        onClick={onClose}
      />
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="book-list-picker-title"
        className="surface relative z-10 flex max-h-[min(42rem,85dvh)] w-full max-w-lg flex-col overflow-hidden shadow-2xl"
      >
        <header className="flex items-start justify-between border-b border-border px-5 py-4">
          <div>
            <p className="eyebrow">Lưu theo cách của bạn</p>
            <h2 id="book-list-picker-title" className="mt-1 text-xl font-bold text-heading">
              Chọn bộ sưu tập
            </h2>
          </div>
          <button type="button" className="icon-button" aria-label="Đóng" onClick={onClose}>
            <X size={20} />
          </button>
        </header>

        <div className="min-h-64 overflow-y-auto p-5">
          {lists.isLoading ? (
            <LoadingRows count={4} />
          ) : lists.isError ? (
            <ErrorState message="Không thể tải bộ sưu tập của bạn." retry={() => void lists.refetch()} />
          ) : lists.data?.items.length ? (
            <div className="space-y-2">
              {lists.data.items.map((list) => {
                const containsBook = Boolean(list.containsBook)
                const pending =
                  toggle.isPending && toggle.variables?.listId === list.id
                return (
                  <button
                    key={list.id}
                    type="button"
                    className="flex w-full items-center gap-3 rounded-xl border border-border p-4 text-left transition-colors hover:bg-surface-muted focus-visible:focus-ring"
                    disabled={toggle.isPending}
                    onClick={() => void toggleList(list.id, containsBook)}
                  >
                    <span className={`grid h-10 w-10 place-items-center rounded-xl ${containsBook ? 'bg-accent text-white' : 'bg-surface-muted text-muted'}`}>
                      {containsBook ? <Check size={20} weight="bold" /> : <FolderPlus size={20} />}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-semibold text-heading">{list.name}</span>
                      <span className="mt-0.5 block text-xs text-muted">
                        {pending ? 'Đang cập nhật...' : `${list.bookCount} sách · ${list.visibility === 'PRIVATE' ? 'Riêng tư' : 'Công khai'}`}
                      </span>
                    </span>
                  </button>
                )
              })}
            </div>
          ) : (
            <div className="grid min-h-56 place-items-center text-center">
              <div>
                <FolderPlus size={32} className="mx-auto text-muted" />
                <p className="mt-3 font-semibold text-heading">Bạn chưa có bộ sưu tập nào</p>
                <p className="mt-1 text-sm text-muted">Tạo một bộ sưu tập rồi quay lại lưu cuốn sách này.</p>
                <Link to="/lists" onClick={onClose} className="button button-primary button-sm mt-4">
                  Tạo bộ sưu tập
                </Link>
              </div>
            </div>
          )}
        </div>

        {lists.data && lists.data.totalPages > 1 ? (
          <Pagination
            page={lists.data.page}
            totalPages={lists.data.totalPages}
            onPageChange={setPage}
            disabled={lists.isFetching || toggle.isPending}
            className="border-t border-border px-5 py-4"
          />
        ) : null}
        <footer className="border-t border-border px-5 py-4 text-right">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Xong</Button>
        </footer>
      </section>
    </div>
  )
}
