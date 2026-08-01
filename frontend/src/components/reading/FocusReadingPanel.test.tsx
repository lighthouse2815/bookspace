import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ActiveReadingSession, Book, LibraryEntry, ReadingSession } from '../../types/domain'
import { formatFocusDuration } from '../../lib/focus-reading'
import { FocusReadingPanel } from './FocusReadingPanel'

const book: Book = {
  id: 'book-1',
  title: 'Rừng Na Uy',
  pageCount: 320,
  author: { id: 'author-1', name: 'Haruki Murakami' },
}

const libraryEntry: LibraryEntry = {
  id: 'library-1',
  userId: 'reader-1',
  bookId: book.id,
  book,
  shelf: 'READING',
  currentPage: 15,
  progressPercent: 5,
  startedAt: '2026-08-01T00:00:00Z',
  updatedAt: '2026-08-01T00:00:00Z',
}

const active: ActiveReadingSession = {
  id: 'focus-1',
  bookId: book.id,
  book,
  status: 'RUNNING',
  startPage: 10,
  startedAt: '2026-08-01T01:00:00Z',
  elapsedSeconds: 90,
  updatedAt: '2026-08-01T01:01:30Z',
}

function queryResult<T>(data: T, overrides: Record<string, unknown> = {}) {
  return {
    data,
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
    ...overrides,
  }
}

function mutationResult(mutateAsync: ReturnType<typeof vi.fn>) {
  return {
    mutateAsync,
    isPending: false,
  }
}

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  activeQuery: vi.fn(),
  libraryQuery: vi.fn(),
  start: vi.fn(),
  pause: vi.fn(),
  resume: vi.fn(),
  finish: vi.fn(),
  cancel: vi.fn(),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useReading', () => ({
  useActiveReadingSession: () => mocks.activeQuery(),
  useLibrary: () => mocks.libraryQuery(),
  useStartActiveReadingSession: () => mutationResult(mocks.start),
  usePauseActiveReadingSession: () => mutationResult(mocks.pause),
  useResumeActiveReadingSession: () => mutationResult(mocks.resume),
  useFinishActiveReadingSession: () => mutationResult(mocks.finish),
  useCancelActiveReadingSession: () => mutationResult(mocks.cancel),
}))

function renderPanel(preselectedBookId?: string) {
  return render(
    <MemoryRouter>
      <FocusReadingPanel preselectedBookId={preselectedBookId} />
    </MemoryRouter>,
  )
}

describe('FocusReadingPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.activeQuery.mockReturnValue(queryResult(null))
    mocks.libraryQuery.mockReturnValue(
      queryResult({
        items: [libraryEntry],
        page: 1,
        pageSize: 100,
        totalItems: 1,
        totalPages: 1,
      }),
    )
    mocks.start.mockResolvedValue(active)
    mocks.pause.mockResolvedValue({ ...active, status: 'PAUSED' })
    mocks.resume.mockResolvedValue(active)
    mocks.finish.mockResolvedValue({ id: 'session-1' } as ReadingSession)
    mocks.cancel.mockResolvedValue(null)
  })

  it('formats the server-backed duration without losing hours', () => {
    expect(formatFocusDuration(3661)).toBe('01:01:01')
    expect(formatFocusDuration(-5)).toBe('00:00:00')
  })

  it('keeps finish disabled before the first full minute', () => {
    mocks.activeQuery.mockReturnValue(queryResult({ ...active, elapsedSeconds: 59 }))
    const view = renderPanel()

    expect(screen.getByRole('button', { name: 'Kết thúc' })).toBeDisabled()
    expect(screen.getByText('Đọc tối thiểu 1 phút trước khi lưu phiên.')).toBeInTheDocument()

    mocks.activeQuery.mockReturnValue(queryResult({ ...active, elapsedSeconds: 60 }))
    view.rerender(
      <MemoryRouter>
        <FocusReadingPanel />
      </MemoryRouter>,
    )
    expect(screen.getByRole('button', { name: 'Kết thúc' })).toBeEnabled()
  })

  it('recovers an active session and pauses it from the server state', async () => {
    mocks.activeQuery.mockReturnValue(queryResult(active))
    const user = userEvent.setup()
    renderPanel()

    expect(screen.getByText('Rừng Na Uy')).toBeInTheDocument()
    expect(screen.getByText('00:01:30')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Tạm dừng' }))

    await waitFor(() => expect(mocks.pause).toHaveBeenCalledOnce())
    expect(mocks.toast).toHaveBeenCalledWith('Đã tạm dừng phiên đọc', 'success')
  })

  it('keeps cancel recovery available when the catalog book was retired', async () => {
    mocks.activeQuery.mockReturnValue(queryResult({ ...active, book: null }))
    const user = userEvent.setup()
    renderPanel()

    expect(screen.getByText('Phiên đọc hiện tại')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Hủy phiên này' }))
    expect(screen.getByRole('button', { name: 'Xác nhận hủy' })).toBeInTheDocument()
  })

  it('preselects a library book and protects start from double submission', async () => {
    let resolveStart: ((value: ActiveReadingSession) => void) | undefined
    mocks.start.mockReturnValue(
      new Promise((resolve) => {
        resolveStart = resolve
      }),
    )
    const user = userEvent.setup()
    renderPanel(book.id)

    expect(screen.getByRole('combobox', { name: 'Cuốn sách muốn đọc' })).toHaveValue(book.id)
    await user.dblClick(screen.getByRole('button', { name: 'Bắt đầu đọc' }))
    expect(mocks.start).toHaveBeenCalledOnce()
    expect(mocks.start).toHaveBeenCalledWith({ bookId: book.id })

    await act(async () => resolveStart?.(active))
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith('Bắt đầu đọc “Rừng Na Uy”', 'success'))
  })

  it('validates current progress and submits an ending page with a private note', async () => {
    mocks.activeQuery.mockReturnValue(queryResult({ ...active, elapsedSeconds: 120 }))
    const user = userEvent.setup()
    renderPanel()

    await user.click(screen.getByRole('button', { name: 'Kết thúc' }))
    const endingPage = screen.getByRole('spinbutton', { name: 'Trang kết thúc' })
    await user.clear(endingPage)
    await user.type(endingPage, '14')
    await user.click(screen.getByRole('button', { name: 'Lưu phiên đọc' }))
    expect(screen.getByRole('alert')).toHaveTextContent('Trang kết thúc phải từ 15 trở lên.')
    expect(mocks.finish).not.toHaveBeenCalled()

    await user.clear(endingPage)
    await user.type(endingPage, '42')
    await user.type(
      screen.getByRole('textbox', { name: 'Ghi chú riêng tư (không bắt buộc)' }),
      'Một đoạn văn đáng nhớ.',
    )
    await user.click(screen.getByRole('button', { name: 'Lưu phiên đọc' }))

    await waitFor(() =>
      expect(mocks.finish).toHaveBeenCalledWith({
        endingPage: 42,
        note: 'Một đoạn văn đáng nhớ.',
      }),
    )
  })

  it('requires confirmation before canceling an active session', async () => {
    mocks.activeQuery.mockReturnValue(queryResult(active))
    const user = userEvent.setup()
    renderPanel()

    await user.click(screen.getByRole('button', { name: 'Hủy phiên này' }))
    expect(mocks.cancel).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Xác nhận hủy' }))

    await waitFor(() => expect(mocks.cancel).toHaveBeenCalledOnce())
  })
})
