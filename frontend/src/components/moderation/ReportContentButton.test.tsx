import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ReportContentButton } from './ReportContentButton'

const mocks = vi.hoisted(() => ({
  create: vi.fn(),
  toast: vi.fn(),
  userId: 'viewer-1',
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: { id: mocks.userId, displayName: 'Người xem', role: 'USER' },
    isAuthenticated: true,
  }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../services/moderation.service', () => ({
  moderationService: { create: mocks.create },
}))

describe('ReportContentButton', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.userId = 'viewer-1'
    mocks.create.mockResolvedValue({ id: 'report-1' })
  })

  it('submits a private report with reason and optional details', async () => {
    const user = userEvent.setup()
    render(
      <ReportContentButton
        targetType="REVIEW"
        targetId="review-1"
        ownerId="author-1"
        label="Báo cáo đánh giá"
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Báo cáo đánh giá nội dung' }))
    expect(screen.getByRole('dialog', { name: 'Báo cáo đánh giá' })).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Lý do báo cáo'), 'HARASSMENT')
    await user.type(
      screen.getByLabelText(/Mô tả thêm/),
      'Nội dung công kích người đọc khác.',
    )
    await user.click(screen.getByRole('button', { name: 'Gửi báo cáo' }))

    await waitFor(() =>
      expect(mocks.create).toHaveBeenCalledWith({
        targetType: 'REVIEW',
        targetId: 'review-1',
        reason: 'HARASSMENT',
        details: 'Nội dung công kích người đọc khác.',
      }),
    )
    expect(mocks.toast).toHaveBeenCalledWith('Đã gửi báo cáo đến đội ngũ quản trị', 'success')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('does not offer reporting on the current user own content', () => {
    mocks.userId = 'author-1'
    render(
      <ReportContentButton
        targetType="REVIEW"
        targetId="review-1"
        ownerId="author-1"
      />,
    )

    expect(screen.queryByRole('button', { name: /Báo cáo/ })).not.toBeInTheDocument()
  })
})
