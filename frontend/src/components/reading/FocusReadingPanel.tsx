import { Clock, LockKey, Pause, Play, Stop, Trash, X } from '@phosphor-icons/react'
import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import {
  useActiveReadingSession,
  useCancelActiveReadingSession,
  useFinishActiveReadingSession,
  useLibrary,
  usePauseActiveReadingSession,
  useResumeActiveReadingSession,
  useStartActiveReadingSession,
} from '../../hooks/useReading'
import { errorMessage } from '../../lib/api'
import { formatFocusDuration } from '../../lib/focus-reading'
import type { ActiveReadingSession, LibraryEntry } from '../../types/domain'
import { BookCover } from '../books/BookCover'
import { Button } from '../ui/Button'
import { InputField, SelectField, TextareaField } from '../ui/FormField'
import { EmptyState, ErrorState } from '../ui/States'
import { useToast } from '../../contexts/ToastContext'

const NOTE_MAX_LENGTH = 1000

function useElapsedTicker(status: ActiveReadingSession['status'], serverElapsedSeconds: number) {
  const receivedAt = useRef(Date.now())
  const [now, setNow] = useState(receivedAt.current)

  useEffect(() => {
    receivedAt.current = Date.now()
    setNow(receivedAt.current)
  }, [serverElapsedSeconds, status])

  useEffect(() => {
    if (status !== 'RUNNING') return
    const interval = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(interval)
  }, [status])

  const localDelta = status === 'RUNNING' ? Math.floor((now - receivedAt.current) / 1000) : 0
  return Math.max(0, serverElapsedSeconds + localDelta)
}

function FocusDialog({
  title,
  description,
  onClose,
  children,
}: {
  title: string
  description: string
  onClose: () => void
  children: ReactNode
}) {
  const titleId = 'focus-reading-dialog-title'

  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center p-4" role="presentation">
      <button
        type="button"
        className="absolute inset-0 bg-slate-950/60 backdrop-blur-sm"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="surface relative z-10 w-full max-w-lg overflow-hidden shadow-2xl"
      >
        <header className="flex items-start justify-between gap-4 border-b border-border px-5 py-4 sm:px-6">
          <div>
            <h2 id={titleId} className="text-xl font-bold text-heading">
              {title}
            </h2>
            <p className="mt-1 text-sm leading-6 text-muted">{description}</p>
          </div>
          <button type="button" className="icon-button" aria-label="Đóng" onClick={onClose}>
            <X size={20} />
          </button>
        </header>
        {children}
      </section>
    </div>
  )
}

function ActiveFocusSession({
  session,
  currentLibraryPage,
  onRefresh,
}: {
  session: ActiveReadingSession
  currentLibraryPage?: number
  onRefresh: () => void
}) {
  const { showToast } = useToast()
  const pause = usePauseActiveReadingSession()
  const resume = useResumeActiveReadingSession()
  const finish = useFinishActiveReadingSession()
  const cancel = useCancelActiveReadingSession()
  const actionLock = useRef(false)
  const elapsedSeconds = useElapsedTicker(session.status, session.elapsedSeconds)
  const [showFinish, setShowFinish] = useState(false)
  const [showCancel, setShowCancel] = useState(false)
  const [endingPage, setEndingPage] = useState(String(session.startPage + 1))
  const [note, setNote] = useState('')
  const [endingPageError, setEndingPageError] = useState('')
  const isBusy = pause.isPending || resume.isPending || finish.isPending || cancel.isPending
  const minimumEndingPage = Math.max(session.startPage + 1, currentLibraryPage ?? session.startPage + 1)
  const canFinish = elapsedSeconds >= 60

  const runLocked = async (action: () => Promise<unknown>) => {
    if (actionLock.current) return false
    actionLock.current = true
    try {
      await action()
      return true
    } finally {
      actionLock.current = false
    }
  }

  const toggleTimer = async () => {
    try {
      const succeeded = await runLocked(() =>
        session.status === 'RUNNING' ? pause.mutateAsync() : resume.mutateAsync(),
      )
      if (succeeded) {
        showToast(session.status === 'RUNNING' ? 'Đã tạm dừng phiên đọc' : 'Đã tiếp tục phiên đọc', 'success')
      }
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật đồng hồ đọc'), 'error')
      onRefresh()
    }
  }

  const openFinishDialog = () => {
    const nextPage = Math.min(minimumEndingPage, session.book?.pageCount ?? minimumEndingPage)
    setEndingPage(String(nextPage))
    setNote('')
    setEndingPageError('')
    setShowFinish(true)
  }

  const submitFinish = async (event: FormEvent) => {
    event.preventDefault()
    const parsedPage = Number(endingPage)
    if (!Number.isInteger(parsedPage) || parsedPage < minimumEndingPage) {
      setEndingPageError(`Trang kết thúc phải từ ${minimumEndingPage} trở lên.`)
      return
    }
    if (session.book?.pageCount && parsedPage > session.book.pageCount) {
      setEndingPageError(`Trang kết thúc không được vượt quá ${session.book.pageCount}.`)
      return
    }
    setEndingPageError('')
    try {
      const succeeded = await runLocked(() =>
        finish.mutateAsync({
          endingPage: parsedPage,
          note: note.trim() || undefined,
        }),
      )
      if (succeeded) {
        setShowFinish(false)
        showToast('Phiên đọc đã được lưu vào nhật ký', 'success')
      }
    } catch (error) {
      showToast(errorMessage(error, 'Không thể kết thúc phiên đọc'), 'error')
      onRefresh()
    }
  }

  const confirmCancel = async () => {
    try {
      const succeeded = await runLocked(() => cancel.mutateAsync())
      if (succeeded) {
        setShowCancel(false)
        showToast('Đã hủy phiên đọc', 'success')
      }
    } catch (error) {
      showToast(errorMessage(error, 'Không thể hủy phiên đọc'), 'error')
      onRefresh()
    }
  }

  return (
    <>
      <div className="grid gap-8 p-5 sm:p-7 lg:grid-cols-[minmax(0,0.8fr)_minmax(22rem,1.2fr)] lg:items-center lg:p-9">
        <div className="flex min-w-0 items-center gap-5 lg:items-start">
          <BookCover
            src={session.book?.coverImageUrl}
            title={session.book?.title ?? 'Cuốn sách đang đọc'}
            className="h-36 w-24 shrink-0 rounded-xl shadow-cover sm:h-44 sm:w-[7.35rem]"
          />
          <div className="min-w-0">
            <span className="inline-flex items-center gap-2 rounded-full bg-accent-soft px-3 py-1 text-xs font-bold text-accent-strong">
              <span
                className={`h-2 w-2 rounded-full ${session.status === 'RUNNING' ? 'animate-pulse bg-accent' : 'bg-amber-500'}`}
                aria-hidden
              />
              {session.status === 'RUNNING' ? 'Đang tập trung' : 'Đang tạm dừng'}
            </span>
            <h3 className="mt-4 line-clamp-2 text-xl font-bold text-heading sm:text-2xl">
              {session.book?.title ?? 'Phiên đọc hiện tại'}
            </h3>
            <p className="mt-2 text-sm text-muted">{session.book?.author?.name ?? 'Tác giả đang cập nhật'}</p>
            <p className="mt-5 text-sm text-body">
              Bắt đầu từ trang <strong className="text-heading">{session.startPage}</strong>
              {session.book?.pageCount ? ` / ${session.book.pageCount}` : ''}
            </p>
          </div>
        </div>

        <div className="rounded-2xl border border-border bg-page/65 p-5 text-center sm:p-7">
          <p className="flex items-center justify-center gap-2 text-xs font-bold uppercase tracking-[0.16em] text-muted">
            <Clock size={16} aria-hidden />
            Thời gian đọc
          </p>
          <output
            className="mt-3 block font-mono text-5xl font-bold tabular-nums tracking-[-0.05em] text-heading sm:text-6xl"
            aria-label={`Thời gian đọc ${formatFocusDuration(elapsedSeconds)}`}
          >
            {formatFocusDuration(elapsedSeconds)}
          </output>
          <p className="mt-3 text-xs leading-5 text-muted">
            Đồng hồ được lưu trên máy chủ. Bạn có thể rời trang và quay lại bất cứ lúc nào.
          </p>
          <div className="mt-6 flex flex-wrap justify-center gap-2">
            <Button
              size="lg"
              variant={session.status === 'RUNNING' ? 'secondary' : 'primary'}
              icon={session.status === 'RUNNING' ? <Pause size={19} /> : <Play size={19} weight="fill" />}
              loading={pause.isPending || resume.isPending}
              disabled={isBusy}
              onClick={() => void toggleTimer()}
            >
              {session.status === 'RUNNING' ? 'Tạm dừng' : 'Tiếp tục'}
            </Button>
            <Button
              size="lg"
              icon={<Stop size={19} weight="fill" />}
              disabled={isBusy || !canFinish}
              onClick={openFinishDialog}
            >
              Kết thúc
            </Button>
          </div>
          {!canFinish ? (
            <p className="mt-3 text-xs font-medium text-amber-700 dark:text-amber-400">
              Đọc tối thiểu 1 phút trước khi lưu phiên.
            </p>
          ) : null}
          <button
            type="button"
            className="mt-4 inline-flex items-center gap-1.5 text-xs font-semibold text-muted hover:text-red-700 disabled:opacity-50 dark:hover:text-red-400"
            disabled={isBusy}
            onClick={() => setShowCancel(true)}
          >
            <Trash size={15} />
            Hủy phiên này
          </button>
        </div>
      </div>

      {showFinish ? (
        <FocusDialog
          title="Kết thúc phiên đọc"
          description="Xác nhận trang bạn vừa dừng để BookSpace tự cập nhật tiến độ."
          onClose={() => !finish.isPending && setShowFinish(false)}
        >
          <form onSubmit={submitFinish} className="space-y-5 p-5 sm:p-6" noValidate>
            <InputField
              label="Trang kết thúc"
              name="endingPage"
              type="number"
              min={minimumEndingPage}
              max={session.book?.pageCount}
              value={endingPage}
              error={endingPageError}
              hint={`Phiên bắt đầu ở trang ${session.startPage}; tiến độ hiện tại không thể bị lùi.`}
              onChange={(event) => {
                setEndingPage(event.target.value)
                setEndingPageError('')
              }}
              autoFocus
              required
            />
            <TextareaField
              label="Ghi chú riêng tư (không bắt buộc)"
              name="note"
              value={note}
              maxLength={NOTE_MAX_LENGTH}
              hint={`${note.length}/${NOTE_MAX_LENGTH} ký tự · Chỉ bạn nhìn thấy ghi chú này.`}
              placeholder="Một ý tưởng, câu hỏi hoặc cảm nhận muốn giữ lại…"
              onChange={(event) => setNote(event.target.value)}
            />
            <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-5">
              <Button type="button" variant="ghost" disabled={finish.isPending} onClick={() => setShowFinish(false)}>
                Đọc tiếp
              </Button>
              <Button type="submit" loading={finish.isPending} disabled={isBusy && !finish.isPending}>
                Lưu phiên đọc
              </Button>
            </div>
          </form>
        </FocusDialog>
      ) : null}

      {showCancel ? (
        <FocusDialog
          title="Hủy phiên đọc này?"
          description="Thời gian và tiến độ của phiên hiện tại sẽ không được lưu vào nhật ký."
          onClose={() => !cancel.isPending && setShowCancel(false)}
        >
          <div className="flex flex-wrap justify-end gap-2 p-5 sm:p-6">
            <Button variant="ghost" disabled={cancel.isPending} onClick={() => setShowCancel(false)}>
              Giữ phiên đọc
            </Button>
            <Button variant="danger" loading={cancel.isPending} onClick={() => void confirmCancel()}>
              Xác nhận hủy
            </Button>
          </div>
        </FocusDialog>
      ) : null}
    </>
  )
}

export function FocusReadingPanel({ preselectedBookId }: { preselectedBookId?: string }) {
  const { showToast } = useToast()
  const activeSession = useActiveReadingSession()
  const library = useLibrary('READING')
  const start = useStartActiveReadingSession()
  const startLock = useRef(false)
  const [selectedBookId, setSelectedBookId] = useState(preselectedBookId ?? '')
  const [selectionError, setSelectionError] = useState('')

  useEffect(() => {
    if (preselectedBookId) setSelectedBookId(preselectedBookId)
  }, [preselectedBookId])

  const selectedEntry = useMemo(
    () => library.data?.items.find((entry) => entry.bookId === selectedBookId),
    [library.data, selectedBookId],
  )
  const readingEntries = useMemo(
    () => library.data?.items ?? [],
    [library.data],
  )
  const activeLibraryEntry = useMemo(
    () => library.data?.items.find((entry) => entry.bookId === activeSession.data?.bookId),
    [activeSession.data?.bookId, library.data],
  )

  const startFocus = async (event: FormEvent) => {
    event.preventDefault()
    if (!selectedEntry) {
      setSelectionError('Hãy chọn một cuốn trong kệ Đang đọc.')
      return
    }
    if (startLock.current) return
    startLock.current = true
    try {
      await start.mutateAsync({ bookId: selectedEntry.bookId })
      showToast(`Bắt đầu đọc “${selectedEntry.book.title}”`, 'success')
      setSelectionError('')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể bắt đầu phiên đọc'), 'error')
      void activeSession.refetch()
    } finally {
      startLock.current = false
    }
  }

  return (
    <section
      aria-labelledby="focus-reading-title"
      className="mt-8 overflow-hidden rounded-[1.75rem] border border-accent/25 bg-surface shadow-[0_18px_60px_rgb(var(--shadow)/0.08)]"
    >
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-border bg-accent-soft/65 px-5 py-4 sm:px-7">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-accent-strong">Focus Reading</p>
          <h2 id="focus-reading-title" className="mt-1 text-xl font-bold text-heading">
            Không gian đọc tập trung
          </h2>
        </div>
        <span className="inline-flex items-center gap-2 text-xs font-semibold text-muted">
          <LockKey size={16} className="text-accent-strong" />
          Đồng hồ và ghi chú riêng tư · hoạt động theo cài đặt hồ sơ
        </span>
      </header>

      {activeSession.isLoading ? (
        <div className="grid animate-pulse gap-8 p-7 lg:grid-cols-2" aria-label="Đang khôi phục phiên đọc" aria-busy="true">
          <div className="h-44 rounded-2xl bg-surface-muted" />
          <div className="h-44 rounded-2xl bg-surface-muted" />
        </div>
      ) : activeSession.isError ? (
        <div className="p-5 sm:p-7">
          <ErrorState
            message="Không thể kiểm tra phiên đọc đang hoạt động. Hãy tải lại trước khi bắt đầu phiên mới."
            retry={() => void activeSession.refetch()}
          />
        </div>
      ) : activeSession.data ? (
        <ActiveFocusSession
          session={activeSession.data}
          currentLibraryPage={activeLibraryEntry?.currentPage}
          onRefresh={() => void Promise.all([activeSession.refetch(), library.refetch()])}
        />
      ) : library.isLoading ? (
        <div className="grid animate-pulse gap-6 p-7 md:grid-cols-[1fr_15rem]" aria-label="Đang tải sách đang đọc" aria-busy="true">
          <div className="h-28 rounded-2xl bg-surface-muted" />
          <div className="h-28 rounded-2xl bg-surface-muted" />
        </div>
      ) : library.isError ? (
        <div className="p-5 sm:p-7">
          <ErrorState message="Không thể tải kệ Đang đọc." retry={() => void library.refetch()} />
        </div>
      ) : readingEntries.length ? (
        <form onSubmit={startFocus} className="grid gap-6 p-5 sm:p-7 lg:grid-cols-[minmax(0,1fr)_18rem] lg:items-end">
          <div>
            <h3 className="text-2xl font-bold text-heading">Sẵn sàng cho một khoảng lặng?</h3>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
              Chọn cuốn đang đọc. BookSpace sẽ ghi thời gian, số trang và tự cập nhật mục tiêu khi bạn kết thúc.
            </p>
            <SelectField
              label="Cuốn sách muốn đọc"
              name="focusBookId"
              value={selectedBookId}
              error={selectionError}
              onChange={(event) => {
                setSelectedBookId(event.target.value)
                setSelectionError('')
              }}
              className="mt-1"
              required
            >
              <option value="">Chọn sách ở kệ Đang đọc</option>
              {readingEntries.map((entry: LibraryEntry) => (
                <option key={entry.bookId} value={entry.bookId}>
                  {entry.book.title} · trang {entry.currentPage}
                </option>
              ))}
            </SelectField>
          </div>
          <div className="rounded-2xl border border-border bg-page/65 p-4">
            {selectedEntry ? (
              <div className="mb-4 flex items-center gap-3">
                <BookCover
                  src={selectedEntry.book.coverImageUrl}
                  title={selectedEntry.book.title}
                  className="h-16 w-11 shrink-0 rounded-lg"
                />
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-heading">{selectedEntry.book.title}</p>
                  <p className="mt-1 text-xs text-muted">Tiếp tục từ trang {selectedEntry.currentPage}</p>
                </div>
              </div>
            ) : (
              <p className="mb-4 text-sm leading-6 text-muted">Đồng hồ sẽ bắt đầu ngay sau khi bạn xác nhận.</p>
            )}
            <Button
              type="submit"
              size="lg"
              className="w-full"
              icon={<Play size={19} weight="fill" />}
              loading={start.isPending}
            >
              Bắt đầu đọc
            </Button>
          </div>
        </form>
      ) : (
        <div className="p-5 sm:p-7">
          <EmptyState
            icon={Play}
            title="Chưa có sách đang đọc"
            description="Chuyển một cuốn sang kệ Đang đọc để bắt đầu phiên tập trung đầu tiên."
            action={
              <Link to="/library" className="button button-primary button-md">
                Mở thư viện
              </Link>
            }
          />
        </div>
      )}
    </section>
  )
}
