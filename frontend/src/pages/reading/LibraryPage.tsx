import { Books, Trash } from '@phosphor-icons/react'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { BookCover } from '../../components/books/BookCover'
import { Button } from '../../components/ui/Button'
import { Progress } from '../../components/ui/Progress'
import { EmptyState, ErrorState, LoadingGrid } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useLibrary, useRemoveFromLibrary, useUpdateLibrary } from '../../hooks/useReading'
import { errorMessage } from '../../lib/api'
import { shelfLabel } from '../../lib/format'
import type { LibraryEntry, Shelf } from '../../types/domain'

const shelves: Array<{ value: Shelf | 'ALL'; label: string }> = [
  { value: 'ALL', label: 'Tất cả' },
  { value: 'WANT_TO_READ', label: 'Muốn đọc' },
  { value: 'READING', label: 'Đang đọc' },
  { value: 'READ', label: 'Đã đọc' },
]

function LibraryRow({ entry }: { entry: LibraryEntry }) {
  const update = useUpdateLibrary()
  const remove = useRemoveFromLibrary()
  const { showToast } = useToast()

  const updateShelf = async (shelf: Shelf) => {
    try {
      await update.mutateAsync({ id: entry.id, input: { shelf } })
      showToast(`Đã chuyển sang ${shelfLabel(shelf).toLowerCase()}`, 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  const updatePage = async (value: number) => {
    const total = entry.book.pageCount ?? value
    const currentPage = Math.max(0, Math.min(value, total))
    try {
      await update.mutateAsync({
        id: entry.id,
        input: {
          currentPage,
          progressPercent: total ? Math.round((currentPage / total) * 100) : 0,
        },
      })
      showToast('Đã cập nhật tiến độ', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  const removeEntry = async () => {
    try {
      await remove.mutateAsync(entry.id)
      showToast('Đã xóa khỏi thư viện', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  return (
    <article className="surface grid gap-5 p-4 sm:grid-cols-[6rem_1fr] sm:p-5">
      <Link to={`/books/${entry.book.id}`}>
        <BookCover
          src={entry.book.coverImageUrl}
          title={entry.book.title}
          className="aspect-[2/3] w-24 rounded-xl sm:w-full"
        />
      </Link>
      <div className="min-w-0">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <Link to={`/books/${entry.book.id}`} className="font-semibold text-heading hover:text-accent-strong">
              {entry.book.title}
            </Link>
            <p className="mt-1 text-sm text-muted">{entry.book.author?.name}</p>
          </div>
          <select
            className="input w-36 py-2 text-sm"
            value={entry.shelf}
            disabled={update.isPending}
            onChange={(event) => void updateShelf(event.target.value as Shelf)}
            aria-label={`Đổi kệ cho ${entry.book.title}`}
          >
            {shelves.slice(1).map((shelf) => (
              <option key={shelf.value} value={shelf.value}>
                {shelf.label}
              </option>
            ))}
          </select>
        </div>
        {entry.shelf === 'READING' ? (
          <div className="mt-5">
            <Progress
              value={entry.progressPercent}
              label={`${entry.currentPage}/${entry.book.pageCount ?? '?'} trang`}
            />
            <form
              className="mt-4 flex flex-wrap items-end gap-2"
              onSubmit={(event) => {
                event.preventDefault()
                const form = new FormData(event.currentTarget)
                void updatePage(Number(form.get('currentPage')))
              }}
            >
              <label className="field max-w-36">
                <span className="field-label">Trang hiện tại</span>
                <input
                  name="currentPage"
                  type="number"
                  min={0}
                  max={entry.book.pageCount}
                  defaultValue={entry.currentPage}
                  className="input py-2"
                />
              </label>
              <Button type="submit" variant="secondary" size="sm" loading={update.isPending}>
                Cập nhật
              </Button>
            </form>
          </div>
        ) : (
          <p className="mt-5 text-sm text-muted">
            {entry.shelf === 'READ'
              ? 'Bạn đã hoàn thành cuốn sách này.'
              : 'Chuyển sang Đang đọc khi bạn bắt đầu.'}
          </p>
        )}
        <Button
          variant="ghost"
          size="sm"
          icon={<Trash size={16} />}
          loading={remove.isPending}
          onClick={() => void removeEntry()}
          className="mt-4"
        >
          Xóa khỏi thư viện
        </Button>
      </div>
    </article>
  )
}

export function LibraryPage() {
  const [params, setParams] = useSearchParams()
  const selected = (params.get('shelf') as Shelf | null) ?? 'ALL'
  const shelf = selected === 'ALL' ? undefined : selected
  const library = useLibrary(shelf)
  const counts = useMemo(() => library.data?.items.length ?? 0, [library.data])

  return (
    <div className="container-page section-space">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Không gian của bạn</p>
          <h1 className="page-title mt-4">Thư viện cá nhân</h1>
          <p className="mt-3 text-muted">Sắp xếp sách theo đúng trạng thái đọc hiện tại.</p>
        </div>
        <Link to="/books" className="button button-primary button-md">
          Tìm thêm sách
        </Link>
      </div>

      <div className="mt-9 flex gap-2 overflow-x-auto pb-2" role="tablist" aria-label="Lọc kệ sách">
        {shelves.map((item) => (
          <button
            key={item.value}
            type="button"
            className={`filter-tab ${selected === item.value ? 'filter-active' : ''}`}
            onClick={() => {
              if (item.value === 'ALL') setParams({})
              else setParams({ shelf: item.value })
            }}
            role="tab"
            aria-selected={selected === item.value}
          >
            {item.label}
          </button>
        ))}
      </div>

      <p className="mt-5 text-sm text-muted">{counts} cuốn sách trong kệ này</p>

      <div className="mt-6">
        {library.isLoading ? (
          <LoadingGrid count={6} />
        ) : library.isError ? (
          <ErrorState message="Không thể tải thư viện." retry={() => void library.refetch()} />
        ) : library.data?.items.length ? (
          <div className="grid gap-4 xl:grid-cols-2">
            {library.data.items.map((entry) => (
              <LibraryRow key={entry.id} entry={entry} />
            ))}
          </div>
        ) : (
          <EmptyState
            icon={Books}
            title="Kệ sách đang trống"
            description="Khám phá catalog và thêm cuốn sách đầu tiên vào hành trình của bạn."
            action={
              <Link to="/books" className="button button-primary button-md">
                Khám phá sách
              </Link>
            }
          />
        )}
      </div>
    </div>
  )
}
