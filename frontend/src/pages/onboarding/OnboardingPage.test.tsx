import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Book, Category, OnboardingState, User } from '../../types/domain'
import { OnboardingPage } from './OnboardingPage'

const categories: Category[] = [
  { id: 'category-1', name: 'Văn học', bookCount: 21 },
  { id: 'category-2', name: 'Lịch sử', bookCount: 13 },
  { id: 'category-3', name: 'Khoa học', bookCount: 17 },
]

const books: Book[] = [
  { id: 'book-1', title: 'Khu vườn bí mật', author: { id: 'author-1', name: 'Frances Hodgson Burnett' } },
  { id: 'book-2', title: 'Sapiens', author: { id: 'author-2', name: 'Yuval Noah Harari' } },
  { id: 'book-3', title: 'Vũ trụ', author: { id: 'author-3', name: 'Carl Sagan' } },
]

const pendingState: OnboardingState = {
  status: 'PENDING',
  finishedAt: null,
  preferredCategoryIds: [],
  referenceBookIds: [],
}

const reader: User = {
  id: 'reader-1',
  displayName: 'Minh Anh',
  email: 'reader@example.com',
  role: 'USER',
}

const mocks = vi.hoisted(() => ({
  onboarding: vi.fn(),
  save: vi.fn(),
  complete: vi.fn(),
  skip: vi.fn(),
  toast: vi.fn(),
}))

function page<T>(items: T[]) {
  return { items, page: 1, pageSize: 8, totalItems: items.length, totalPages: items.length ? 1 : 0 }
}

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useOnboarding', () => ({
  useOnboarding: () => mocks.onboarding(),
  useSaveOnboardingPreferences: () => ({ mutateAsync: mocks.save, isPending: false }),
  useCompleteOnboarding: () => ({ mutateAsync: mocks.complete, isPending: false }),
  useSkipOnboarding: () => ({ mutateAsync: mocks.skip, isPending: false }),
}))

vi.mock('../../hooks/useCatalog', () => ({
  useCategories: () => ({ data: page(categories), isLoading: false, isError: false, refetch: vi.fn() }),
  useBooks: () => ({ data: page(books), isLoading: false, isError: false, isFetching: false, refetch: vi.fn() }),
  useBook: (id: string) => ({ data: books.find((book) => book.id === id), isLoading: false }),
  useBookRecommendations: () => ({ data: page([]), isLoading: false, isError: false, refetch: vi.fn() }),
}))

vi.mock('../../hooks/useCommunity', () => ({
  usePeopleSuggestions: () => ({ data: page([]), isLoading: false, isError: false, refetch: vi.fn() }),
  useFollowUser: () => ({ mutate: vi.fn(), isPending: false }),
}))

vi.mock('../../hooks/useReading', () => ({
  useAddToLibrary: () => ({ mutateAsync: vi.fn(), isPending: false }),
}))

vi.mock('../../hooks/useReadingProduct', () => ({
  useReadingGoals: () => ({ data: page([]), isLoading: false, isError: false }),
  useCreateReadingGoal: () => ({ mutateAsync: vi.fn(), isPending: false }),
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ user: reader, isAuthenticated: true, isLoading: false }),
}))

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>
}

function renderPage(state: { from?: string } = { from: '/library?focus=next' }) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: '/onboarding', state }]}>
      <LocationProbe />
      <OnboardingPage />
    </MemoryRouter>,
  )
}

describe('personalized onboarding flow', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal('scrollTo', vi.fn())
    mocks.onboarding.mockReturnValue({
      data: pendingState,
      isLoading: false,
      isPending: false,
      isError: false,
      refetch: vi.fn(),
    })
    mocks.save.mockResolvedValue(pendingState)
    mocks.complete.mockResolvedValue({ ...pendingState, status: 'COMPLETED' })
    mocks.skip.mockResolvedValue({ ...pendingState, status: 'SKIPPED' })
  })

  it('requires 3 categories and 3 books, persists both stages, then returns after completion', async () => {
    const user = userEvent.setup()
    renderPage()

    const nextCategories = screen.getByRole('button', { name: 'Chọn sách tham chiếu' })
    expect(nextCategories).toBeDisabled()

    await user.click(screen.getByRole('button', { name: /Văn học/ }))
    await user.click(screen.getByRole('button', { name: /Lịch sử/ }))
    await user.click(screen.getByRole('button', { name: /Khoa học/ }))
    expect(nextCategories).toBeEnabled()
    await user.click(nextCategories)

    expect(mocks.save).toHaveBeenNthCalledWith(1, {
      preferredCategoryIds: ['category-1', 'category-2', 'category-3'],
      referenceBookIds: [],
    })
    const bookStepHeading = await screen.findByRole('heading', {
      name: 'Chọn vài cuốn bạn thật sự yêu thích',
    })
    await waitFor(() => expect(bookStepHeading).toHaveFocus())

    await user.click(screen.getByRole('button', { name: 'Chọn Khu vườn bí mật' }))
    await user.click(screen.getByRole('button', { name: 'Chọn Sapiens' }))
    await user.click(screen.getByRole('button', { name: 'Chọn Vũ trụ' }))
    await user.click(screen.getByRole('button', { name: 'Xem gợi ý của tôi' }))

    expect(mocks.save).toHaveBeenNthCalledWith(2, {
      preferredCategoryIds: ['category-1', 'category-2', 'category-3'],
      referenceBookIds: ['book-1', 'book-2', 'book-3'],
    })
    const activationHeading = await screen.findByRole('heading', {
      name: 'Góc đọc của bạn đã có hình hài',
    })
    await waitFor(() => expect(activationHeading).toHaveFocus())

    await user.click(screen.getByRole('button', { name: 'Hoàn tất thiết lập' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/library?focus=next'))
    expect(mocks.complete).toHaveBeenCalledOnce()
  }, 15_000)

  it('saves a partial draft before skip and rejects an external-looking return path', async () => {
    const user = userEvent.setup()
    renderPage({ from: '//outside.example/path' })

    await user.click(screen.getByRole('button', { name: /Văn học/ }))
    await user.click(screen.getByRole('button', { name: 'Để sau' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/dashboard'))
    expect(mocks.save).toHaveBeenCalledWith({
      preferredCategoryIds: ['category-1'],
      referenceBookIds: [],
    })
    expect(mocks.skip).toHaveBeenCalledOnce()
    expect(mocks.save.mock.invocationCallOrder[0]).toBeLessThan(mocks.skip.mock.invocationCallOrder[0])
  })
})
