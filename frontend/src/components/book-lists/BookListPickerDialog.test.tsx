import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BookListPickerDialog } from './BookListPickerDialog'

const mocks = vi.hoisted(() => ({
  listQuery: vi.fn(),
  listMutation: vi.fn(),
  mutateAsync: vi.fn(),
  refetch: vi.fn(),
  showToast: vi.fn(),
}))

vi.mock('../../hooks/useBookLists', () => ({
  useMyBookLists: (...args: unknown[]) => mocks.listQuery(...args),
  useToggleBookInList: () => mocks.listMutation(),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.showToast }),
}))

const owner = {
  id: 'reader-1',
  email: 'reader@example.com',
  displayName: 'Minh Anh',
  role: 'USER',
}

function page(items: unknown[] = [], pageNumber = 1, totalPages = 1) {
  return {
    items,
    page: pageNumber,
    pageSize: 12,
    totalItems: items.length,
    totalPages,
  }
}

function queryResult(overrides: Record<string, unknown> = {}) {
  return {
    data: page(),
    isLoading: false,
    isFetching: false,
    isError: false,
    refetch: mocks.refetch,
    ...overrides,
  }
}

function renderDialog(onClose = vi.fn()) {
  return {
    onClose,
    ...render(
      <MemoryRouter>
        <BookListPickerDialog bookId="book-42" open onClose={onClose} />
      </MemoryRouter>,
    ),
  }
}

describe('BookListPickerDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.mutateAsync.mockResolvedValue(undefined)
    mocks.listMutation.mockReturnValue({
      mutateAsync: mocks.mutateAsync,
      isPending: false,
      variables: undefined,
    })
    mocks.listQuery.mockReturnValue(queryResult())
  })

  it('loads membership for the selected book, toggles both states, and paginates', async () => {
    const user = userEvent.setup()
    mocks.listQuery.mockReturnValue(
      queryResult({
        data: page(
          [
            {
              id: 'list-1',
              name: 'Đang đọc',
              visibility: 'PRIVATE',
              owner,
              bookCount: 2,
              previewBooks: [],
              isOwner: true,
              containsBook: true,
              createdAt: '2026-08-01T00:00:00Z',
            },
            {
              id: 'list-2',
              name: 'Muốn đọc',
              visibility: 'PUBLIC',
              owner,
              bookCount: 4,
              previewBooks: [],
              isOwner: true,
              containsBook: false,
              createdAt: '2026-08-02T00:00:00Z',
            },
          ],
          1,
          2,
        ),
      }),
    )

    renderDialog()

    expect(mocks.listQuery).toHaveBeenCalledWith(1, 'book-42', true)
    await user.click(screen.getByRole('button', { name: /Đang đọc/ }))
    await user.click(screen.getByRole('button', { name: /Muốn đọc/ }))

    await waitFor(() => {
      expect(mocks.mutateAsync).toHaveBeenNthCalledWith(1, {
        listId: 'list-1',
        bookId: 'book-42',
        containsBook: true,
      })
      expect(mocks.mutateAsync).toHaveBeenNthCalledWith(2, {
        listId: 'list-2',
        bookId: 'book-42',
        containsBook: false,
      })
    })

    expect(screen.getByText('Trang 1 / 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Trang sau' }))
    expect(mocks.listQuery).toHaveBeenCalledWith(2, 'book-42', true)
  })

  it('shows the loading and retry states', async () => {
    const user = userEvent.setup()
    mocks.listQuery.mockReturnValue(queryResult({ data: undefined, isLoading: true }))
    const view = renderDialog()

    expect(screen.getByLabelText('Đang tải dữ liệu')).toHaveAttribute('aria-busy', 'true')

    mocks.listQuery.mockReturnValue(
      queryResult({ data: undefined, isError: true }),
    )
    view.rerender(
      <MemoryRouter>
        <BookListPickerDialog bookId="book-42" open onClose={view.onClose} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Không thể tải bộ sưu tập của bạn.',
    )
    await user.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(mocks.refetch).toHaveBeenCalledOnce()
  })

  it('links the empty state to collection creation and closes the dialog', async () => {
    const user = userEvent.setup()
    const { onClose } = renderDialog()

    const createLink = screen.getByRole('link', { name: 'Tạo bộ sưu tập' })
    expect(createLink).toHaveAttribute('href', '/lists')
    await user.click(createLink)
    expect(onClose).toHaveBeenCalledOnce()
  })
})
