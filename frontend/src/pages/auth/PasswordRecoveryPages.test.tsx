import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ForgotPasswordPage, ResetPasswordPage } from './PasswordRecoveryPages'

const mocks = vi.hoisted(() => ({
  requestPasswordReset: vi.fn(),
  resetPassword: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../services/auth.service', () => ({
  authService: {
    requestPasswordReset: mocks.requestPasswordReset,
    resetPassword: mocks.resetPassword,
  },
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

describe('password recovery pages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    mocks.requestPasswordReset.mockResolvedValue(null)
    mocks.resetPassword.mockResolvedValue(null)
  })

  it('submits an email and keeps the success state enumeration-safe', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <ForgotPasswordPage />
      </MemoryRouter>,
    )

    await user.type(screen.getByLabelText('Email'), 'reader@bookspace.local')
    await user.click(screen.getByRole('button', { name: 'Gửi hướng dẫn' }))

    await screen.findByRole('heading', { name: 'Kiểm tra hộp thư của bạn' })
    expect(mocks.requestPasswordReset).toHaveBeenCalledWith('reader@bookspace.local')
    expect(screen.getByRole('status')).toHaveTextContent(
      'Nếu reader@bookspace.local thuộc một tài khoản BookSpace',
    )
  })

  it('confirms a strong password with the token and clears the old local session', async () => {
    const user = userEvent.setup()
    localStorage.setItem(
      'bookspace.tokens',
      JSON.stringify({ accessToken: 'old-access', refreshToken: 'old-refresh' }),
    )
    render(
      <MemoryRouter initialEntries={['/reset-password?token=secure-token']}>
        <ResetPasswordPage />
      </MemoryRouter>,
    )

    await user.type(screen.getByLabelText('Mật khẩu mới'), 'Reader456!')
    await user.type(screen.getByLabelText('Xác nhận mật khẩu mới'), 'Reader456!')
    await user.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu' }))

    await screen.findByRole('heading', { name: 'Mật khẩu đã được cập nhật' })
    expect(mocks.resetPassword).toHaveBeenCalledWith({
      token: 'secure-token',
      password: 'Reader456!',
    })
    await waitFor(() => expect(localStorage.getItem('bookspace.tokens')).toBeNull())
  })

  it('does not show the reset form when the URL has no token', () => {
    render(
      <MemoryRouter initialEntries={['/reset-password']}>
        <ResetPasswordPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'Liên kết chưa hợp lệ' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Mật khẩu mới')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Gửi yêu cầu mới' })).toHaveAttribute(
      'href',
      '/forgot-password',
    )
  })
})
