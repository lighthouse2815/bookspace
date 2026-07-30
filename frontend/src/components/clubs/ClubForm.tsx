import { LockSimple, UsersThree } from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import type { SaveClubInput } from '../../services/club.service'
import { Button } from '../ui/Button'
import { InputField, TextareaField } from '../ui/FormField'

interface ClubFormProps {
  initialValue?: SaveClubInput
  submitLabel: string
  loading?: boolean
  autoFocus?: boolean
  onSubmit: (input: SaveClubInput) => Promise<void> | void
  onCancel?: () => void
}

const emptyClub: SaveClubInput = {
  name: '',
  description: '',
  coverImageUrl: '',
  isPrivate: false,
}

export function ClubForm({
  initialValue = emptyClub,
  submitLabel,
  loading = false,
  autoFocus = false,
  onSubmit,
  onCancel,
}: ClubFormProps) {
  const [form, setForm] = useState<SaveClubInput>(initialValue)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const name = form.name.trim()
    const description = form.description?.trim() ?? ''
    const coverImageUrl = form.coverImageUrl?.trim() ?? ''
    const nextErrors: Record<string, string> = {}

    if (name.length < 3) nextErrors.name = 'Tên câu lạc bộ cần ít nhất 3 ký tự.'
    if (name.length > 150) nextErrors.name = 'Tên câu lạc bộ không vượt quá 150 ký tự.'
    if (description.length > 2000) nextErrors.description = 'Mô tả không vượt quá 2.000 ký tự.'
    if (coverImageUrl) {
      try {
        const url = new URL(coverImageUrl)
        if (!['http:', 'https:'].includes(url.protocol)) throw new Error()
      } catch {
        nextErrors.coverImageUrl = 'Nhập URL ảnh hợp lệ bắt đầu bằng http:// hoặc https://.'
      }
    }

    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return

    await onSubmit({
      name,
      description: description || undefined,
      coverImageUrl: coverImageUrl || undefined,
      isPrivate: form.isPrivate,
    })
  }

  return (
    <form onSubmit={submit}>
      <div className="grid gap-5 md:grid-cols-2">
        <InputField
          label="Tên câu lạc bộ"
          name="clubName"
          value={form.name}
          maxLength={150}
          autoFocus={autoFocus}
          error={errors.name}
          onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
          placeholder="Ví dụ: Những người mê văn học Việt"
          required
        />
        <InputField
          label="URL ảnh bìa"
          name="clubCoverImageUrl"
          type="url"
          value={form.coverImageUrl ?? ''}
          maxLength={1000}
          error={errors.coverImageUrl}
          onChange={(event) =>
            setForm((current) => ({ ...current, coverImageUrl: event.target.value }))
          }
          placeholder="https://..."
        />
        <TextareaField
          label="Mô tả"
          name="clubDescription"
          value={form.description ?? ''}
          maxLength={2000}
          error={errors.description}
          className="md:col-span-2"
          onChange={(event) =>
            setForm((current) => ({ ...current, description: event.target.value }))
          }
          placeholder="CLB đọc gì, gặp nhau ra sao và phù hợp với ai?"
        />
      </div>

      <fieldset className="mt-6">
        <legend className="field-label">Quyền riêng tư</legend>
        <div className="grid gap-3 sm:grid-cols-2">
          <label
            className={`flex cursor-pointer gap-3 rounded-xl border p-4 transition-colors ${
              !form.isPrivate
                ? 'border-accent bg-accent-soft'
                : 'border-border bg-surface hover:bg-surface-muted'
            }`}
          >
            <input
              type="radio"
              name="clubVisibility"
              checked={!form.isPrivate}
              className="mt-1 accent-accent"
              onChange={() => setForm((current) => ({ ...current, isPrivate: false }))}
            />
            <UsersThree size={22} className="shrink-0 text-accent-strong" />
            <span>
              <strong className="block text-sm text-heading">Công khai</strong>
              <span className="mt-1 block text-xs leading-5 text-muted">
                Ai cũng có thể tìm thấy và tự tham gia.
              </span>
            </span>
          </label>
          <label
            className={`flex cursor-pointer gap-3 rounded-xl border p-4 transition-colors ${
              form.isPrivate
                ? 'border-accent bg-accent-soft'
                : 'border-border bg-surface hover:bg-surface-muted'
            }`}
          >
            <input
              type="radio"
              name="clubVisibility"
              checked={form.isPrivate}
              className="mt-1 accent-accent"
              onChange={() => setForm((current) => ({ ...current, isPrivate: true }))}
            />
            <LockSimple size={22} className="shrink-0 text-accent-strong" />
            <span>
              <strong className="block text-sm text-heading">Riêng tư</strong>
              <span className="mt-1 block text-xs leading-5 text-muted">
                Thành viên chỉ có thể vào bằng lời mời.
              </span>
            </span>
          </label>
        </div>
      </fieldset>

      <div className="mt-7 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel}>
            Hủy
          </Button>
        ) : null}
        <Button type="submit" loading={loading}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}
