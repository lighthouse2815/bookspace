import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RegisterPage } from './AuthPages'

const mocks = vi.hoisted(() => ({
  register: vi.fn(),
  toast: vi.fn(),
  isAuthenticated: false,
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: null,
    isAuthenticated: mocks.isAuthenticated,
    isLoading: false,
    register: mocks.register,
  }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

function LocationProbe() {
  const location = useLocation()
  return (
    <output data-testid="location">
      {JSON.stringify({ pathname: location.pathname, from: (location.state as { from?: string } | null)?.from })}
    </output>
  )
}

describe('registration onboarding route', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.isAuthenticated = false
    mocks.register.mockImplementation(async () => {
      mocks.isAuthenticated = true
      return { id: 'reader-1', displayName: 'Minh Anh', role: 'USER' }
    })
  })

  it('keeps onboarding ahead of the authenticated-register redirect race', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={[{ pathname: '/register', state: { from: '/challenges/open' } }]}>
        <LocationProbe />
        <RegisterPage />
      </MemoryRouter>,
    )

    await user.type(screen.getByLabelText('Tên hiển thị'), 'Minh Anh')
    await user.type(screen.getByLabelText('Email'), 'minhanh@example.com')
    await user.type(screen.getByLabelText('Mật khẩu'), 'password123')
    await user.type(screen.getByLabelText('Xác nhận mật khẩu'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Tạo tài khoản' }))

    await waitFor(() => {
      expect(screen.getByTestId('location')).toHaveTextContent('"pathname":"/onboarding"')
      expect(screen.getByTestId('location')).toHaveTextContent('"from":"/challenges/open"')
    })
    expect(mocks.register).toHaveBeenCalledWith({
      displayName: 'Minh Anh',
      email: 'minhanh@example.com',
      password: 'password123',
    })
  }, 15_000)
})
