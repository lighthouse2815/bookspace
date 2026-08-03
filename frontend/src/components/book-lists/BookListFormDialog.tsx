import { X } from '@phosphor-icons/react'
import { useEffect, useState, type FormEvent } from 'react'
import { useToast } from '../../contexts/ToastContext'
import { useCreateBookList, useUpdateBookList } from '../../hooks/useBookLists'
import { errorMessage } from '../../lib/api'
import type { BookListDetail, BookListSummary, BookListVisibility } from '../../types/domain'
import { Button } from '../ui/Button'

type EditableList = Pick<BookListSummary | BookListDetail, 'id' | 'name' | 'description' | 'visibility'>

export function BookListFormDialog({
  open,
  list,
  onClose,
}: {
  open: boolean
  list?: EditableList | null
  onClose: () => void
}) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [visibility, setVisibility] = useState<BookListVisibility>('PUBLIC')
  const { showToast } = useToast()
  const createList = useCreateBookList()
  const updateList = useUpdateBookList(list?.id ?? '')

  useEffect(() => {
    if (!open) return
    setName(list?.name ?? '')
    setDescription(list?.description ?? '')
    setVisibility(list?.visibility ?? 'PUBLIC')
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [list, onClose, open])

  if (!open) return null

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!name.trim()) {
      showToast('Tên bộ sưu tập không được để trống', 'error')
      return
    }
    try {
      const input = { name: name.trim(), description: description.trim() || null, visibility }
      if (list) await updateList.mutateAsync(input)
      else await createList.mutateAsync(input)
      showToast(list ? 'Đã cập nhật bộ sưu tập' : 'Đã tạo bộ sưu tập mới', 'success')
      onClose()
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu bộ sưu tập'), 'error')
    }
  }

  const pending = createList.isPending || updateList.isPending

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center p-4" role="presentation">
      <button
        type="button"
        className="absolute inset-0 bg-slate-950/55 backdrop-blur-sm"
        aria-label="Đóng biểu mẫu"
        onClick={onClose}
      />
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="book-list-form-title"
        className="surface relative z-10 w-full max-w-lg p-6 shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="eyebrow">Bộ sưu tập cá nhân</p>
            <h2 id="book-list-form-title" className="mt-2 text-2xl font-bold text-heading">
              {list ? 'Chỉnh sửa bộ sưu tập' : 'Tạo bộ sưu tập mới'}
            </h2>
          </div>
          <button type="button" className="icon-button" aria-label="Đóng" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <form className="mt-6 space-y-5" onSubmit={submit}>
          <label className="block">
            <span className="field-label">Tên bộ sưu tập</span>
            <input
              className="input mt-2"
              value={name}
              maxLength={120}
              autoFocus
              onChange={(event) => setName(event.target.value)}
              placeholder="Ví dụ: Những cuốn muốn đọc mùa mưa"
            />
          </label>
          <label className="block">
            <span className="field-label">Mô tả</span>
            <textarea
              className="input mt-2 min-h-28 resize-y"
              value={description}
              maxLength={1000}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Chia sẻ tinh thần của bộ sưu tập này..."
            />
          </label>
          <fieldset>
            <legend className="field-label">Quyền xem</legend>
            <div className="mt-2 grid gap-2 sm:grid-cols-2">
              {([
                ['PUBLIC', 'Công khai', 'Mọi người đều có thể xem.'],
                ['PRIVATE', 'Riêng tư', 'Chỉ mình bạn nhìn thấy.'],
              ] as const).map(([value, label, hint]) => (
                <label
                  key={value}
                  className={`cursor-pointer rounded-xl border p-4 ${visibility === value ? 'border-accent bg-accent-soft' : 'border-border bg-surface'}`}
                >
                  <input
                    type="radio"
                    name="visibility"
                    value={value}
                    checked={visibility === value}
                    onChange={() => setVisibility(value)}
                    className="sr-only"
                  />
                  <span className="font-semibold text-heading">{label}</span>
                  <span className="mt-1 block text-xs text-muted">{hint}</span>
                </label>
              ))}
            </div>
          </fieldset>
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="ghost" onClick={onClose}>Hủy</Button>
            <Button type="submit" loading={pending}>Lưu bộ sưu tập</Button>
          </div>
        </form>
      </section>
    </div>
  )
}
