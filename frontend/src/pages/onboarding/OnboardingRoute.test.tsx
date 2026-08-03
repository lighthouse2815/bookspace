import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import type { User } from '../../types/domain'

const reader: User = {
  id: 'reader-1',
  displayName: 'Minh Anh',
  email: 'reader@example.com',
  role: 'USER',
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
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../../contexts/ThemeContext', () => ({
  useTheme: () => ({ theme: 'light', setTheme: vi.fn(), toggleTheme: vi.fn(), isDark: false }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: vi.fn() }),
}))

vi.mock('../../hooks/useNotifications', () => ({
  useUnreadNotificationCount: () => ({ data: { count: 0 } }),
}))

vi.mock('./OnboardingPage', () => ({
  OnboardingPage: () => <h1>Thiết lập sở thích đang hoạt động</h1>,
}))

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname}</output>
}

function renderApp() {
  return render(
    <MemoryRouter initialEntries={['/onboarding']}>
      <LocationProbe />
      <App />
    </MemoryRouter>,
  )
}

describe('protected onboarding route', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, { user: reader, isAuthenticated: true, isLoading: false })
  })

  it('resolves the lazy onboarding page for an authenticated reader', async () => {
    renderApp()

    expect(await screen.findByRole('heading', { name: 'Thiết lập sở thích đang hoạt động' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/onboarding')
  }, 15_000)

  it('redirects a guest to login and preserves onboarding as the intended route', async () => {
    Object.assign(mocks.auth, { user: null, isAuthenticated: false, isLoading: false })
    renderApp()

    await waitFor(
      () => expect(screen.getByTestId('location')).toHaveTextContent('/login'),
      { timeout: 10_000 },
    )
    expect(await screen.findByRole('heading', { name: 'Chào mừng bạn quay lại' })).toBeInTheDocument()
  }, 15_000)
})
