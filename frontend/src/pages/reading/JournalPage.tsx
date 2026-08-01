import { BookOpenText, Clock, NotePencil, X } from '@phosphor-icons/react'
import { useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { FocusReadingPanel } from '../../components/reading/FocusReadingPanel'
import { Button } from '../../components/ui/Button'
import { InputField, SelectField, TextareaField } from '../../components/ui/FormField'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import {
  useActiveReadingSession,
  useCreateSession,
  useLibrary,
  useSessions,
  useUpdateSession,
} from '../../hooks/useReading'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { ReadingSession } from '../../types/domain'

function localDateTimeValue(value?: string) {
  const now = value ? new Date(value) : new Date()
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
  return now.toISOString().slice(0, 16)
}

export function JournalPage() {
  const [searchParams] = useSearchParams()
  const sessions = useSessions()
  const library = useLibrary('READING')
  const activeSession = useActiveReadingSession()
  const create = useCreateSession()
  const updateSession = useUpdateSession()
  const { showToast } = useToast()
  const editLock = useRef(false)
  const [showForm, setShowForm] = useState(false)
  const [editingSession, setEditingSession] = useState<ReadingSession | null>(null)
  const [editErrors, setEditErrors] = useState<Record<string, string>>({})
  const [editForm, setEditForm] = useState({
    startedAt: '',
    durationMinutes: '',
    pagesRead: '',
    note: '',
  })
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
    const startedAt = new Date(form.startedAt)
    if (form.bookId && form.bookId === activeSession.data?.bookId) {
      showToast('Hãy hoàn tất hoặc hủy Focus Reading trước khi ghi thủ công cho cuốn này', 'error')
      return
    }
    if (
      !form.bookId ||
      Number.isNaN(startedAt.getTime()) ||
      !Number.isInteger(durationMinutes) ||
      durationMinutes < 1 ||
      durationMinutes > 1440 ||
      !Number.isInteger(pagesRead) ||
      pagesRead < 1
    ) {
      showToast('Chọn sách, thời điểm, thời lượng và số trang hợp lệ', 'error')
      return
    }
    try {
      await create.mutateAsync({
        bookId: form.bookId,
        startedAt: startedAt.toISOString(),
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

  const openEdit = (session: ReadingSession) => {
    setEditingSession(session)
    setEditErrors({})
    setEditForm({
      startedAt: localDateTimeValue(session.startedAt),
      durationMinutes: String(session.durationMinutes),
      pagesRead: String(session.pagesRead),
      note: session.note ?? '',
    })
  }

  const submitEdit = async (event: FormEvent) => {
    event.preventDefault()
    if (!editingSession || editLock.current) return
    const startedAt = new Date(editForm.startedAt)
    const durationMinutes = Number(editForm.durationMinutes)
    const pagesRead = Number(editForm.pagesRead)
    const nextErrors: Record<string, string> = {}

    if (Number.isNaN(startedAt.getTime())) nextErrors.startedAt = 'Hãy chọn thời điểm bắt đầu hợp lệ.'
    else if (startedAt.getTime() > Date.now() + 5 * 60_000) {
      nextErrors.startedAt = 'Thời điểm bắt đầu không được nằm trong tương lai.'
    }
    if (!Number.isInteger(durationMinutes) || durationMinutes < 1 || durationMinutes > 1440) {
      nextErrors.durationMinutes = 'Thời lượng phải là số nguyên từ 1 đến 1.440 phút.'
    }
    if (!Number.isInteger(pagesRead) || pagesRead < 1) {
      nextErrors.pagesRead = 'Số trang phải là số nguyên dương.'
    } else if (editingSession.book?.pageCount && pagesRead > editingSession.book.pageCount) {
      nextErrors.pagesRead = `Số trang không được vượt quá ${editingSession.book.pageCount}.`
    }
    if (editForm.note.length > 1000) nextErrors.note = 'Ghi chú không được vượt quá 1.000 ký tự.'
    setEditErrors(nextErrors)
    if (Object.keys(nextErrors).length) return

    editLock.current = true
    try {
      await updateSession.mutateAsync({
        id: editingSession.id,
        input: {
          startedAt: startedAt.toISOString(),
          durationMinutes,
          pagesRead,
          note: editForm.note.trim() || null,
        },
      })
      setEditingSession(null)
      showToast('Đã sửa phiên đọc', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể sửa phiên đọc'), 'error')
    } finally {
      editLock.current = false
    }
  }

  return (
    <div className="container-page section-space">
      <div className="flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="eyebrow">Dấu vết mỗi ngày</p>
          <h1 className="page-title mt-4">Nhật ký đọc</h1>
          <p className="mt-3 text-muted">Đọc tập trung với đồng hồ hoặc ghi lại một phiên đã hoàn thành.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link to="/notes" className="button button-secondary button-md">
            Ghi chú sách
          </Link>
          <Button icon={<NotePencil size={18} />} onClick={() => setShowForm((value) => !value)}>
            {showForm ? 'Đóng biểu mẫu' : 'Ghi thủ công'}
          </Button>
        </div>
      </div>

      <FocusReadingPanel preselectedBookId={searchParams.get('bookId') ?? undefined} />

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
        <form onSubmit={submit} className="mt-8 surface p-5 sm:p-7" noValidate>
          <h2 className="text-xl font-bold text-heading">Ghi phiên đã hoàn thành</h2>
          <p className="mt-2 text-sm leading-6 text-muted">
            Dùng biểu mẫu này khi bạn không bật đồng hồ Focus Reading trong lúc đọc.
          </p>
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <SelectField
              label="Cuốn sách"
              name="bookId"
              value={form.bookId}
              hint={
                activeSession.data
                  ? 'Cuốn đang Focus được khóa để tránh ghi trùng số trang.'
                  : undefined
              }
              onChange={(event) => setForm({ ...form, bookId: event.target.value })}
              required
            >
              <option value="">Chọn sách đang đọc</option>
              {library.data?.items.map((entry) => (
                <option
                  key={entry.bookId}
                  value={entry.bookId}
                  disabled={entry.bookId === activeSession.data?.bookId}
                >
                  {entry.book.title}
                  {entry.bookId === activeSession.data?.bookId ? ' · đang Focus' : ''}
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
              max={library.data?.items.find((entry) => entry.bookId === form.bookId)?.book.pageCount}
              onChange={(event) => setForm({ ...form, pagesRead: event.target.value })}
              required
            />
            <TextareaField
              label="Ghi chú riêng tư"
              name="note"
              value={form.note}
              maxLength={1000}
              className="md:col-span-2"
              hint="Một ý tưởng, câu hỏi hoặc cảm nhận chỉ bạn nhìn thấy."
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
                  <div className="flex flex-wrap items-center gap-4 text-sm sm:justify-end sm:text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      icon={<NotePencil size={16} />}
                      onClick={() => openEdit(session)}
                    >
                      Sửa
                    </Button>
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

      {editingSession ? (
        <div className="fixed inset-0 z-[80] grid place-items-center p-4" role="presentation">
          <button
            type="button"
            className="absolute inset-0 bg-slate-950/60 backdrop-blur-sm"
            aria-label="Đóng biểu mẫu sửa"
            disabled={updateSession.isPending}
            onClick={() => setEditingSession(null)}
          />
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="edit-reading-session-title"
            className="surface relative z-10 max-h-[90dvh] w-full max-w-xl overflow-y-auto shadow-2xl"
          >
            <header className="flex items-start justify-between gap-4 border-b border-border px-5 py-4 sm:px-6">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-accent-strong">Chỉnh lại nhật ký</p>
                <h2 id="edit-reading-session-title" className="mt-1 text-xl font-bold text-heading">
                  {editingSession.book?.title ?? 'Sửa phiên đọc'}
                </h2>
              </div>
              <button
                type="button"
                className="icon-button"
                aria-label="Đóng"
                disabled={updateSession.isPending}
                onClick={() => setEditingSession(null)}
              >
                <X size={20} />
              </button>
            </header>
            <form onSubmit={submitEdit} className="grid gap-5 p-5 sm:grid-cols-2 sm:p-6" noValidate>
              <InputField
                label="Bắt đầu lúc"
                name="editStartedAt"
                type="datetime-local"
                value={editForm.startedAt}
                error={editErrors.startedAt}
                onChange={(event) => setEditForm({ ...editForm, startedAt: event.target.value })}
                required
              />
              <InputField
                label="Số phút đọc"
                name="editDurationMinutes"
                type="number"
                min={1}
                max={1440}
                value={editForm.durationMinutes}
                error={editErrors.durationMinutes}
                onChange={(event) => setEditForm({ ...editForm, durationMinutes: event.target.value })}
                required
              />
              <InputField
                label="Số trang đã đọc"
                name="editPagesRead"
                type="number"
                min={1}
                max={editingSession.book?.pageCount}
                value={editForm.pagesRead}
                error={editErrors.pagesRead}
                onChange={(event) => setEditForm({ ...editForm, pagesRead: event.target.value })}
                required
              />
              <TextareaField
                label="Ghi chú riêng tư"
                name="editNote"
                value={editForm.note}
                maxLength={1000}
                error={editErrors.note}
                hint={`${editForm.note.length}/1000 ký tự · Chỉ bạn nhìn thấy.`}
                className="sm:col-span-2"
                onChange={(event) => setEditForm({ ...editForm, note: event.target.value })}
              />
              <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-5 sm:col-span-2">
                <Button
                  type="button"
                  variant="ghost"
                  disabled={updateSession.isPending}
                  onClick={() => setEditingSession(null)}
                >
                  Hủy thay đổi
                </Button>
                <Button type="submit" loading={updateSession.isPending}>
                  Lưu chỉnh sửa
                </Button>
              </div>
            </form>
          </section>
        </div>
      ) : null}
    </div>
  )
}
