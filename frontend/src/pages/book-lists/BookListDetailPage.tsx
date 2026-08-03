import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  FolderOpen,
  GlobeHemisphereWest,
  LockKey,
  PencilSimple,
  Trash,
} from '@phosphor-icons/react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { BookListFormDialog } from '../../components/book-lists/BookListFormDialog'
import { BookCover } from '../../components/books/BookCover'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import {
  useBookListDetail,
  useDeleteBookList,
  useRemoveBookFromList,
  useReorderBookList,
} from '../../hooks/useBookLists'
import { errorMessage, isNotFoundError } from '../../lib/api'

export function BookListDetailPage() {
  const { listId } = useParams()
  const navigate = useNavigate()
  const list = useBookListDetail(listId)
  const deleteList = useDeleteBookList()
  const removeBook = useRemoveBookFromList(listId ?? '')
  const reorder = useReorderBookList(listId ?? '')
  const { showToast } = useToast()
  const [editing, setEditing] = useState(false)

  if (list.isLoading) {
    return <div className="container-page section-space"><LoadingRows count={5} /></div>
  }

  if (list.isError && isNotFoundError(list.error)) {
    return (
      <div className="container-page section-space">
        <EmptyState
          title="Không tìm thấy bộ sưu tập"
          description="Bộ sưu tập có thể đang ở chế độ riêng tư, đã bị xóa hoặc đường dẫn không còn đúng."
          icon={FolderOpen}
          action={<Link to="/explore" className="button button-secondary button-sm">Về trang khám phá</Link>}
        />
      </div>
    )
  }

  if (list.isError || !list.data) {
    return <div className="container-page section-space"><ErrorState message="Không thể tải bộ sưu tập." retry={() => void list.refetch()} /></div>
  }

  const detail = list.data

  const deleteCurrentList = async () => {
    if (!window.confirm(`Xóa bộ sưu tập “${detail.name}”?`)) return
    try {
      await deleteList.mutateAsync(detail.id)
      showToast('Đã xóa bộ sưu tập', 'success')
      navigate('/lists', { replace: true })
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa bộ sưu tập'), 'error')
    }
  }

  const move = async (index: number, direction: -1 | 1) => {
    const nextIndex = index + direction
    if (nextIndex < 0 || nextIndex >= detail.items.length) return
    const ids = detail.items.map((item) => item.book.id)
    ;[ids[index], ids[nextIndex]] = [ids[nextIndex], ids[index]]
    try {
      await reorder.mutateAsync(ids)
    } catch (error) {
      showToast(errorMessage(error, 'Không thể đổi thứ tự sách'), 'error')
    }
  }

  const remove = async (bookId: string) => {
    try {
      await removeBook.mutateAsync(bookId)
      showToast('Đã bỏ sách khỏi bộ sưu tập', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể bỏ sách khỏi bộ sưu tập'), 'error')
    }
  }

  return (
    <div className="container-page section-space">
      <Link to={detail.isOwner ? '/lists' : `/users/${detail.owner.id}?tab=collections`} className="inline-flex items-center gap-2 text-sm font-semibold text-muted hover:text-heading">
        <ArrowLeft size={17} /> {detail.isOwner ? 'Bộ sưu tập của tôi' : `Hồ sơ ${detail.owner.displayName}`}
      </Link>

      <header className="mt-6 border-b border-border pb-8">
        <div className="flex flex-col justify-between gap-5 sm:flex-row sm:items-start">
          <div className="max-w-3xl">
            <div className="flex flex-wrap items-center gap-2 text-xs font-semibold text-muted">
              <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-muted px-2.5 py-1">
                {detail.visibility === 'PRIVATE' ? <LockKey size={14} /> : <GlobeHemisphereWest size={14} />}
                {detail.visibility === 'PRIVATE' ? 'Riêng tư' : 'Công khai'}
              </span>
              <span>{detail.items.length} sách</span>
            </div>
            <h1 className="mt-4 break-words text-4xl font-bold tracking-tight text-heading sm:text-5xl">{detail.name}</h1>
            {detail.description ? <p className="mt-4 whitespace-pre-line text-base leading-7 text-body">{detail.description}</p> : null}
            <Link to={`/users/${detail.owner.id}`} className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-heading hover:text-accent-strong">
              <Avatar src={detail.owner.avatarUrl} name={detail.owner.displayName} size="sm" />
              Tuyển chọn bởi {detail.owner.displayName}
            </Link>
          </div>
          {detail.isOwner ? (
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" icon={<PencilSimple size={17} />} onClick={() => setEditing(true)}>Chỉnh sửa</Button>
              <Button variant="danger" size="sm" icon={<Trash size={17} />} loading={deleteList.isPending} onClick={() => void deleteCurrentList()}>Xóa</Button>
            </div>
          ) : null}
        </div>
      </header>

      <main className="mt-8">
        {detail.items.length ? (
          <ol className="space-y-3">
            {detail.items.map((item, index) => (
              <li key={item.id} className="surface flex items-center gap-4 p-4">
                <span className="hidden w-8 text-center text-sm font-bold text-muted sm:block">{String(index + 1).padStart(2, '0')}</span>
                <Link to={`/books/${item.book.id}`} className="shrink-0">
                  <BookCover src={item.book.coverImageUrl} title={item.book.title} className="h-24 w-16 rounded-lg" />
                </Link>
                <div className="min-w-0 flex-1">
                  <Link to={`/books/${item.book.id}`} className="line-clamp-2 font-bold text-heading hover:text-accent-strong">{item.book.title}</Link>
                  <p className="mt-1 truncate text-sm text-muted">{item.book.author?.name || 'Tác giả đang cập nhật'}</p>
                </div>
                {detail.isOwner ? (
                  <div className="flex shrink-0 items-center gap-1">
                    <button type="button" className="icon-button" aria-label={`Đưa ${item.book.title} lên`} disabled={index === 0 || reorder.isPending} onClick={() => void move(index, -1)}><ArrowUp size={17} /></button>
                    <button type="button" className="icon-button" aria-label={`Đưa ${item.book.title} xuống`} disabled={index === detail.items.length - 1 || reorder.isPending} onClick={() => void move(index, 1)}><ArrowDown size={17} /></button>
                    <button type="button" className="icon-button text-danger" aria-label={`Bỏ ${item.book.title}`} disabled={removeBook.isPending} onClick={() => void remove(item.book.id)}><Trash size={17} /></button>
                  </div>
                ) : null}
              </li>
            ))}
          </ol>
        ) : (
          <EmptyState
            title="Bộ sưu tập này còn trống"
            description={detail.isOwner ? 'Mở một trang sách và chọn “Lưu vào bộ sưu tập” để thêm cuốn đầu tiên.' : 'Chủ bộ sưu tập chưa thêm cuốn sách nào.'}
            icon={FolderOpen}
            action={detail.isOwner ? <Link to="/books" className="button button-primary button-sm">Khám phá sách</Link> : undefined}
          />
        )}
      </main>

      <BookListFormDialog open={editing} list={detail} onClose={() => setEditing(false)} />
    </div>
  )
}
