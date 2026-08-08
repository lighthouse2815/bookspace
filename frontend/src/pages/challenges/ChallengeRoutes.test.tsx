import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import type { Challenge, ChallengeLeaderboardItem, User } from '../../types/domain'

const reader: User = {
  id: 'reader-1',
  email: 'reader@example.com',
  displayName: 'Bạn đọc',
  role: 'USER',
}

const challenge: Challenge = {
  id: 'challenge-123',
  title: 'Đọc sâu mỗi ngày',
  description: 'Một thử thách có dữ liệu thật.',
  startDate: '2026-07-01T00:00:00Z',
  endDate: '2026-07-31T23:59:59Z',
  goalBooks: 3,
  currentBooks: 2,
  participantCount: 12,
  isJoined: true,
  coverImageUrl: undefined,
  isPublished: true,
  completedAt: undefined,
}

const leaderboardItems: ChallengeLeaderboardItem[] = [
  {
    rank: 1,
    user: {
      id: 'reader-2',
      displayName: 'Hà Linh',
      avatarUrl: undefined,
      role: 'USER',
    },
    currentBooks: 3,
    targetBooks: 3,
    progressPercent: 100,
    completedAt: '2026-07-15T09:00:00Z',
    isCurrentUser: false,
  },
  {
    rank: 2,
    user: reader,
    currentBooks: 2,
    targetBooks: 3,
    progressPercent: 67,
    completedAt: null,
    isCurrentUser: true,
  },
]

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
  detail: vi.fn(),
  leaderboard: vi.fn(),
  list: vi.fn(),
  membership: vi.fn(),
  mutateMembership: vi.fn(),
  retryDetail: vi.fn(),
  retryLeaderboard: vi.fn(),
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

vi.mock('../../hooks/useSocialProduct', () => ({
  useChallenge: (id: string) => mocks.detail(id),
  useChallengeLeaderboard: (id: string, page: number, pageSize: number) =>
    mocks.leaderboard(id, page, pageSize),
  useChallenges: () => mocks.list(),
  useChallengeMembership: (id: string, joined: boolean) => mocks.membership(id, joined),
}))

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="current-location">{location.pathname}</output>
}

function renderProductionApp(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <LocationProbe />
      <App />
    </MemoryRouter>,
  )
}

function detailResult(overrides: Record<string, unknown> = {}) {
  return {
    data: challenge,
    isLoading: false,
    isError: false,
    error: null,
    refetch: mocks.retryDetail,
    ...overrides,
  }
}

function leaderboardResult(overrides: Record<string, unknown> = {}) {
  return {
    data: {
      items: leaderboardItems,
      page: 1,
      pageSize: 10,
      totalItems: leaderboardItems.length,
      totalPages: 1,
    },
    isPending: false,
    isLoading: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: mocks.retryLeaderboard,
    ...overrides,
  }
}

describe('production challenge routes', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, {
      user: reader,
      isAuthenticated: true,
      isLoading: false,
    })
    mocks.auth.login.mockResolvedValue(reader)
    mocks.detail.mockReturnValue(detailResult())
    mocks.leaderboard.mockReturnValue(leaderboardResult())
    mocks.list.mockReturnValue({
      data: { items: [challenge], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    mocks.membership.mockReturnValue({
      mutateAsync: mocks.mutateMembership,
      isPending: false,
    })
    mocks.mutateMembership.mockResolvedValue(undefined)
  })

  it('resolves a direct detail deep-link through the production App router', async () => {
    renderProductionApp('/challenges/challenge-123')

    expect(await screen.findByRole('heading', { name: 'Đọc sâu mỗi ngày' })).toBeInTheDocument()
    expect(mocks.detail).toHaveBeenCalledWith('challenge-123')
    expect(screen.getByTestId('current-location')).toHaveTextContent('/challenges/challenge-123')
    expect(screen.getByText('2/3 cuốn đã hoàn thành')).toBeInTheDocument()
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Lưu' })).not.toBeInTheDocument()
  })

  it('links every list card to its production detail route', async () => {
    renderProductionApp('/challenges')

    expect(await screen.findByRole('link', { name: 'Đọc sâu mỗi ngày' })).toHaveAttribute(
      'href',
      '/challenges/challenge-123',
    )
  })

  it('renders loading and empty detail states', async () => {
    mocks.detail.mockReturnValue(detailResult({ data: undefined, isLoading: true }))
    const view = renderProductionApp('/challenges/challenge-123')

    expect(await screen.findByLabelText('Đang tải dữ liệu')).toBeInTheDocument()

    mocks.detail.mockReturnValue(detailResult({ data: undefined }))
    view.rerender(
      <MemoryRouter initialEntries={['/challenges/challenge-123']}>
        <LocationProbe />
        <App />
      </MemoryRouter>,
    )

    expect(
      await screen.findByRole('heading', { name: 'Không có dữ liệu thử thách' }),
    ).toBeInTheDocument()
  })

  it('does not misreport a server failure as a missing challenge and supports retry', async () => {
    mocks.detail.mockReturnValue(
      detailResult({
        data: undefined,
        isError: true,
        error: {
          isAxiosError: true,
          response: { status: 500, data: {} },
        },
      }),
    )
    const user = userEvent.setup()
    renderProductionApp('/challenges/challenge-123')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Không thể tải chi tiết thử thách. Vui lòng thử lại.')
    expect(alert).not.toHaveTextContent('Không tìm thấy thử thách')

    await user.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(mocks.retryDetail).toHaveBeenCalledOnce()
  })

  it.each([
    ['detail CTA', '/challenges/challenge-123'],
    ['list-card CTA', '/challenges'],
  ])('returns to the intended challenge after login from the %s', async (_label, entryPath) => {
    Object.assign(mocks.auth, {
      user: null,
      isAuthenticated: false,
      isLoading: false,
    })
    const user = userEvent.setup()
    renderProductionApp(entryPath)

    await user.click(await screen.findByRole('link', { name: 'Đăng nhập để tham gia' }))
    await waitFor(() => {
      expect(screen.getByTestId('current-location')).toHaveTextContent('/login')
    })
    expect(await screen.findByRole('heading', { name: 'Chào mừng bạn quay lại' })).toBeInTheDocument()

    await user.type(screen.getByLabelText('Email'), 'reader@example.com')
    await user.type(screen.getByLabelText('Mật khẩu'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    await waitFor(() => {
      expect(screen.getByTestId('current-location')).toHaveTextContent(
        '/challenges/challenge-123',
      )
    })
    expect(await screen.findByRole('heading', { name: 'Đọc sâu mỗi ngày' })).toBeInTheDocument()
    expect(mocks.auth.login).toHaveBeenCalledWith({
      email: 'reader@example.com',
      password: 'password123',
    })
  })

  it.each([
    [false, 'Tham gia thử thách'],
    [true, 'Rời thử thách'],
  ])('runs the membership mutation for joined=%s', async (isJoined, buttonName) => {
    mocks.detail.mockReturnValue(detailResult({ data: { ...challenge, isJoined } }))
    const user = userEvent.setup()
    renderProductionApp('/challenges/challenge-123')

    await user.click(await screen.findByRole('button', { name: buttonName }))

    expect(mocks.membership).toHaveBeenCalledWith('challenge-123', isJoined)
    expect(mocks.mutateMembership).toHaveBeenCalledOnce()
  })

  it('renders leaderboard rows in API order and highlights the current reader', async () => {
    renderProductionApp('/challenges/challenge-123')

    const list = await screen.findByRole('list', { name: 'Bảng xếp hạng thử thách' })
    const rows = within(list).getAllByRole('listitem')

    expect(mocks.leaderboard).toHaveBeenCalledWith('challenge-123', 1, 10)
    expect(rows).toHaveLength(2)
    expect(rows[0]).toHaveTextContent('Hà Linh')
    expect(rows[0]).toHaveTextContent('3/3 cuốn')
    expect(rows[0]).toHaveTextContent('Đã hoàn thành')
    expect(rows[0]).not.toHaveAttribute('aria-current')
    expect(rows[1]).toHaveTextContent('Bạn đọc · Bạn')
    expect(rows[1]).toHaveTextContent('2/3 cuốn')
    expect(rows[1]).toHaveTextContent('Đang thực hiện')
    expect(rows[1]).toHaveAttribute('aria-current', 'true')
    expect(within(rows[0]).getByRole('progressbar', { name: 'Tiến độ của Hà Linh' }))
      .toHaveAttribute('aria-valuetext', '3/3 cuốn, 100%')
    expect(within(rows[1]).getByRole('progressbar', { name: 'Tiến độ của Bạn đọc' }))
      .toHaveAttribute('aria-valuetext', '2/3 cuốn, 67%')
  })

  it('renders the leaderboard loading state', async () => {
    mocks.leaderboard.mockReturnValue(
      leaderboardResult({ data: undefined, isPending: true, isLoading: true }),
    )

    renderProductionApp('/challenges/challenge-123')

    const section = await screen.findByRole('region', { name: 'Bảng xếp hạng' })
    expect(within(section).getByLabelText('Đang tải dữ liệu')).toBeInTheDocument()
  })

  it('renders and retries the leaderboard error state', async () => {
    mocks.leaderboard.mockReturnValue(
      leaderboardResult({ data: undefined, isError: true, error: new Error('network') }),
    )
    const user = userEvent.setup()

    renderProductionApp('/challenges/challenge-123')

    const section = await screen.findByRole('region', { name: 'Bảng xếp hạng' })
    expect(within(section).getByRole('alert')).toHaveTextContent(
      'Không thể tải bảng xếp hạng thử thách.',
    )
    await user.click(within(section).getByRole('button', { name: 'Thử lại' }))
    expect(mocks.retryLeaderboard).toHaveBeenCalledOnce()
  })

  it('renders the leaderboard empty state', async () => {
    mocks.leaderboard.mockReturnValue(
      leaderboardResult({
        data: {
          items: [],
          page: 1,
          pageSize: 10,
          totalItems: 0,
          totalPages: 0,
        },
      }),
    )

    renderProductionApp('/challenges/challenge-123')

    expect(
      await screen.findByRole('heading', { name: 'Chưa có thứ hạng hiển thị' }),
    ).toBeInTheDocument()
  })

  it('moves between leaderboard pages with page size 10', async () => {
    mocks.leaderboard.mockImplementation((_id: string, page: number) =>
      leaderboardResult({
        data: {
          items: leaderboardItems,
          page,
          pageSize: 10,
          totalItems: 22,
          totalPages: 3,
        },
      }),
    )
    const user = userEvent.setup()

    renderProductionApp('/challenges/challenge-123')

    await screen.findByText('Trang 1 / 3')
    await user.click(screen.getByRole('button', { name: 'Trang sau' }))

    await waitFor(() => {
      expect(mocks.leaderboard).toHaveBeenLastCalledWith('challenge-123', 2, 10)
    })
    expect(screen.getByText('Trang 2 / 3')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Trang trước' }))
    await waitFor(() => {
      expect(mocks.leaderboard).toHaveBeenLastCalledWith('challenge-123', 1, 10)
    })
  })
})
