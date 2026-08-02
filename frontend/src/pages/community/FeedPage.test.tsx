import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../../types/api'
import type { FeedItem, User, UserDiscoveryItem } from '../../types/domain'
import { FeedPage } from './FeedPage'

const actor: User = {
  id: 'reader-2',
  displayName: 'Hà Linh',
  role: 'USER',
}

const finishedBook: FeedItem = {
  id: 'finished-1',
  type: 'BOOK_FINISHED',
  actor,
  book: {
    id: 'book-1',
    title: 'Mùa hè năm ấy',
    author: { id: 'author-1', name: 'An Nhiên' },
  },
  progressPercent: 100,
  createdAt: '2026-08-01T08:00:00Z',
}

const readingProgress: FeedItem = {
  ...finishedBook,
  id: 'session-1',
  type: 'READING_PROGRESS',
  progressPercent: 12.5,
}

const suggestion: UserDiscoveryItem = {
  id: 'suggestion-1',
  displayName: 'Minh Anh',
  bio: 'Thích văn học Việt Nam.',
  followerCount: 12,
  booksReadCount: 24,
  isFollowing: false,
  followsYou: true,
  mutualFollowCount: 2,
  reason: 'MUTUAL_FOLLOWS',
  reasonText: '2 người bạn theo dõi cũng theo dõi độc giả này.',
}

function page(
  items: FeedItem[],
  currentPage = 1,
  totalPages = items.length ? 1 : 0,
): PageResult<FeedItem> {
  return {
    items,
    page: currentPage,
    pageSize: 10,
    totalItems: totalPages > 1 ? totalPages * 10 : items.length,
    totalPages,
  }
}

const mocks = vi.hoisted(() => ({
  feed: vi.fn(),
  suggestions: vi.fn(),
  follow: vi.fn(),
  mutateFollow: vi.fn(),
  refetchFeed: vi.fn(),
  refetchSuggestions: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../components/community/UserSafetyActions', () => ({
  MuteUserButton: () => null,
}))

vi.mock('../../hooks/useCommunity', () => ({
  useFeed: (...args: unknown[]) => mocks.feed(...args),
  usePeopleSuggestions: (...args: unknown[]) => mocks.suggestions(...args),
  useFollowUser: (...args: unknown[]) => mocks.follow(...args),
  useLikeReview: vi.fn(),
  useCommentReview: vi.fn(),
  useDeleteReview: vi.fn(),
  useDeleteReviewComment: vi.fn(),
  useReviewComments: vi.fn(),
  useUpdateReview: vi.fn(),
}))

function feedResult(overrides: Record<string, unknown> = {}) {
  return {
    data: page([finishedBook]),
    isLoading: false,
    isPending: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: mocks.refetchFeed,
    ...overrides,
  }
}

function suggestionResult(overrides: Record<string, unknown> = {}) {
  return {
    data: {
      items: [],
      page: 1,
      pageSize: 3,
      totalItems: 0,
      totalPages: 0,
    },
    isLoading: false,
    isPending: false,
    isError: false,
    error: null,
    refetch: mocks.refetchSuggestions,
    ...overrides,
  }
}

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>
}

function renderFeed(path = '/feed') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <FeedPage />
      <LocationProbe />
    </MemoryRouter>,
  )
}

describe('feed v2', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.scrollTo = vi.fn()
    mocks.feed.mockReturnValue(feedResult())
    mocks.suggestions.mockReturnValue(suggestionResult())
    mocks.follow.mockReturnValue({ mutate: mocks.mutateFollow, isPending: false })
  })

  it('reads filter and page from URL, sends API args, and resets page when the filter changes', async () => {
    const user = userEvent.setup()
    mocks.feed.mockReturnValue(
      feedResult({
        data: {
          ...page([finishedBook], 2, 3),
          totalItems: 25,
        },
      }),
    )

    renderFeed('/feed?type=reading&page=2')

    expect(mocks.feed).toHaveBeenCalledWith({ type: 'READING', page: 2, pageSize: 10 })
    expect(screen.getByRole('button', { name: 'Tiến độ' })).toHaveAttribute(
      'aria-current',
      'page',
    )

    await user.click(screen.getByRole('button', { name: 'Trang sau' }))
    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        '/feed?type=reading&page=3',
      ),
    )
    expect(mocks.feed).toHaveBeenLastCalledWith({ type: 'READING', page: 3, pageSize: 10 })

    await user.click(screen.getByRole('button', { name: 'Câu lạc bộ' }))
    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent('/feed?type=club'),
    )
    expect(mocks.feed).toHaveBeenLastCalledWith({ type: 'CLUB', page: 1, pageSize: 10 })
  })

  it('canonicalizes invalid filters and an empty out-of-range page to page one', async () => {
    mocks.feed.mockImplementation(({ page: requestedPage }: { page: number }) =>
      requestedPage === 99
        ? feedResult({ data: page([], 99, 0) })
        : feedResult({ data: page([], 1, 0) }),
    )

    renderFeed('/feed?type=unknown&page=99')

    expect(mocks.feed).toHaveBeenCalledWith({ type: undefined, page: 99, pageSize: 10 })
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/feed$/))
    expect(mocks.feed).toHaveBeenLastCalledWith({ type: undefined, page: 1, pageSize: 10 })
  })

  it('points the empty feed CTA to people discovery', () => {
    mocks.feed.mockReturnValue(feedResult({ data: page([]) }))

    renderFeed()

    expect(screen.getByRole('heading', { name: 'Bảng tin của bạn còn yên ắng' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Khám phá độc giả' })).toHaveAttribute(
      'href',
      '/people',
    )
    expect(screen.queryByRole('link', { name: /Khám phá cộng đồng/ })).not.toBeInTheDocument()
  })

  it('renders completed-book activity with public profile and book deep-links', () => {
    renderFeed()

    expect(screen.getByText('đã hoàn thành một cuốn sách')).toBeInTheDocument()
    expect(screen.getByText('Hoàn thành')).toBeInTheDocument()
    expect(screen.getByText('100%')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: actor.displayName })).toHaveAttribute(
      'href',
      `/users/${actor.id}`,
    )
    expect(screen.getByRole('link', { name: /Mùa hè năm ấy/ })).toHaveAttribute(
      'href',
      '/books/book-1',
    )
  })

  it('describes reading progress as the pages read in that session', () => {
    mocks.feed.mockReturnValue(feedResult({ data: page([readingProgress]) }))

    renderFeed('/feed?type=reading')

    expect(screen.getByText('Đã đọc trong phiên')).toBeInTheDocument()
    expect(screen.getByText('13%')).toBeInTheDocument()
  })

  it('shows compact reader suggestions and runs the existing follow mutation', async () => {
    const user = userEvent.setup()
    mocks.suggestions.mockReturnValue(
      suggestionResult({
        data: {
          items: [suggestion],
          page: 1,
          pageSize: 3,
          totalItems: 1,
          totalPages: 1,
        },
      }),
    )

    renderFeed()

    expect(screen.getByRole('heading', { name: 'Độc giả nên theo dõi' })).toBeInTheDocument()
    expect(screen.getByText(suggestion.reasonText)).toBeInTheDocument()
    expect(mocks.suggestions).toHaveBeenCalledWith(1, 3)
    expect(mocks.follow).toHaveBeenCalledWith(suggestion.id, false)

    await user.click(screen.getByRole('button', { name: `Theo dõi ${suggestion.displayName}` }))
    expect(mocks.mutateFollow).toHaveBeenCalledWith(undefined, expect.any(Object))
  })

  it('renders loading and a retryable request error', async () => {
    mocks.feed.mockReturnValue(
      feedResult({ data: undefined, isLoading: true, isPending: true }),
    )
    const view = renderFeed()

    expect(screen.getByLabelText('Đang tải dữ liệu')).toBeInTheDocument()

    mocks.feed.mockReturnValue(
      feedResult({
        data: undefined,
        isLoading: false,
        isPending: false,
        isError: true,
        error: new Error('Mạng tạm thời gián đoạn'),
      }),
    )
    view.rerender(
      <MemoryRouter initialEntries={['/feed']}>
        <FeedPage />
        <LocationProbe />
      </MemoryRouter>,
    )

    const user = userEvent.setup()
    expect(screen.getByRole('alert')).toHaveTextContent('Mạng tạm thời gián đoạn')
    await user.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(mocks.refetchFeed).toHaveBeenCalledOnce()
  })

  it('allows an explicit refresh without changing the current URL', async () => {
    const user = userEvent.setup()
    renderFeed('/feed?type=review')

    await user.click(screen.getByRole('button', { name: 'Làm mới bảng tin' }))

    expect(mocks.refetchFeed).toHaveBeenCalledOnce()
    expect(screen.getByTestId('location')).toHaveTextContent('/feed?type=review')
  })
})
