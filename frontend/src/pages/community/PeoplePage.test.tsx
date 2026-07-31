import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import type { User, UserDiscoveryItem } from '../../types/domain'

const reader: User = {
  id: 'reader-1',
  email: 'reader@example.com',
  displayName: 'Bạn đọc',
  role: 'USER',
}

const person: UserDiscoveryItem = {
  id: 'person-1',
  displayName: 'Minh Anh',
  bio: 'Mỗi cuốn sách là một cuộc đối thoại mới.',
  avatarUrl: undefined,
  followerCount: 12,
  booksReadCount: 8,
  isFollowing: false,
  followsYou: false,
  mutualFollowCount: 0,
  reason: 'DIRECTORY',
  reasonText: 'Độc giả đang hoạt động trên BookSpace.',
}

const suggestion: UserDiscoveryItem = {
  ...person,
  id: 'suggestion-1',
  displayName: 'Hà Linh',
  followsYou: true,
  mutualFollowCount: 1,
  reason: 'MUTUAL_FOLLOWS',
  reasonText: '1 người bạn theo dõi cũng theo dõi độc giả này.',
}

function page(items: UserDiscoveryItem[]) {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalItems: items.length,
    totalPages: 1,
  }
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
  people: vi.fn(),
  suggestions: vi.fn(),
  follow: vi.fn(),
  mutateFollow: vi.fn(),
  retryPeople: vi.fn(),
  retrySuggestions: vi.fn(),
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
  usePeopleSearch: (...args: unknown[]) => mocks.people(...args),
  usePeopleSuggestions: (...args: unknown[]) => mocks.suggestions(...args),
  useFollowUser: (...args: unknown[]) => mocks.follow(...args),
  useFeed: vi.fn(),
  useReviews: vi.fn(),
  useUser: vi.fn(),
}))

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="current-location">{`${location.pathname}${location.search}`}</output>
}

function renderProductionApp(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <LocationProbe />
      <App />
    </MemoryRouter>,
  )
}

function queryResult(overrides: Record<string, unknown> = {}) {
  return {
    data: page([person]),
    isLoading: false,
    isPending: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: mocks.retryPeople,
    ...overrides,
  }
}

describe('production people route', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, {
      user: null,
      isAuthenticated: false,
      isLoading: false,
    })
    mocks.people.mockReturnValue(queryResult())
    mocks.suggestions.mockReturnValue({
      data: page([]),
      isLoading: false,
      isPending: false,
      isError: false,
      error: null,
      refetch: mocks.retrySuggestions,
    })
    mocks.follow.mockReturnValue({
      mutate: mocks.mutateFollow,
      isPending: false,
    })
  })

  it('renders guest search from URL and keeps a login-return CTA on every result', async () => {
    renderProductionApp('/people?search=Minh&page=2')

    expect(await screen.findByRole('heading', { name: 'Tìm người cùng nhịp đọc' })).toBeInTheDocument()
    expect(screen.getByLabelText('Tên độc giả')).toHaveValue('Minh')
    expect(mocks.people).toHaveBeenCalledWith('Minh', 2, 12, true)
    expect(screen.getByRole('link', { name: 'Minh Anh' })).toHaveAttribute(
      'href',
      '/users/person-1',
    )
    expect(screen.getByRole('link', { name: 'Đăng nhập để theo dõi Minh Anh' })).toHaveAttribute(
      'href',
      '/login',
    )
    expect(screen.queryByText(/@person/)).not.toBeInTheDocument()
  })

  it('writes submitted search to the production URL query string', async () => {
    const user = userEvent.setup()
    renderProductionApp('/people')

    const input = await screen.findByLabelText('Tên độc giả')
    await user.clear(input)
    await user.type(input, '  Lan Chi  ')
    await user.click(screen.getByRole('button', { name: 'Tìm độc giả' }))

    await waitFor(() =>
      expect(screen.getByTestId('current-location')).toHaveTextContent('/people?search=Lan+Chi'),
    )
    expect(mocks.people).toHaveBeenLastCalledWith('Lan Chi', 1, 12, true)
  })

  it('shows authenticated suggestions with reason and runs the follow action once', async () => {
    Object.assign(mocks.auth, {
      user: reader,
      isAuthenticated: true,
    })
    mocks.suggestions.mockReturnValue({
      data: page([suggestion]),
      isLoading: false,
      isPending: false,
      isError: false,
      error: null,
      refetch: mocks.retrySuggestions,
    })
    const user = userEvent.setup()
    renderProductionApp('/people')

    expect(await screen.findByRole('heading', { name: 'Dành cho bạn' })).toBeInTheDocument()
    expect(screen.getByText(suggestion.reasonText)).toBeInTheDocument()
    expect(screen.getByText('Đang theo dõi bạn')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Theo dõi Hà Linh' }))
    expect(mocks.follow).toHaveBeenCalledWith(suggestion.id, false)
    expect(mocks.mutateFollow).toHaveBeenCalledOnce()
  })

  it('renders loading, error with retry, and empty states without changing routes', async () => {
    mocks.people.mockReturnValue(queryResult({ data: undefined, isLoading: true }))
    const view = renderProductionApp('/people')
    expect(await screen.findByLabelText('Đang tải dữ liệu')).toBeInTheDocument()

    mocks.people.mockReturnValue(
      queryResult({
        data: undefined,
        isLoading: false,
        isError: true,
        error: new Error('Mạng tạm thời gián đoạn'),
      }),
    )
    view.rerender(
      <MemoryRouter initialEntries={['/people']}>
        <LocationProbe />
        <App />
      </MemoryRouter>,
    )
    const user = userEvent.setup()
    expect(await screen.findByRole('alert')).toHaveTextContent('Mạng tạm thời gián đoạn')
    await user.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(mocks.retryPeople).toHaveBeenCalledOnce()

    mocks.people.mockReturnValue(queryResult({ data: page([]), isError: false }))
    view.rerender(
      <MemoryRouter initialEntries={['/people']}>
        <LocationProbe />
        <App />
      </MemoryRouter>,
    )
    expect(
      await screen.findByRole('heading', { name: 'Chưa có độc giả để khám phá' }),
    ).toBeInTheDocument()
  })

  it('renders a loading skeleton while authentication is bootstrapping', async () => {
    mocks.auth.isLoading = true
    mocks.people.mockReturnValue(
      queryResult({ data: undefined, isLoading: false, isPending: true }),
    )

    renderProductionApp('/people')

    expect(await screen.findByLabelText('Đang tải dữ liệu')).toBeInTheDocument()
    expect(screen.queryByText('Chưa có độc giả để khám phá')).not.toBeInTheDocument()
  })

  it('canonicalizes an out-of-range page instead of showing a false empty directory', async () => {
    mocks.people.mockImplementation((_search: string, requestedPage: number) =>
      requestedPage === 999
        ? queryResult({
            data: {
              items: [],
              page: 999,
              pageSize: 12,
              totalItems: 1,
              totalPages: 1,
            },
          })
        : queryResult(),
    )

    renderProductionApp('/people?page=999')

    await waitFor(() =>
      expect(screen.getByTestId('current-location')).toHaveTextContent('/people'),
    )
    expect(await screen.findByRole('link', { name: 'Minh Anh' })).toBeInTheDocument()
    expect(screen.queryByText('Chưa có độc giả để khám phá')).not.toBeInTheDocument()
  })

  it('exposes conditional validation relationships on the search input', async () => {
    const user = userEvent.setup()
    renderProductionApp('/people')
    const input = await screen.findByLabelText('Tên độc giả')

    expect(input).toHaveAttribute('aria-invalid', 'false')
    expect(input).toHaveAttribute('aria-describedby', 'people-search-hint')
    await user.type(input, 'a')
    await user.click(screen.getByRole('button', { name: 'Tìm độc giả' }))

    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAttribute(
      'aria-describedby',
      'people-search-hint people-search-error',
    )
    expect(screen.getByRole('alert')).toHaveTextContent('2 đến 100 ký tự')
    expect(screen.queryByLabelText('Đang tải dữ liệu')).not.toBeInTheDocument()
  })

  it('keeps long public names and bios breakable on narrow cards', async () => {
    const longName = 'TênĐộcGiảKhôngCóKhoảngTrắng'.repeat(5)
    const longBio = 'TiểuSửCôngKhaiKhôngCóKhoảngTrắng'.repeat(8)
    mocks.people.mockReturnValue(
      queryResult({ data: page([{ ...person, displayName: longName, bio: longBio }]) }),
    )

    renderProductionApp('/people')

    expect(await screen.findByRole('link', { name: longName })).toHaveClass('break-words')
    expect(screen.getByText(longBio)).toHaveClass('break-words')
    expect(
      screen.getByRole('link', { name: `Đăng nhập để theo dõi ${longName}` }),
    ).toHaveAttribute('href', '/login')
  })
})
