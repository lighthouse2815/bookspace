import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BlockUserButton, MuteUserButton } from './UserSafetyActions'

const mocks = vi.hoisted(() => ({
  mute: vi.fn(),
  block: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ user: { id: 'reader-1' } }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useCommunity', () => ({
  useMuteUser: () => ({ mutate: mocks.mute, isPending: false }),
  useBlockUser: () => ({ mutate: mocks.block, isPending: false }),
}))

describe('UserSafetyActions', () => {
  beforeEach(() => vi.clearAllMocks())

  it('mutes another reader and reports success', async () => {
    mocks.mute.mockImplementation((_value, options) => options.onSuccess())
    const user = userEvent.setup()
    render(<MuteUserButton targetId="reader-2" displayName="Hà Linh" />)

    await user.click(screen.getByRole('button', { name: 'Ẩn nội dung của Hà Linh' }))

    expect(mocks.mute).toHaveBeenCalledOnce()
    expect(mocks.toast).toHaveBeenCalledWith('Đã ẩn nội dung từ người đọc này', 'success')
  })

  it('requires confirmation before blocking and completes the callback', async () => {
    mocks.block.mockImplementation((_value, options) => options.onSuccess())
    const onBlocked = vi.fn()
    const user = userEvent.setup()
    render(<BlockUserButton targetId="reader-2" displayName="Hà Linh" onBlocked={onBlocked} />)

    await user.click(screen.getByRole('button', { name: 'Chặn Hà Linh' }))
    expect(screen.getByRole('dialog', { name: 'Chặn Hà Linh?' })).toBeInTheDocument()
    expect(mocks.block).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Xác nhận chặn' }))

    expect(mocks.block).toHaveBeenCalledOnce()
    expect(mocks.toast).toHaveBeenCalledWith('Đã chặn người đọc này', 'success')
    expect(onBlocked).toHaveBeenCalledOnce()
  })
})
