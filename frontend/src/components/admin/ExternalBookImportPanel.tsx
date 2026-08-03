import { ArrowLeft, DownloadSimple, MagnifyingGlass } from '@phosphor-icons/react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { errorMessage } from '../../lib/api'
import {
  adminService,
  type ExternalBookImportInput,
} from '../../services/admin.service'
import type { Author, Category, ExternalBook, ExternalBookImportResult } from '../../types/domain'
import { BookCover } from '../books/BookCover'
import { Button } from '../ui/Button'
import { InputField, SelectField, TextareaField } from '../ui/FormField'
import { useToast } from '../../contexts/ToastContext'

interface ExternalBookImportPanelProps {
  authors: Author[]
  categories: Category[]
  onClose: () => void
}

interface ImportDraft {
  authorId: string
  authorName: string
  categoryIds: string[]
  categoryNames: string
  description: string
  pageCount: string
  publishedYear: string
  language: string
}

function draftFromBook(book: ExternalBook): ImportDraft {
  return {
    authorId: '',
    authorName: book.authors[0] ?? '',
    categoryIds: [],
    categoryNames: book.categories.join(', '),
    description: book.description ?? '',
    pageCount: book.pageCount ? String(book.pageCount) : '',
    publishedYear: book.publishedYear ? String(book.publishedYear) : '',
    language: book.language ?? 'vi',
  }
}

function categoryNames(value: string) {
  return Array.from(
    new Set(
      value
        .split(',')
        .map((name) => name.trim())
        .filter(Boolean),
    ),
  )
}

function successMessage(result: ExternalBookImportResult) {
  if (result.status === 'ALREADY_IMPORTED') {
    return `“${result.book.title}” đã có trong catalog từ lần import trước.`
  }
  if (result.status === 'LINKED_EXISTING') {
    return `Đã liên kết nguồn ngoài với “${result.book.title}” theo ISBN.`
  }
  return `Đã import “${result.book.title}” vào catalog BookSpace.`
}

export function ExternalBookImportPanel({
  authors,
  categories,
  onClose,
}: ExternalBookImportPanelProps) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<ExternalBook | null>(null)
  const [draft, setDraft] = useState<ImportDraft | null>(null)

  const search = useMutation({
    mutationFn: adminService.searchExternalBooks,
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const importBook = useMutation({
    mutationFn: adminService.importExternalBook,
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
      showToast(successMessage(result), 'success')
      setSelected(null)
      setDraft(null)
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    const normalized = query.trim()
    if (!normalized) {
      showToast('Hãy nhập tên sách, tác giả hoặc ISBN.', 'error')
      return
    }
    setSelected(null)
    setDraft(null)
    search.mutate(normalized)
  }

  const selectBook = (book: ExternalBook) => {
    setSelected(book)
    setDraft(draftFromBook(book))
  }

  const submitImport = (event: FormEvent) => {
    event.preventDefault()
    if (!selected || !draft || !search.data) return

    const newCategoryNames = categoryNames(draft.categoryNames)
    if (!draft.authorId && !draft.authorName.trim()) {
      showToast('Hãy chọn hoặc nhập tên tác giả.', 'error')
      return
    }
    if (!draft.categoryIds.length && !newCategoryNames.length) {
      showToast('Hãy chọn hoặc nhập ít nhất một thể loại.', 'error')
      return
    }
    const pageCount = Number(draft.pageCount)
    if (!Number.isInteger(pageCount) || pageCount <= 0) {
      showToast('Số trang phải là số nguyên lớn hơn 0.', 'error')
      return
    }

    const input: ExternalBookImportInput = {
      provider: search.data.provider,
      externalId: selected.externalId,
      authorId: draft.authorId || undefined,
      authorName: draft.authorName.trim() || undefined,
      categoryIds: draft.categoryIds,
      categoryNames: newCategoryNames,
      description: draft.description.trim() || undefined,
      pageCount,
      publishedYear: draft.publishedYear ? Number(draft.publishedYear) : undefined,
      language: draft.language.trim() || undefined,
    }
    importBook.mutate(input)
  }

  return (
    <section className="mb-8 surface overflow-hidden" aria-labelledby="external-import-title">
      <div className="border-b border-border bg-surface-muted/70 p-5 sm:p-7">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="eyebrow">Catalog enrichment</p>
            <h2 id="external-import-title" className="mt-2 text-2xl font-bold text-heading">
              Import sách từ nguồn ngoài
            </h2>
            <p className="mt-2 max-w-3xl text-sm leading-6 text-muted">
              Tìm metadata, kiểm tra lại tác giả và thể loại, rồi tạo một bản ghi do BookSpace sở hữu.
            </p>
          </div>
          <Button variant="ghost" size="sm" icon={<ArrowLeft size={17} />} onClick={onClose}>
            Quay lại catalog
          </Button>
        </div>

        <form onSubmit={submitSearch} className="mt-6 flex flex-col gap-3 sm:flex-row">
          <label className="sr-only" htmlFor="external-book-query">
            Tìm sách từ nguồn ngoài
          </label>
          <input
            id="external-book-query"
            className="input min-w-0 flex-1"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Tên sách, tác giả hoặc ISBN"
          />
          <Button type="submit" loading={search.isPending} icon={<MagnifyingGlass size={18} />}>
            Tìm metadata
          </Button>
        </form>
      </div>

      <div className="p-5 sm:p-7">
        {search.data && !search.data.available ? (
          <div className="rounded-2xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900" role="status">
            {search.data.message}
          </div>
        ) : null}

        {search.data?.available && search.data.items.length === 0 ? (
          <p className="rounded-2xl border border-border bg-surface-muted p-5 text-sm text-muted" role="status">
            {search.data.message}
          </p>
        ) : null}

        {!selected && search.data?.items.length ? (
          <div>
            <div className="mb-4 flex items-center justify-between gap-3">
              <h3 className="font-semibold text-heading">Kết quả từ {search.data.provider}</h3>
              <span className="text-xs font-medium text-muted">{search.data.items.length} kết quả</span>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {search.data.items.map((book) => (
                <article key={book.externalId} className="rounded-2xl border border-border p-4">
                  <div className="flex gap-4">
                    <BookCover
                      src={book.coverImageUrl ?? undefined}
                      title={book.title}
                      className="h-24 w-16 shrink-0 rounded-lg"
                    />
                    <div className="min-w-0 flex-1">
                      <h4 className="line-clamp-2 font-semibold text-heading">{book.title}</h4>
                      <p className="mt-1 line-clamp-1 text-sm text-muted">
                        {book.authors.join(', ') || 'Chưa có tác giả'}
                      </p>
                      <p className="mt-2 text-xs text-muted">ISBN: {book.isbn || 'Chưa có'}</p>
                    </div>
                  </div>
                  <Button
                    variant="secondary"
                    size="sm"
                    className="mt-4 w-full"
                    onClick={() => selectBook(book)}
                  >
                    Xem trước và import
                  </Button>
                </article>
              ))}
            </div>
          </div>
        ) : null}

        {selected && draft && search.data ? (
          <form onSubmit={submitImport} aria-label="Xác nhận import sách">
            <button
              type="button"
              className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-accent-strong hover:underline"
              onClick={() => {
                setSelected(null)
                setDraft(null)
              }}
            >
              <ArrowLeft size={16} /> Chọn kết quả khác
            </button>

            <div className="grid gap-6 lg:grid-cols-[15rem_1fr]">
              <div className="rounded-2xl border border-border bg-surface-muted p-5">
                <BookCover
                  src={selected.coverImageUrl ?? undefined}
                  title={selected.title}
                  className="mx-auto aspect-[2/3] w-32 rounded-xl"
                />
                <h3 className="mt-4 text-center text-lg font-bold text-heading">{selected.title}</h3>
                <dl className="mt-4 space-y-2 text-xs text-muted">
                  <div className="flex justify-between gap-3">
                    <dt>Nguồn</dt>
                    <dd className="font-semibold text-heading">{search.data.provider}</dd>
                  </div>
                  <div className="flex justify-between gap-3">
                    <dt>ISBN</dt>
                    <dd className="text-right font-semibold text-heading">{selected.isbn || 'Chưa có'}</dd>
                  </div>
                </dl>
              </div>

              <div>
                <h3 className="text-xl font-bold text-heading">Kiểm tra metadata nội bộ</h3>
                <p className="mt-2 text-sm text-muted">
                  ISBN trùng sẽ liên kết với sách hiện có; dữ liệu BookSpace không bị ghi đè.
                </p>
                <div className="mt-6 grid gap-5 md:grid-cols-2">
                  <SelectField
                    label="Ghép với tác giả hiện có"
                    name="externalAuthorId"
                    value={draft.authorId}
                    onChange={(event) => setDraft({ ...draft, authorId: event.target.value })}
                  >
                    <option value="">Tự tạo hoặc ghép theo tên</option>
                    {authors.map((author) => (
                      <option key={author.id} value={author.id}>
                        {author.name}
                      </option>
                    ))}
                  </SelectField>
                  <InputField
                    label="Tên tác giả từ nguồn"
                    name="externalAuthorName"
                    value={draft.authorName}
                    disabled={Boolean(draft.authorId)}
                    onChange={(event) => setDraft({ ...draft, authorName: event.target.value })}
                    hint="Tên trùng sẽ được ghép, tên mới sẽ được tạo tự động."
                  />
                  <InputField
                    label="Số trang"
                    name="externalPageCount"
                    type="number"
                    min={1}
                    value={draft.pageCount}
                    onChange={(event) => setDraft({ ...draft, pageCount: event.target.value })}
                    required
                  />
                  <InputField
                    label="Năm xuất bản"
                    name="externalPublishedYear"
                    type="number"
                    min={1000}
                    max={2200}
                    value={draft.publishedYear}
                    onChange={(event) => setDraft({ ...draft, publishedYear: event.target.value })}
                  />
                  <InputField
                    label="Ngôn ngữ"
                    name="externalLanguage"
                    value={draft.language}
                    onChange={(event) => setDraft({ ...draft, language: event.target.value })}
                  />
                  <InputField
                    label="Thể loại mới"
                    name="externalCategoryNames"
                    value={draft.categoryNames}
                    onChange={(event) => setDraft({ ...draft, categoryNames: event.target.value })}
                    hint="Phân tách nhiều tên bằng dấu phẩy; tên trùng sẽ được ghép."
                  />
                  <TextareaField
                    label="Mô tả"
                    name="externalDescription"
                    className="md:col-span-2"
                    value={draft.description}
                    onChange={(event) => setDraft({ ...draft, description: event.target.value })}
                  />
                  <fieldset className="md:col-span-2">
                    <legend className="field-label">Ghép với thể loại hiện có</legend>
                    <div className="mt-2 flex flex-wrap gap-2">
                      {categories.map((category) => {
                        const checked = draft.categoryIds.includes(category.id)
                        return (
                          <label
                            key={category.id}
                            className={`cursor-pointer rounded-full border px-3 py-2 text-sm font-medium ${
                              checked
                                ? 'border-accent bg-accent-soft text-accent-strong'
                                : 'border-border bg-surface text-body'
                            }`}
                          >
                            <input
                              type="checkbox"
                              className="sr-only"
                              checked={checked}
                              onChange={() =>
                                setDraft({
                                  ...draft,
                                  categoryIds: checked
                                    ? draft.categoryIds.filter((id) => id !== category.id)
                                    : [...draft.categoryIds, category.id],
                                })
                              }
                            />
                            {category.name}
                          </label>
                        )
                      })}
                    </div>
                  </fieldset>
                </div>
                <div className="mt-7 flex justify-end">
                  <Button type="submit" loading={importBook.isPending} icon={<DownloadSimple size={18} />}>
                    Import vào BookSpace
                  </Button>
                </div>
              </div>
            </div>
          </form>
        ) : null}
      </div>
    </section>
  )
}
