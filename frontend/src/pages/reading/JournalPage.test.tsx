import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ActiveReadingSession, Book, LibraryEntry, ReadingSession } from '../../types/domain'
import { JournalPage } from './JournalPage'

const book: Book = {
  id: 'book-1',
  title: 'Những người khốn khổ',
  pageCount: 680,
  author: { id: 'author-1', name: 'Victor Hugo' },
}

const session: ReadingSession = {
  id: 'session-1',
  bookId: book.id,
  book,
  startedAt: '2026-08-01T01:00:00Z',
  endedAt: '2026-08-01T01:30:00Z',
  durationMinutes: 30,
  pagesRead: 10,
  note: 'Bản ghi cũ',
  createdAt: '2026-08-01T01:30:00Z',
}

const libraryEntry: LibraryEntry = {
  id: 'library-1',
  userId: 'reader-1',
  bookId: book.id,
  book,
  shelf: 'READING',
  currentPage: 100,
  progressPercent: 15,
  updatedAt: '2026-08-01T01:30:00Z',
}

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  refetchSessions: vi.fn(),
  sessionsHook: vi.fn(),
  libraryHook: vi.fn(),
  activeHook: vi.fn(),
}))

vi.mock('../../components/reading/FocusReadingPanel', () => ({
  FocusReadingPanel: () => <div data-testid="focus-reading-panel" />,
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useReading', () => ({
  useSessions: () => mocks.sessionsHook(),
  useLibrary: () => mocks.libraryHook(),
  useActiveReadingSession: () => mocks.activeHook(),
  useCreateSession: () => ({ mutateAsync: mocks.create, isPending: false }),
  useUpdateSession: () => ({ mutateAsync: mocks.update, isPending: false }),
}))

describe('JournalPage session correction', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.sessionsHook.mockReturnValue({
      data: {
        items: [session],
        page: 1,
        pageSize: 100,
        totalItems: 1,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
      refetch: mocks.refetchSessions,
    })
    mocks.libraryHook.mockReturnValue({
      data: {
        items: [libraryEntry],
        page: 1,
        pageSize: 100,
        totalItems: 1,
        totalPages: 1,
      },
      isLoading: false,
    })
    mocks.activeHook.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    mocks.create.mockResolvedValue(session)
    mocks.update.mockResolvedValue(session)
  })

  it('validates a correction before calling the owner-only update endpoint', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/journal']}>
        <JournalPage />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Sửa' }))
    const pages = screen.getByRole('spinbutton', { name: 'Số trang đã đọc' })
    await user.clear(pages)
    await user.type(pages, '0')
    await user.click(screen.getByRole('button', { name: 'Lưu chỉnh sửa' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Số trang phải là số nguyên dương.')
    expect(mocks.update).not.toHaveBeenCalled()
  })

  it('submits a corrected session only once and keeps the note private', async () => {
    let resolveUpdate: ((value: ReadingSession) => void) | undefined
    mocks.update.mockReturnValue(
      new Promise((resolve) => {
        resolveUpdate = resolve
      }),
    )
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/journal']}>
        <JournalPage />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Sửa' }))
    const duration = screen.getByRole('spinbutton', { name: 'Số phút đọc' })
    const pages = screen.getByRole('spinbutton', { name: 'Số trang đã đọc' })
    const note = screen.getByRole('textbox', { name: 'Ghi chú riêng tư' })
    await user.clear(duration)
    await user.type(duration, '45')
    await user.clear(pages)
    await user.type(pages, '12')
    await user.clear(note)
    await user.type(note, 'Nội dung đã sửa')
    await user.dblClick(screen.getByRole('button', { name: 'Lưu chỉnh sửa' }))

    expect(mocks.update).toHaveBeenCalledOnce()
    expect(mocks.update).toHaveBeenCalledWith({
      id: session.id,
      input: {
        startedAt: expect.any(String),
        durationMinutes: 45,
        pagesRead: 12,
        note: 'Nội dung đã sửa',
      },
    })
    await act(async () => resolveUpdate?.({ ...session, durationMinutes: 45, pagesRead: 12 }))
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith('Đã sửa phiên đọc', 'success'))
  })

  it('locks the active Focus book in the manual session form', async () => {
    mocks.activeHook.mockReturnValue({
      data: {
        id: 'focus-1',
        bookId: book.id,
        book,
        status: 'RUNNING',
        startPage: 100,
        startedAt: '2026-08-01T02:00:00Z',
        elapsedSeconds: 90,
        updatedAt: '2026-08-01T02:01:30Z',
      } satisfies ActiveReadingSession,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/journal']}>
        <JournalPage />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Ghi thủ công' }))

    expect(screen.getByRole('option', { name: 'Những người khốn khổ · đang Focus' })).toBeDisabled()
    expect(screen.getByText('Cuốn đang Focus được khóa để tránh ghi trùng số trang.')).toBeInTheDocument()
    expect(mocks.create).not.toHaveBeenCalled()
  })
})
