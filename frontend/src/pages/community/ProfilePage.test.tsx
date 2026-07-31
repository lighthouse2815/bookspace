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
  retry: vi.fn(),
  toast: vi.fn(),
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

describe('production public profile route', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, {
      user: null,
      isAuthenticated: false,
      isLoading: false,
    })
    mocks.user.mockReturnValue(userResult())
    mocks.follow.mockReturnValue({ mutate: vi.fn(), isPending: false })
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
})
