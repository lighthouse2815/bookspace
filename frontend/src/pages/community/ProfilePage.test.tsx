import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import type { User } from '../../types/domain'

const profile: User = {
  id: 'person-1',
  email: null,
  displayName: 'Hà Linh',
  bio: 'Đọc truyện ngắn và tản văn.',
  role: 'USER',
  followerCount: 4,
  followingCount: 2,
  booksReadCount: 6,
  isFollowing: false,
  followsYou: true,
  mutualFollowCount: 2,
  privacy: {
    isReadingShelfPublic: true,
    isReadingActivityPublic: true,
  },
  joinedAt: '2026-01-01T00:00:00Z',
}

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isAuthenticated: false,
    isLoading: false,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  },
  user: vi.fn(),
  follow: vi.fn(),
  library: vi.fn(),
  reviews: vi.fn(),
  activity: vi.fn(),
  connections: vi.fn(),
  mute: vi.fn(),
  block: vi.fn(),
  retry: vi.fn(),
  toast: vi.fn(),
  startConversation: vi.fn(),
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../../contexts/ThemeContext', () => ({
  useTheme: () => ({
    theme: 'light',
    setTheme: vi.fn(),
    toggleTheme: vi.fn(),
    isDark: false,
  }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useCommunity', () => ({
  useUser: (...args: unknown[]) => mocks.user(...args),
  useFollowUser: (...args: unknown[]) => mocks.follow(...args),
  useUserLibrary: (...args: unknown[]) => mocks.library(...args),
  useUserReviews: (...args: unknown[]) => mocks.reviews(...args),
  useUserActivity: (...args: unknown[]) => mocks.activity(...args),
  useUserConnections: (...args: unknown[]) => mocks.connections(...args),
  useMuteUser: (...args: unknown[]) => mocks.mute(...args),
  useBlockUser: (...args: unknown[]) => mocks.block(...args),
}))

vi.mock('../../hooks/useDirectMessages', () => ({
  useStartConversation: () => ({
    mutate: mocks.startConversation,
    isPending: false,
  }),
}))

function renderProfile() {
  return render(
    <MemoryRouter initialEntries={['/users/person-1']}>
      <LocationProbe />
      <App />
    </MemoryRouter>,
  )
}

function LocationProbe() {
  const location = useLocation()
  const state = location.state as { from?: string } | null
  return (
    <>
      <output data-testid="current-location">{`${location.pathname}${location.search}`}</output>
      <output data-testid="location-from">{state?.from ?? ''}</output>
    </>
  )
}

function userResult(overrides: Record<string, unknown> = {}) {
  return {
    data: profile,
    isLoading: false,
    isPending: false,
    isError: false,
    error: null,
    refetch: mocks.retry,
    ...overrides,
  }
}

function page(items: unknown[] = []) {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalItems: items.length,
    totalPages: 1,
  }
}

function queryResult(items: unknown[] = []) {
  return {
    data: page(items),
    isLoading: false,
    isPending: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }
}

describe('production public profile route', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.mute.mockReturnValue({ mutate: vi.fn(), isPending: false })
    mocks.block.mockReturnValue({ mutate: vi.fn(), isPending: false })
    Object.assign(mocks.auth, {
      user: null,
      isAuthenticated: false,
      isLoading: false,
    })
    mocks.user.mockReturnValue(userResult())
    mocks.follow.mockReturnValue({ mutate: vi.fn(), isPending: false })
    mocks.startConversation.mockReset()
    mocks.library.mockReturnValue(queryResult())
    mocks.reviews.mockReturnValue(queryResult())
    mocks.activity.mockReturnValue(queryResult())
    mocks.connections.mockReturnValue(queryResult())
  })

  it('offers guest login-return without inventing a username from the id', async () => {
    const user = userEvent.setup()
    renderProfile()

    expect(await screen.findByRole('heading', { name: 'Hà Linh' })).toBeInTheDocument()
    const loginLink = screen.getByRole('link', { name: 'Đăng nhập để theo dõi Hà Linh' })
    expect(loginLink).toHaveAttribute('href', '/login')
    expect(screen.queryByText('@person-1')).not.toBeInTheDocument()
    await user.click(loginLink)
    await waitFor(() => {
      expect(screen.getByTestId('current-location')).toHaveTextContent('/login')
    })
    expect(screen.getByTestId('location-from')).toHaveTextContent('/users/person-1')
  })

  it('renders a dedicated missing-profile state for 404', async () => {
    mocks.user.mockReturnValue(
      userResult({
        data: undefined,
        isError: true,
        error: { isAxiosError: true, response: { status: 404 } },
      }),
    )
    renderProfile()

    expect(
      await screen.findByRole('heading', { name: 'Không tìm thấy hồ sơ' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Xem danh sách độc giả' })).toHaveAttribute(
      'href',
      '/people',
    )
  })

  it('does not misreport a server failure as a missing profile', async () => {
    mocks.user.mockReturnValue(
      userResult({
        data: undefined,
        isError: true,
        error: { isAxiosError: true, response: { status: 500 } },
      }),
    )
    renderProfile()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không thể tải hồ sơ người đọc.',
    )
    expect(screen.queryByText('Không tìm thấy hồ sơ')).not.toBeInTheDocument()
  })

  it('keeps the profile skeleton visible while auth bootstrap owns the query pause', async () => {
    mocks.auth.isLoading = true
    mocks.user.mockReturnValue(
      userResult({ data: undefined, isLoading: false, isPending: true }),
    )

    renderProfile()

    expect(document.querySelector('.animate-pulse')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('includes the profile display name in the authenticated follow action', async () => {
    Object.assign(mocks.auth, {
      user: { ...profile, id: 'reader-1' },
      isAuthenticated: true,
    })

    renderProfile()

    expect(await screen.findByRole('button', { name: 'Theo dõi Hà Linh' })).toBeEnabled()
  })

  it('offers direct messaging only when both readers follow each other', async () => {
    Object.assign(mocks.auth, {
      user: { ...profile, id: 'reader-1' },
      isAuthenticated: true,
    })
    mocks.user.mockReturnValue(
      userResult({ data: { ...profile, isFollowing: true, followsYou: true } }),
    )
    const user = userEvent.setup()

    renderProfile()
    await user.click(await screen.findByRole('button', { name: 'Nhắn tin' }))

    expect(mocks.startConversation).toHaveBeenCalledWith(
      'person-1',
      expect.objectContaining({ onSuccess: expect.any(Function), onError: expect.any(Function) }),
    )
  })

  it('keeps long public profile text breakable inside the mobile surface', async () => {
    const longName = 'TênĐộcGiảKhôngCóKhoảngTrắng'.repeat(5)
    const longBio = 'TiểuSửCôngKhaiKhôngCóKhoảngTrắng'.repeat(8)
    mocks.user.mockReturnValue(
      userResult({ data: { ...profile, displayName: longName, bio: longBio } }),
    )

    renderProfile()

    expect(await screen.findByRole('heading', { name: longName })).toHaveClass('break-words')
    expect(screen.getByText(longBio)).toHaveClass('break-words')
  })

  it('opens the real public shelf tab and renders paginated book data', async () => {
    mocks.library.mockReturnValue(
      queryResult([
        {
          bookId: 'book-1',
          book: {
            id: 'book-1',
            title: 'Một cuốn sách công khai',
            author: { id: 'author-1', name: 'Tác giả' },
            averageRating: 4.5,
            reviewCount: 2,
          },
          shelf: 'READING',
          progressPercent: 42,
          updatedAt: '2026-07-30T00:00:00Z',
        },
      ]),
    )
    const user = userEvent.setup()
    renderProfile()

    await user.click(await screen.findByRole('button', { name: 'Kệ sách' }))

    expect(await screen.findByText('Một cuốn sách công khai')).toBeInTheDocument()
    expect(screen.getByText('42%')).toBeInTheDocument()
    expect(mocks.library).toHaveBeenLastCalledWith('person-1', undefined, 1, 12, true)
  })

  it('does not request a private shelf for another viewer', async () => {
    mocks.user.mockReturnValue(
      userResult({
        data: {
          ...profile,
          privacy: { isReadingShelfPublic: false, isReadingActivityPublic: false },
        },
      }),
    )
    const user = userEvent.setup()
    renderProfile()

    await user.click(await screen.findByRole('button', { name: 'Kệ sách' }))

    expect(await screen.findByRole('heading', { name: 'Kệ sách đang riêng tư' })).toBeInTheDocument()
    expect(mocks.library).toHaveBeenLastCalledWith('person-1', undefined, 1, 12, false)
  })

  it('opens the follower list from the observable profile counter', async () => {
    mocks.connections.mockReturnValue(
      queryResult([
        {
          id: 'follower-1',
          displayName: 'Người đọc kết nối',
          role: 'USER',
        },
      ]),
    )
    const user = userEvent.setup()
    renderProfile()

    await user.click(await screen.findByRole('button', { name: /4 người theo dõi/ }))

    expect(await screen.findByRole('dialog', { name: 'Người theo dõi' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Xem hồ sơ Người đọc kết nối' })).toHaveAttribute(
      'href',
      '/users/follower-1',
    )
  })
})
