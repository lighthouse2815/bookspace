import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SettingsPage } from './SettingsPage'

const mocks = vi.hoisted(() => ({
  updatePrivacy: vi.fn(),
  updateNotificationPreferences: vi.fn(),
  updateProfile: vi.fn(),
  unmute: vi.fn(),
  unblock: vi.fn(),
  safety: vi.fn(),
  onboarding: vi.fn(),
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
  useUserSafetyList: () => mocks.safety(),
  useMuteUser: () => ({ mutate: mocks.unmute, isPending: false }),
  useUnblockUser: () => ({ mutate: mocks.unblock, isPending: false }),
}))

vi.mock('../../hooks/useNotifications', () => ({
  useNotificationPreferences: () => ({
    data: {
      isFollowNotificationEnabled: true,
      isReviewNotificationEnabled: true,
      isClubNotificationEnabled: false,
      isChallengeNotificationEnabled: true,
      isDirectMessageNotificationEnabled: true,
    },
    isLoading: false,
    isError: false,
  }),
  useUpdateNotificationPreferences: () => ({
    mutateAsync: (...args: unknown[]) => mocks.updateNotificationPreferences(...args),
    isPending: false,
  }),
}))

vi.mock('../../hooks/useOnboarding', () => ({
  useOnboarding: () => mocks.onboarding(),
}))

describe('profile privacy settings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.updatePrivacy.mockResolvedValue({})
    mocks.updateNotificationPreferences.mockResolvedValue({})
    mocks.onboarding.mockReturnValue({
      data: {
        status: 'COMPLETED',
        finishedAt: '2026-08-02T08:00:00Z',
        preferredCategoryIds: ['category-1', 'category-2', 'category-3'],
        referenceBookIds: ['book-1', 'book-2', 'book-3'],
      },
      isLoading: false,
      isError: false,
    })
    mocks.safety.mockReturnValue({
      data: { items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 0 },
      isLoading: false,
      isError: false,
    })
  })

  it('keeps the edit destination stable while onboarding status is loading', () => {
    mocks.onboarding.mockReturnValue({ data: undefined, isLoading: true, isError: false })

    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Thiết lập sở thích' })).toHaveAttribute(
      'href',
      '/onboarding?mode=edit',
    )
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
      isDirectMessageNotificationEnabled: true,
    })
    expect(mocks.toast).toHaveBeenCalledWith(
      'Tùy chọn thông báo đã được cập nhật',
      'success',
    )
  })

  it('lets the reader undo mute and block controls', async () => {
    mocks.unmute.mockImplementation((_value, options) => options.onSuccess())
    mocks.unblock.mockImplementation((_value, options) => options.onSuccess())
    mocks.safety.mockReturnValue({
      data: {
        items: [
          {
            user: { id: 'reader-2', displayName: 'Hà Linh', role: 'USER' },
            isBlocked: true,
            isMuted: true,
            blockedAt: '2026-08-02T08:00:00Z',
            mutedAt: '2026-08-02T07:00:00Z',
          },
        ],
        page: 1,
        pageSize: 100,
        totalItems: 1,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
    })
    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Hà Linh')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Bỏ ẩn' }))
    await user.click(screen.getByRole('button', { name: 'Bỏ chặn' }))

    expect(mocks.unmute).toHaveBeenCalledOnce()
    expect(mocks.unblock).toHaveBeenCalledOnce()
    expect(mocks.toast).toHaveBeenCalledWith('Đã hiển thị lại nội dung', 'success')
    expect(mocks.toast).toHaveBeenCalledWith('Đã bỏ chặn người đọc', 'success')
  })
})
