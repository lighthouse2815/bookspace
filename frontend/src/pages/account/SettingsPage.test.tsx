import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SettingsPage } from './SettingsPage'

const mocks = vi.hoisted(() => ({
  updatePrivacy: vi.fn(),
  updateNotificationPreferences: vi.fn(),
  updateProfile: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: {
      id: 'reader-1',
      email: 'reader@example.com',
      displayName: 'Bạn đọc',
      role: 'USER',
    },
    refreshUser: vi.fn(),
  }),
}))

vi.mock('../../contexts/ThemeContext', () => ({
  useTheme: () => ({ theme: 'light', setTheme: vi.fn() }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../services/community.service', () => ({
  communityService: {
    updateProfile: (...args: unknown[]) => mocks.updateProfile(...args),
  },
}))

vi.mock('../../hooks/useCommunity', () => ({
  useUser: () => ({
    data: {
      privacy: {
        isReadingShelfPublic: false,
        isReadingActivityPublic: true,
      },
    },
    isLoading: false,
    isError: false,
  }),
  useUpdateProfilePrivacy: () => ({
    mutateAsync: (...args: unknown[]) => mocks.updatePrivacy(...args),
    isPending: false,
  }),
}))

vi.mock('../../hooks/useNotifications', () => ({
  useNotificationPreferences: () => ({
    data: {
      isFollowNotificationEnabled: true,
      isReviewNotificationEnabled: true,
      isClubNotificationEnabled: false,
      isChallengeNotificationEnabled: true,
    },
    isLoading: false,
    isError: false,
  }),
  useUpdateNotificationPreferences: () => ({
    mutateAsync: (...args: unknown[]) => mocks.updateNotificationPreferences(...args),
    isPending: false,
  }),
}))

describe('profile privacy settings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.updatePrivacy.mockResolvedValue({})
    mocks.updateNotificationPreferences.mockResolvedValue({})
  })

  it('loads server visibility and saves both public profile switches', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    const shelf = await screen.findByRole('checkbox', { name: /Hiển thị kệ sách chi tiết/ })
    const activity = screen.getByRole('checkbox', { name: /Hiển thị dòng hoạt động/ })
    expect(shelf).not.toBeChecked()
    expect(activity).toBeChecked()

    await user.click(shelf)
    await user.click(screen.getByRole('button', { name: 'Lưu quyền riêng tư' }))

    expect(mocks.updatePrivacy).toHaveBeenCalledWith({
      isReadingShelfPublic: true,
      isReadingActivityPublic: true,
    })
    expect(mocks.toast).toHaveBeenCalledWith(
      'Quyền riêng tư hồ sơ đã được cập nhật',
      'success',
    )
  })

  it('loads and saves notification preferences while keeping system notifications on', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    const follow = await screen.findByRole('checkbox', { name: /Người theo dõi mới/ })
    const club = screen.getByRole('checkbox', { name: /Câu lạc bộ và đợt đọc chung/ })
    expect(follow).toBeChecked()
    expect(club).not.toBeChecked()
    expect(screen.getByText(/Thông báo hệ thống luôn bật/)).toBeInTheDocument()

    await user.click(follow)
    await user.click(screen.getByRole('button', { name: 'Lưu tùy chọn thông báo' }))

    expect(mocks.updateNotificationPreferences).toHaveBeenCalledWith({
      isFollowNotificationEnabled: false,
      isReviewNotificationEnabled: true,
      isClubNotificationEnabled: false,
      isChallengeNotificationEnabled: true,
    })
    expect(mocks.toast).toHaveBeenCalledWith(
      'Tùy chọn thông báo đã được cập nhật',
      'success',
    )
  })
})
