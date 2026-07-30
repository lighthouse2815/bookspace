import {
  BookOpenText,
  MagnifyingGlass,
  NotePencil,
  PencilSimple,
  Plus,
  Quotes,
  Tag,
  Trash,
  X,
} from '@phosphor-icons/react'
import { useMemo, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { InputField, SelectField, TextareaField } from '../../components/ui/FormField'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useBook } from '../../hooks/useCatalog'
import { useLibrary } from '../../hooks/useReading'
import {
  useCreateReadingNote,
  useDeleteReadingNote,
  useReadingNotes,
  useUpdateReadingNote,
} from '../../hooks/useReadingProduct'
import { errorMessage } from '../../lib/api'
import { formatDate, formatRelativeTime } from '../../lib/format'
import type { ReadingNote } from '../../types/domain'

interface NoteFormState {
  bookId: string
  pageNumber: string
  quote: string
  content: string
  tags: string
}

function createNoteForm(bookId = ''): NoteFormState {
  return { bookId, pageNumber: '', quote: '', content: '', tags: '' }
}

function noteFormFromNote(note: ReadingNote): NoteFormState {
  return {
    bookId: note.bookId,
    pageNumber: note.pageNumber ? String(note.pageNumber) : '',
    quote: note.quote ?? '',
    content: note.content ?? '',
    tags: note.tags.join(', '),
  }
}

function parseTags(value: string) {
  return [...new Set(value.split(/[,;#]/).map((tag) => tag.trim()).filter(Boolean))]
}

export function NotesPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [selectedBookId, setSelectedBookId] = useState(() => searchParams.get('bookId') ?? '')
  const [searchText, setSearchText] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [selectedTag, setSelectedTag] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [editingNote, setEditingNote] = useState<ReadingNote | null>(null)
  const [form, setForm] = useState<NoteFormState>(() => createNoteForm(searchParams.get('bookId') ?? ''))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)
  const { showToast } = useToast()
  const library = useLibrary()
  const selectedBook = useBook(selectedBookId)
  const noteFilters = useMemo(
    () => ({
      bookId: selectedBookId || undefined,
      search: appliedSearch.trim() || undefined,
      tag: selectedTag || undefined,
    }),
    [appliedSearch, selectedBookId, selectedTag],
  )
  const notesQuery = useReadingNotes(noteFilters)
  const createNote = useCreateReadingNote()
  const updateNote = useUpdateReadingNote()
  const deleteNote = useDeleteReadingNote()

  const notes = notesQuery.data?.items ?? []
  const selectableBooks = useMemo(() => {
    const books = library.data?.items.map((entry) => entry.book) ?? []
    if (selectedBook.data && !books.some((book) => book.id === selectedBook.data?.id)) {
      return [selectedBook.data, ...books]
    }
    return books
  }, [library.data, selectedBook.data])
  const tags = useMemo(
    () =>
      [...new Set((notesQuery.data?.items ?? []).flatMap((note) => note.tags))].sort((left, right) =>
        left.localeCompare(right, 'vi'),
      ),
    [notesQuery.data?.items],
  )

  const closeForm = () => {
    setShowForm(false)
    setEditingNote(null)
    setForm(createNoteForm(selectedBookId))
    setErrors({})
  }

  const openCreate = () => {
    setEditingNote(null)
    setForm(createNoteForm(selectedBookId))
    setErrors({})
    setShowForm(true)
  }

  const openEdit = (note: ReadingNote) => {
    setEditingNote(note)
    setForm(noteFormFromNote(note))
    setErrors({})
    setShowForm(true)
    setPendingDeleteId(null)
  }

  const updateBookFilter = (bookId: string) => {
    setSelectedBookId(bookId)
    const next = new URLSearchParams(searchParams)
    if (bookId) next.set('bookId', bookId)
    else next.delete('bookId')
    setSearchParams(next)
  }

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    setAppliedSearch(searchText)
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const pageNumber = Number(form.pageNumber)
    const quote = form.quote.trim()
    const content = form.content.trim()
    const parsedTags = parseTags(form.tags)
    const nextErrors: Record<string, string> = {}

    if (!form.bookId) nextErrors.bookId = 'Chọn cuốn sách cho ghi chú này.'
    if (form.pageNumber && (!Number.isInteger(pageNumber) || pageNumber < 1)) {
      nextErrors.pageNumber = 'Số trang cần là số nguyên lớn hơn 0.'
    }
    if (!quote && !content) nextErrors.content = 'Nhập một trích dẫn hoặc nội dung ghi chú.'
    if (parsedTags.length > 10) nextErrors.tags = 'Dùng tối đa 10 thẻ để ghi chú dễ tìm lại.'
    if (parsedTags.some((tag) => tag.length > 30)) nextErrors.tags = 'Mỗi thẻ chỉ nên tối đa 30 ký tự.'

    if (Object.keys(nextErrors).length) {
      setErrors(nextErrors)
      return
    }

    const input = {
      bookId: form.bookId,
      pageNumber: form.pageNumber ? pageNumber : undefined,
      quote: quote || undefined,
      content: content || undefined,
      tags: parsedTags,
    }

    try {
      if (editingNote) {
        await updateNote.mutateAsync({ id: editingNote.id, input })
        showToast('Đã cập nhật ghi chú', 'success')
      } else {
        await createNote.mutateAsync(input)
        showToast('Đã lưu ghi chú', 'success')
      }
      closeForm()
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu ghi chú'), 'error')
    }
  }

  const removeNote = async (id: string) => {
    try {
      await deleteNote.mutateAsync(id)
      setPendingDeleteId(null)
      if (editingNote?.id === id) closeForm()
      showToast('Đã xóa ghi chú', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể xóa ghi chú'), 'error')
    }
  }

  return (
    <div className="container-page section-space max-w-6xl">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Những điều ở lại</p>
          <h1 className="page-title mt-4">Ghi chú sách</h1>
          <p className="mt-3 max-w-2xl text-muted">
            Lưu một câu văn, một trang đáng nhớ hoặc suy nghĩ riêng của bạn để quay lại đúng lúc cần.
          </p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link to="/journal" className="button button-secondary button-md">
            Mở nhật ký
          </Link>
          <Button icon={<Plus size={18} />} onClick={openCreate}>
            Thêm ghi chú
          </Button>
        </div>
      </div>

      <section className="mt-9 surface p-5 sm:p-6">
        <form onSubmit={submitSearch} className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_16rem_auto] lg:items-end">
          <div className="field">
            <label htmlFor="notes-search" className="field-label">
              Tìm trong ghi chú
            </label>
            <div className="relative">
              <MagnifyingGlass size={18} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
              <input
                id="notes-search"
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                className="input pl-10"
                placeholder="Từ khóa trong trích dẫn hoặc suy nghĩ của bạn"
              />
            </div>
          </div>
          <SelectField
            label="Cuốn sách"
            name="notes-book-filter"
            value={selectedBookId}
            onChange={(event) => updateBookFilter(event.target.value)}
          >
            <option value="">Tất cả cuốn sách</option>
            {selectableBooks.map((book) => (
              <option key={book.id} value={book.id}>
                {book.title}
              </option>
            ))}
            {selectedBookId && !selectableBooks.some((book) => book.id === selectedBookId) ? (
              <option value={selectedBookId}>Cuốn sách đã chọn</option>
            ) : null}
          </SelectField>
          <Button type="submit" variant="secondary" icon={<MagnifyingGlass size={17} />}>
            Tìm
          </Button>
        </form>

        {tags.length || selectedTag ? (
          <div className="mt-5 flex flex-wrap items-center gap-2 border-t border-border pt-5" aria-label="Lọc ghi chú theo thẻ">
            <Tag size={17} className="text-accent-strong" aria-hidden />
            <button
              type="button"
              onClick={() => setSelectedTag('')}
              className={`rounded-full px-3 py-1.5 text-sm font-semibold transition-colors focus-visible:focus-ring ${
                !selectedTag ? 'bg-accent text-white' : 'bg-surface-muted text-muted hover:bg-accent-soft hover:text-accent-strong'
              }`}
              aria-pressed={!selectedTag}
            >
              Tất cả thẻ
            </button>
            {selectedTag && !tags.includes(selectedTag) ? (
              <button
                type="button"
                onClick={() => setSelectedTag('')}
                className="rounded-full bg-accent-soft px-3 py-1.5 text-sm font-semibold text-accent-strong"
              >
                #{selectedTag} ×
              </button>
            ) : null}
            {tags.map((tag) => (
              <button
                key={tag}
                type="button"
                onClick={() => setSelectedTag(tag)}
                className={`rounded-full px-3 py-1.5 text-sm font-semibold transition-colors focus-visible:focus-ring ${
                  selectedTag === tag
                    ? 'bg-accent text-white'
                    : 'bg-surface-muted text-muted hover:bg-accent-soft hover:text-accent-strong'
                }`}
                aria-pressed={selectedTag === tag}
              >
                #{tag}
              </button>
            ))}
          </div>
        ) : null}
      </section>

      {showForm ? (
        <section className="mt-8 surface p-5 sm:p-7">
          <div className="flex items-start justify-between gap-5">
            <div>
              <h2 className="text-xl font-bold text-heading">{editingNote ? 'Sửa ghi chú' : 'Ghi chú mới'}</h2>
              <p className="mt-1 text-sm text-muted">Ghi lại điều cụ thể để lần đọc sau bạn tìm được nhanh hơn.</p>
            </div>
            <button type="button" className="icon-button" onClick={closeForm} aria-label="Đóng biểu mẫu ghi chú">
              <X size={18} />
            </button>
          </div>

          <form onSubmit={submit} className="mt-6 grid gap-5 md:grid-cols-2">
            <SelectField
              label="Cuốn sách"
              name="bookId"
              value={form.bookId}
              error={errors.bookId}
              onChange={(event) => {
                setForm({ ...form, bookId: event.target.value })
                setErrors({ ...errors, bookId: '' })
              }}
              required
            >
              <option value="">Chọn sách để ghi chú</option>
              {selectableBooks.map((book) => (
                <option key={book.id} value={book.id}>
                  {book.title}
                </option>
              ))}
              {form.bookId && !selectableBooks.some((book) => book.id === form.bookId) ? (
                <option value={form.bookId}>Cuốn sách đã chọn</option>
              ) : null}
            </SelectField>
            <InputField
              label="Số trang (không bắt buộc)"
              name="pageNumber"
              type="number"
              min={1}
              step={1}
              inputMode="numeric"
              value={form.pageNumber}
              error={errors.pageNumber}
              onChange={(event) => {
                setForm({ ...form, pageNumber: event.target.value })
                setErrors({ ...errors, pageNumber: '' })
              }}
            />
            <TextareaField
              label="Trích dẫn (không bắt buộc)"
              name="quote"
              value={form.quote}
              maxLength={500}
              className="min-h-32 md:col-span-2"
              hint={`${form.quote.length}/500 ký tự`}
              onChange={(event) => setForm({ ...form, quote: event.target.value })}
              placeholder="Một câu hoặc đoạn bạn muốn giữ lại…"
            />
            <TextareaField
              label="Suy nghĩ của bạn"
              name="content"
              value={form.content}
              maxLength={5000}
              error={errors.content}
              className="min-h-36 md:col-span-2"
              hint={`${form.content.length}/5000 ký tự`}
              onChange={(event) => {
                setForm({ ...form, content: event.target.value })
                setErrors({ ...errors, content: '' })
              }}
              placeholder="Điều gì trong trang này làm bạn dừng lại?"
            />
            <InputField
              label="Thẻ"
              name="tags"
              value={form.tags}
              error={errors.tags}
              hint="Ngăn cách thẻ bằng dấu phẩy, ví dụ: nhân vật, gia đình, cần suy ngẫm"
              className="md:col-span-2"
              onChange={(event) => {
                setForm({ ...form, tags: event.target.value })
                setErrors({ ...errors, tags: '' })
              }}
            />
            <div className="flex flex-wrap items-center gap-3 md:col-span-2">
              <Button type="submit" loading={createNote.isPending || updateNote.isPending} icon={<NotePencil size={18} />}>
                {editingNote ? 'Lưu thay đổi' : 'Lưu ghi chú'}
              </Button>
              <Button type="button" variant="ghost" onClick={closeForm}>
                Hủy
              </Button>
            </div>
          </form>
        </section>
      ) : null}

      <section className="mt-10">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-heading">Kho ghi chú</h2>
            <p className="mt-1 text-sm text-muted">
              {notesQuery.data ? `${notesQuery.data.totalItems.toLocaleString('vi-VN')} ghi chú phù hợp` : 'Các ghi chú của bạn'}
            </p>
          </div>
          {appliedSearch || selectedBookId || selectedTag ? (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setAppliedSearch('')
                setSelectedTag('')
                updateBookFilter('')
              }}
              className="text-sm font-semibold text-accent-strong hover:underline focus-visible:focus-ring"
            >
              Xóa bộ lọc
            </button>
          ) : null}
        </div>

        <div className="mt-5">
          {notesQuery.isLoading ? (
            <LoadingRows count={5} />
          ) : notesQuery.isError ? (
            <ErrorState message="Không thể tải ghi chú. Hãy thử lại sau ít phút." retry={() => void notesQuery.refetch()} />
          ) : notes.length ? (
            <div className="space-y-4">
              {notes.map((note) => (
                <article key={note.id} className="surface p-5 sm:p-6">
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="flex min-w-0 items-start gap-3">
                      <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                        <BookOpenText size={22} weight="duotone" />
                      </div>
                      <div className="min-w-0">
                        <Link to={`/books/${note.bookId}`} className="font-semibold text-heading hover:text-accent-strong hover:underline">
                          {note.book?.title || 'Cuốn sách đã lưu'}
                        </Link>
                        <p className="mt-1 text-xs text-muted">
                          {note.pageNumber ? `Trang ${note.pageNumber} · ` : ''}
                          {note.updatedAt ? `Cập nhật ${formatRelativeTime(note.updatedAt)}` : formatDate(note.createdAt)}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        className="icon-button"
                        onClick={() => openEdit(note)}
                        aria-label={`Sửa ghi chú cho ${note.book?.title || 'cuốn sách'}`}
                      >
                        <PencilSimple size={18} />
                      </button>
                      <button
                        type="button"
                        className="icon-button text-red-700 hover:bg-red-50 dark:text-red-300 dark:hover:bg-red-950/30"
                        onClick={() => setPendingDeleteId(note.id)}
                        aria-label={`Xóa ghi chú cho ${note.book?.title || 'cuốn sách'}`}
                      >
                        <Trash size={18} />
                      </button>
                    </div>
                  </div>

                  {note.quote ? (
                    <blockquote className="mt-5 border-l-2 border-accent pl-4 text-base leading-7 text-heading">
                      <Quotes size={18} weight="fill" className="mb-2 text-accent-strong" aria-hidden />
                      <p className="whitespace-pre-line">{note.quote}</p>
                    </blockquote>
                  ) : null}
                  {note.content ? <p className="mt-4 whitespace-pre-line text-sm leading-7 text-body">{note.content}</p> : null}
                  {note.tags.length ? (
                    <div className="mt-5 flex flex-wrap gap-2">
                      {note.tags.map((tag) => (
                        <button
                          key={tag}
                          type="button"
                          onClick={() => setSelectedTag(tag)}
                          className="rounded-full bg-surface-muted px-2.5 py-1 text-xs font-semibold text-muted transition-colors hover:bg-accent-soft hover:text-accent-strong focus-visible:focus-ring"
                        >
                          #{tag}
                        </button>
                      ))}
                    </div>
                  ) : null}

                  {pendingDeleteId === note.id ? (
                    <div className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-surface-muted p-3">
                      <p className="text-sm text-heading">Xóa ghi chú này? Hành động không thể hoàn tác.</p>
                      <div className="flex gap-2">
                        <Button type="button" variant="ghost" size="sm" onClick={() => setPendingDeleteId(null)}>
                          Hủy
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          size="sm"
                          loading={deleteNote.isPending}
                          onClick={() => void removeNote(note.id)}
                        >
                          Xóa ghi chú
                        </Button>
                      </div>
                    </div>
                  ) : null}
                </article>
              ))}
            </div>
          ) : (
            <EmptyState
              icon={Quotes}
              title={selectedBookId || appliedSearch || selectedTag ? 'Không tìm thấy ghi chú phù hợp' : 'Chưa có ghi chú nào'}
              description={
                selectedBookId || appliedSearch || selectedTag
                  ? 'Thử đổi từ khóa hoặc xóa bộ lọc để xem lại tất cả ghi chú.'
                  : 'Lưu lại một trích dẫn hoặc suy nghĩ ngay khi nó còn ở lại với bạn.'
              }
              action={
                selectedBookId || appliedSearch || selectedTag ? (
                  <Button
                    variant="secondary"
                    onClick={() => {
                      setSearchText('')
                      setAppliedSearch('')
                      setSelectedTag('')
                      updateBookFilter('')
                    }}
                  >
                    Xóa bộ lọc
                  </Button>
                ) : (
                  <Button onClick={openCreate}>Tạo ghi chú đầu tiên</Button>
                )
              }
            />
          )}
        </div>
      </section>
    </div>
  )
}
