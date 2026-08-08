import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../../types/api'
import type { Book, BookRecommendation } from '../../types/domain'
import { ExplorePage } from './ExplorePage'

const popularBook: Book = {
  id: 'popular-book',
  title: 'Cuốn sách phổ biến',
  author: { id: 'author-popular', name: 'Tác giả Phổ Biến' },
  averageRating: 4.5,
  reviewCount: 18,
}

const recommendedBook: Book = {
  id: 'recommended-book',
  title: 'Khu vườn bí mật',
  author: { id: 'author-recommended', name: 'Frances Hodgson Burnett' },
  averageRating: 4.8,
  reviewCount: 32,
}

const recommendation: BookRecommendation = {
  book: recommendedBook,
  reasonCode: 'FOLLOWED_READER_LIKED',
  reasonText: 'Được một độc giả bạn theo dõi đánh giá 5 sao.',
}

function page<T>(items: T[], overrides: Partial<PageResult<T>> = {}): PageResult<T> {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalItems: items.length,
    totalPages: items.length ? 1 : 0,
    ...overrides,
  }
}

function queryResult<T>(data: T | undefined, overrides: Record<string, unknown> = {}) {
  return {
    data,
    isLoading: false,
    isPending: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...overrides,
  }
}

const mocks = vi.hoisted(() => ({
  auth: { isAuthenticated: false },
  books: vi.fn(),
  recommendations: vi.fn(),
  categories: vi.fn(),
  clubs: vi.fn(),
  challenges: vi.fn(),
  addToLibrary: vi.fn(),
  mutateAddToLibrary: vi.fn(),
  retryRecommendations: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useCatalog', () => ({
  useBooks: (...args: unknown[]) => mocks.books(...args),
  useBookRecommendations: (...args: unknown[]) => mocks.recommendations(...args),
  useCategories: (...args: unknown[]) => mocks.categories(...args),
}))

vi.mock('../../hooks/useReading', () => ({
  useAddToLibrary: (...args: unknown[]) => mocks.addToLibrary(...args),
}))

vi.mock('../../hooks/useSocialProduct', () => ({
  useClubs: (...args: unknown[]) => mocks.clubs(...args),
  useChallenges: (...args: unknown[]) => mocks.challenges(...args),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <ExplorePage />
    </MemoryRouter>,
  )
}

describe('personalized book discovery on Explore', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.auth.isAuthenticated = false
    mocks.books.mockReturnValue(queryResult(page([popularBook], { pageSize: 8 })))
    mocks.recommendations.mockReturnValue(
      queryResult(page([recommendation]), { refetch: mocks.retryRecommendations }),
    )
    mocks.categories.mockReturnValue(queryResult(page([])))
    mocks.clubs.mockReturnValue(queryResult(page([])))
    mocks.challenges.mockReturnValue(queryResult(page([])))
    mocks.mutateAddToLibrary.mockResolvedValue({ id: 'library-entry-1' })
    mocks.addToLibrary.mockReturnValue({
      mutateAsync: mocks.mutateAddToLibrary,
      isPending: false,
    })
  })

  it('keeps the popular discovery experience for guests', () => {
    renderPage()

    expect(screen.queryByRole('heading', { name: 'Dành cho bạn' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Được đọc nhiều' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: popularBook.title })).toBeInTheDocument()
  })

  it('links discovery to public author and category profiles', () => {
    mocks.categories.mockReturnValue(
      queryResult(page([{ id: 'category-1', name: 'Kinh điển', bookCount: 6 }])),
    )

    renderPage()

    expect(screen.getByRole('link', { name: 'Xem tác giả' })).toHaveAttribute('href', '/authors')
    expect(screen.getByRole('link', { name: 'Xem mọi thể loại' })).toHaveAttribute(
      'href',
      '/categories',
    )
    expect(screen.getByRole('link', { name: 'Kinh điển · 6' })).toHaveAttribute(
      'href',
      '/categories/category-1',
    )
  })

  it('renders the recommendation reason and adds a book only once while pending', async () => {
    mocks.auth.isAuthenticated = true
    let resolveMutation: ((value: { id: string }) => void) | undefined
    mocks.mutateAddToLibrary.mockReturnValue(
      new Promise((resolve) => {
        resolveMutation = resolve
      }),
    )
    const user = userEvent.setup()
    renderPage()

    expect(screen.getByRole('heading', { name: 'Dành cho bạn' })).toBeInTheDocument()
    expect(screen.getByText(recommendation.reasonText)).toBeInTheDocument()
    const addButton = screen.getByRole('button', {
      name: `Thêm ${recommendedBook.title} vào kệ Muốn đọc`,
    })
    await user.click(addButton)
    expect(addButton).toBeDisabled()
    await user.click(addButton)
    expect(mocks.mutateAddToLibrary).toHaveBeenCalledOnce()
    expect(mocks.mutateAddToLibrary).toHaveBeenCalledWith({
      bookId: recommendedBook.id,
      shelf: 'WANT_TO_READ',
    })

    await act(async () => resolveMutation?.({ id: 'library-entry-1' }))
    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith(
        `Đã thêm “${recommendedBook.title}” vào kệ Muốn đọc`,
        'success',
      ),
    )
  })

  it('shows the API error when quick-add fails', async () => {
    mocks.auth.isAuthenticated = true
    mocks.mutateAddToLibrary.mockRejectedValue(new Error('Máy chủ đang bận'))
    const user = userEvent.setup()
    renderPage()

    await user.click(
      screen.getByRole('button', {
        name: `Thêm ${recommendedBook.title} vào kệ Muốn đọc`,
      }),
    )

    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith('Máy chủ đang bận', 'error'),
    )
  })

  it('renders loading, retryable error, and empty states', async () => {
    mocks.auth.isAuthenticated = true
    mocks.recommendations.mockReturnValue(queryResult(undefined, { isLoading: true }))
    const view = renderPage()
    expect(screen.getByLabelText('Đang tải dữ liệu')).toBeInTheDocument()

    mocks.recommendations.mockReturnValue(
      queryResult(undefined, {
        isError: true,
        error: new Error('Mất kết nối tạm thời'),
        refetch: mocks.retryRecommendations,
      }),
    )
    view.rerender(
      <MemoryRouter>
        <ExplorePage />
      </MemoryRouter>,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('Mất kết nối tạm thời')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(mocks.retryRecommendations).toHaveBeenCalledOnce()

    mocks.recommendations.mockReturnValue(queryResult(page([])))
    view.rerender(
      <MemoryRouter>
        <ExplorePage />
      </MemoryRouter>,
    )
    expect(screen.getByRole('heading', { name: 'Chưa có gợi ý mới' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Xem toàn bộ catalog' })).toHaveAttribute(
      'href',
      '/books',
    )
  })

  it('requests the next personalized page without changing the guest catalog', async () => {
    mocks.auth.isAuthenticated = true
    const secondBook = { ...recommendedBook, id: 'recommended-book-2', title: 'Trang sách thứ hai' }
    mocks.recommendations.mockImplementation(({ page: requestedPage }: { page: number }) =>
      queryResult(
        page(requestedPage === 2 ? [{ ...recommendation, book: secondBook }] : [recommendation], {
          page: requestedPage,
          totalItems: 13,
          totalPages: 2,
        }),
      ),
    )
    const user = userEvent.setup()
    renderPage()

    await user.click(screen.getByRole('button', { name: 'Trang sau' }))

    await waitFor(() =>
      expect(mocks.recommendations).toHaveBeenLastCalledWith({ page: 2, pageSize: 12 }),
    )
    expect(screen.getByRole('heading', { name: secondBook.title })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: popularBook.title })).toBeInTheDocument()
  })
})
