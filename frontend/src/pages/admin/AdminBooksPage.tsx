import { NotePencil, Plus, Trash, X } from '@phosphor-icons/react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { AdminNav } from '../../components/admin/AdminNav'
import { BookCover } from '../../components/books/BookCover'
import { Button } from '../../components/ui/Button'
import { InputField, SelectField, TextareaField } from '../../components/ui/FormField'
import { ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useAuthors, useBooks, useCategories } from '../../hooks/useCatalog'
import { errorMessage } from '../../lib/api'
import { adminService, type BookAdminInput } from '../../services/admin.service'
import type { Book } from '../../types/domain'

const emptyBook: BookAdminInput = {
  title: '',
  authorId: '',
  categoryIds: [],
  description: '',
  isbn: '',
  coverImageUrl: '',
  pageCount: 1,
  publishedYear: new Date().getFullYear(),
}

export function AdminBooksPage() {
  const books = useBooks({ page: 1, pageSize: 100, sort: 'newest' })
  const authors = useAuthors()
  const categories = useCategories()
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [editing, setEditing] = useState<Book | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<BookAdminInput>(emptyBook)

  const save = useMutation({
    mutationFn: (input: BookAdminInput) =>
      editing ? adminService.updateBook(editing.id, input) : adminService.createBook(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
      showToast(editing ? 'Đã cập nhật sách' : 'Đã thêm sách vào catalog', 'success')
      closeForm()
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const remove = useMutation({
    mutationFn: adminService.deleteBook,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
      showToast('Đã xóa sách khỏi catalog', 'success')
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const closeForm = () => {
    setEditing(null)
    setForm(emptyBook)
    setShowForm(false)
  }

  const editBook = (book: Book) => {
    setEditing(book)
    setForm({
      title: book.title,
      authorId: book.author?.id || book.authorId || '',
      categoryIds: book.categories?.map((category) => category.id) ?? [],
      description: book.description ?? '',
      isbn: book.isbn ?? '',
      coverImageUrl: book.coverImageUrl ?? '',
      pageCount: book.pageCount ?? 1,
      publishedYear: book.publishedYear ?? new Date().getFullYear(),
    })
    setShowForm(true)
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!form.title.trim() || !form.authorId || !form.categoryIds.length) {
      showToast('Tên sách, tác giả và ít nhất một chủ đề là bắt buộc', 'error')
      return
    }
    save.mutate({
      ...form,
      title: form.title.trim(),
      description: form.description?.trim() || undefined,
      isbn: form.isbn?.trim() || undefined,
      coverImageUrl: form.coverImageUrl?.trim() || undefined,
    })
  }

  return (
    <div className="container-page section-space">
      <p className="eyebrow">Quản trị BookSpace</p>
      <div className="mt-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="page-title">Catalog sách</h1>
          <p className="mt-3 text-muted">Quản lý dữ liệu sách độc lập của BookSpace.</p>
        </div>
        <Button
          icon={showForm ? <X size={18} /> : <Plus size={18} />}
          onClick={() => (showForm ? closeForm() : setShowForm(true))}
        >
          {showForm ? 'Đóng biểu mẫu' : 'Thêm sách'}
        </Button>
      </div>
      <AdminNav />

      {showForm ? (
        <form onSubmit={submit} className="mb-8 surface p-5 sm:p-7">
          <h2 className="text-xl font-bold text-heading">{editing ? 'Chỉnh sửa sách' : 'Sách mới'}</h2>
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <InputField
              label="Tên sách"
              name="title"
              value={form.title}
              onChange={(event) => setForm({ ...form, title: event.target.value })}
              required
            />
            <SelectField
              label="Tác giả"
              name="authorId"
              value={form.authorId}
              onChange={(event) => setForm({ ...form, authorId: event.target.value })}
              required
            >
              <option value="">Chọn tác giả</option>
              {authors.data?.items.map((author) => (
                <option key={author.id} value={author.id}>
                  {author.name}
                </option>
              ))}
            </SelectField>
            <InputField
              label="ISBN"
              name="isbn"
              value={form.isbn}
              onChange={(event) => setForm({ ...form, isbn: event.target.value })}
            />
            <InputField
              label="URL ảnh bìa"
              name="coverImageUrl"
              type="url"
              value={form.coverImageUrl}
              onChange={(event) => setForm({ ...form, coverImageUrl: event.target.value })}
            />
            <InputField
              label="Số trang"
              name="pageCount"
              type="number"
              min={1}
              value={form.pageCount}
              onChange={(event) => setForm({ ...form, pageCount: Number(event.target.value) })}
            />
            <InputField
              label="Năm xuất bản"
              name="publishedYear"
              type="number"
              min={1000}
              max={new Date().getFullYear() + 1}
              value={form.publishedYear}
              onChange={(event) => setForm({ ...form, publishedYear: Number(event.target.value) })}
            />
            <TextareaField
              label="Mô tả"
              name="description"
              value={form.description}
              className="md:col-span-2"
              onChange={(event) => setForm({ ...form, description: event.target.value })}
            />
            <fieldset className="md:col-span-2">
              <legend className="field-label">Chủ đề</legend>
              <div className="mt-2 flex flex-wrap gap-2">
                {categories.data?.items.map((category) => {
                  const selected = form.categoryIds.includes(category.id)
                  return (
                    <label
                      key={category.id}
                      className={`cursor-pointer rounded-full border px-3 py-2 text-sm font-medium ${
                        selected
                          ? 'border-accent bg-accent-soft text-accent-strong'
                          : 'border-border bg-surface text-body'
                      }`}
                    >
                      <input
                        type="checkbox"
                        className="sr-only"
                        checked={selected}
                        onChange={() =>
                          setForm({
                            ...form,
                            categoryIds: selected
                              ? form.categoryIds.filter((id) => id !== category.id)
                              : [...form.categoryIds, category.id],
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
          <div className="mt-6 flex justify-end">
            <Button type="submit" loading={save.isPending}>
              {editing ? 'Lưu thay đổi' : 'Thêm vào catalog'}
            </Button>
          </div>
        </form>
      ) : null}

      {books.isLoading ? (
        <LoadingRows count={6} />
      ) : books.isError ? (
        <ErrorState message="Không thể tải catalog quản trị." retry={() => void books.refetch()} />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-border">
          <table className="w-full min-w-[760px] border-collapse text-left text-sm">
            <thead className="bg-surface-muted text-xs uppercase tracking-wider text-muted">
              <tr>
                <th className="px-4 py-3 font-semibold">Sách</th>
                <th className="px-4 py-3 font-semibold">Tác giả</th>
                <th className="px-4 py-3 font-semibold">ISBN</th>
                <th className="px-4 py-3 font-semibold">Năm</th>
                <th className="px-4 py-3 text-right font-semibold">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border bg-surface">
              {books.data?.items.map((book) => (
                <tr key={book.id}>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <BookCover src={book.coverImageUrl} title={book.title} className="h-14 w-10 rounded-md" />
                      <span className="max-w-72 font-semibold text-heading">{book.title}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted">{book.author?.name || 'Chưa cập nhật'}</td>
                  <td className="px-4 py-3 text-muted">{book.isbn || 'Không có'}</td>
                  <td className="px-4 py-3 text-muted">{book.publishedYear || 'Không rõ'}</td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-1">
                      <button
                        type="button"
                        className="icon-button"
                        onClick={() => editBook(book)}
                        aria-label={`Chỉnh sửa ${book.title}`}
                      >
                        <NotePencil size={18} />
                      </button>
                      <button
                        type="button"
                        className="icon-button text-red-600"
                        disabled={remove.isPending}
                        onClick={() => {
                          if (window.confirm(`Xóa "${book.title}" khỏi catalog?`)) remove.mutate(book.id)
                        }}
                        aria-label={`Xóa ${book.title}`}
                      >
                        <Trash size={18} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
