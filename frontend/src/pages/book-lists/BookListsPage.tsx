import { FolderPlus, PencilSimple, Plus, Trash } from '@phosphor-icons/react'
import { useState } from 'react'
import { BookListCard } from '../../components/book-lists/BookListCard'
import { BookListFormDialog } from '../../components/book-lists/BookListFormDialog'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useDeleteBookList, useMyBookLists } from '../../hooks/useBookLists'
import { errorMessage } from '../../lib/api'
import type { BookListSummary } from '../../types/domain'

export function BookListsPage() {
  const [page, setPage] = useState(1)
  const [editor, setEditor] = useState<'create' | BookListSummary | null>(null)
  const lists = useMyBookLists(page)
  const deleteList = useDeleteBookList()
  const { showToast } = useToast()

  const remove = async (list: BookListSummary) => {
    if (!window.confirm(`Xóa bộ sưu tập “${list.name}”? Sách trong thư viện của bạn không bị ảnh hưởng.`)) return
    try {
      await deleteList.mutateAsync(list.id)
      showToast('Đã xóa bộ sưu tập', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa bộ sưu tập'), 'error')
    }
  }

  return (
    <div className="container-page section-space">
      <header className="flex flex-col justify-between gap-5 sm:flex-row sm:items-end">
        <div>
          <p className="eyebrow">Không gian tuyển chọn</p>
          <h1 className="mt-3 text-3xl font-bold tracking-tight text-heading sm:text-4xl">
            Bộ sưu tập của bạn
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
            Gom những cuốn sách cùng một mạch cảm hứng, sắp xếp theo ý bạn và chọn chia sẻ công khai hoặc giữ riêng tư.
          </p>
        </div>
        <Button icon={<Plus size={18} />} onClick={() => setEditor('create')}>
          Tạo bộ sưu tập
        </Button>
      </header>

      <main className="mt-8">
        {lists.isLoading ? (
          <LoadingRows count={4} />
        ) : lists.isError ? (
          <ErrorState message="Không thể tải các bộ sưu tập." retry={() => void lists.refetch()} />
        ) : lists.data?.items.length ? (
          <>
            <div className="grid gap-5 lg:grid-cols-2">
              {lists.data.items.map((list) => (
                <BookListCard
                  key={list.id}
                  list={list}
                  actions={
                    <>
                      <button
                        type="button"
                        className="icon-button"
                        aria-label={`Chỉnh sửa ${list.name}`}
                        onClick={() => setEditor(list)}
                      >
                        <PencilSimple size={18} />
                      </button>
                      <button
                        type="button"
                        className="icon-button text-danger"
                        aria-label={`Xóa ${list.name}`}
                        disabled={deleteList.isPending}
                        onClick={() => void remove(list)}
                      >
                        <Trash size={18} />
                      </button>
                    </>
                  }
                />
              ))}
            </div>
            <Pagination
              page={lists.data.page}
              totalPages={lists.data.totalPages}
              onPageChange={setPage}
              disabled={lists.isFetching}
              className="mt-8"
            />
          </>
        ) : (
          <EmptyState
            title="Bắt đầu bộ sưu tập đầu tiên"
            description="Tạo một chủ đề nhỏ cho hành trình đọc: sách chữa lành, khoa học dễ đọc hay những câu chuyện muốn quay lại."
            icon={FolderPlus}
            action={
              <Button size="sm" icon={<Plus size={17} />} onClick={() => setEditor('create')}>
                Tạo ngay
              </Button>
            }
          />
        )}
      </main>

      <BookListFormDialog
        open={editor !== null}
        list={editor && editor !== 'create' ? editor : null}
        onClose={() => setEditor(null)}
      />
    </div>
  )
}
