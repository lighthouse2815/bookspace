import { BookOpenText, Clock, NotePencil } from '@phosphor-icons/react'
import { useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { InputField, SelectField, TextareaField } from '../../components/ui/FormField'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useCreateSession, useLibrary, useSessions } from '../../hooks/useReading'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'

function localDateTimeValue() {
  const now = new Date()
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
  return now.toISOString().slice(0, 16)
}

export function JournalPage() {
  const sessions = useSessions()
  const library = useLibrary('READING')
  const create = useCreateSession()
  const { showToast } = useToast()
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({
    bookId: '',
    startedAt: localDateTimeValue(),
    durationMinutes: '30',
    pagesRead: '',
    note: '',
  })

  const totals = useMemo(
    () => ({
      pages: sessions.data?.items.reduce((sum, item) => sum + item.pagesRead, 0) ?? 0,
      minutes: sessions.data?.items.reduce((sum, item) => sum + item.durationMinutes, 0) ?? 0,
    }),
    [sessions.data],
  )

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const durationMinutes = Number(form.durationMinutes)
    const pagesRead = Number(form.pagesRead)
    if (!form.bookId || durationMinutes <= 0 || pagesRead <= 0) {
      showToast('Chọn sách và nhập thời lượng, số trang hợp lệ', 'error')
      return
    }
    try {
      await create.mutateAsync({
        bookId: form.bookId,
        startedAt: new Date(form.startedAt).toISOString(),
        durationMinutes,
        pagesRead,
        note: form.note.trim() || undefined,
      })
      setForm({
        bookId: '',
        startedAt: localDateTimeValue(),
        durationMinutes: '30',
        pagesRead: '',
        note: '',
      })
      setShowForm(false)
      showToast('Phiên đọc đã được lưu', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu phiên đọc'), 'error')
    }
  }

  return (
    <div className="container-page section-space">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Dấu vết mỗi ngày</p>
          <h1 className="page-title mt-4">Nhật ký đọc</h1>
          <p className="mt-3 text-muted">Lưu thời gian, số trang và điều bạn muốn nhớ sau mỗi phiên đọc.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link to="/notes" className="button button-secondary button-md">
            Ghi chú sách
          </Link>
          <Button icon={<NotePencil size={18} />} onClick={() => setShowForm((value) => !value)}>
            {showForm ? 'Đóng biểu mẫu' : 'Ghi phiên đọc'}
          </Button>
        </div>
      </div>

      <div className="mt-8 grid gap-px overflow-hidden rounded-2xl border border-border bg-border sm:grid-cols-2">
        <div className="bg-surface p-6">
          <BookOpenText size={24} weight="duotone" className="text-accent-strong" />
          <p className="mt-4 text-3xl font-bold text-heading">{totals.pages.toLocaleString('vi-VN')}</p>
          <p className="text-sm text-muted">trang đã ghi nhận</p>
        </div>
        <div className="bg-surface p-6">
          <Clock size={24} weight="duotone" className="text-accent-strong" />
          <p className="mt-4 text-3xl font-bold text-heading">{totals.minutes.toLocaleString('vi-VN')}</p>
          <p className="text-sm text-muted">phút đọc tập trung</p>
        </div>
      </div>

      {showForm ? (
        <form onSubmit={submit} className="mt-8 surface p-5 sm:p-7">
          <h2 className="text-xl font-bold text-heading">Phiên đọc mới</h2>
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <SelectField
              label="Cuốn sách"
              name="bookId"
              value={form.bookId}
              onChange={(event) => setForm({ ...form, bookId: event.target.value })}
              required
            >
              <option value="">Chọn sách đang đọc</option>
              {library.data?.items.map((entry) => (
                <option key={entry.bookId} value={entry.bookId}>
                  {entry.book.title}
                </option>
              ))}
            </SelectField>
            <InputField
              label="Bắt đầu lúc"
              name="startedAt"
              type="datetime-local"
              value={form.startedAt}
              onChange={(event) => setForm({ ...form, startedAt: event.target.value })}
              required
            />
            <InputField
              label="Số phút đọc"
              name="durationMinutes"
              type="number"
              min={1}
              max={1440}
              value={form.durationMinutes}
              onChange={(event) => setForm({ ...form, durationMinutes: event.target.value })}
              required
            />
            <InputField
              label="Số trang đã đọc"
              name="pagesRead"
              type="number"
              min={1}
              value={form.pagesRead}
              onChange={(event) => setForm({ ...form, pagesRead: event.target.value })}
              required
            />
            <TextareaField
              label="Ghi chú"
              name="note"
              value={form.note}
              maxLength={2000}
              className="md:col-span-2"
              hint="Một ý tưởng, câu hỏi hoặc cảm nhận bạn muốn nhớ."
              onChange={(event) => setForm({ ...form, note: event.target.value })}
            />
          </div>
          {!library.isLoading && !library.data?.items.length ? (
            <p className="mt-4 text-sm text-amber-700 dark:text-amber-400">
              Thư viện chưa có sách ở kệ Đang đọc. Hãy chuyển một cuốn sang Đang đọc trước.
            </p>
          ) : null}
          <div className="mt-6 flex justify-end">
            <Button type="submit" loading={create.isPending} disabled={!library.data?.items.length}>
              Lưu phiên đọc
            </Button>
          </div>
        </form>
      ) : null}

      <section className="mt-10">
        <h2 className="text-xl font-bold text-heading">Dòng thời gian</h2>
        <div className="mt-5">
          {sessions.isLoading ? (
            <LoadingRows count={5} />
          ) : sessions.isError ? (
            <ErrorState message="Không thể tải nhật ký." retry={() => void sessions.refetch()} />
          ) : sessions.data?.items.length ? (
            <div className="space-y-3">
              {sessions.data.items.map((session) => (
                <article key={session.id} className="surface grid gap-4 p-5 sm:grid-cols-[1fr_auto] sm:items-center">
                  <div>
                    <p className="font-semibold text-heading">{session.book?.title || 'Phiên đọc'}</p>
                    <p className="mt-1 text-xs text-muted">{formatDate(session.startedAt)}</p>
                    {session.note ? <p className="mt-3 text-sm leading-6 text-body">{session.note}</p> : null}
                  </div>
                  <div className="flex gap-4 text-sm sm:text-right">
                    <div>
                      <p className="font-semibold text-heading">{session.pagesRead}</p>
                      <p className="text-xs text-muted">trang</p>
                    </div>
                    <div>
                      <p className="font-semibold text-heading">{session.durationMinutes}</p>
                      <p className="text-xs text-muted">phút</p>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <EmptyState
              icon={NotePencil}
              title="Chưa có phiên đọc"
              description="Ghi phiên đầu tiên để bắt đầu nhìn thấy nhịp đọc của mình."
              action={<Button onClick={() => setShowForm(true)}>Ghi phiên đọc</Button>}
            />
          )}
        </div>
      </section>
    </div>
  )
}
