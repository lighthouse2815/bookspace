import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { NotificationsPage } from './NotificationsPage'

const mocks = vi.hoisted(() => ({
  notifications: vi.fn(),
  unreadCount: vi.fn(),
  readOne: vi.fn(),
  readAll: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useNotifications', () => ({
  useNotifications: (...args: unknown[]) => mocks.notifications(...args),
  useUnreadNotificationCount: (...args: unknown[]) => mocks.unreadCount(...args),
  useMarkNotificationRead: () => ({ mutate: mocks.readOne, isPending: false }),
  useMarkAllNotificationsRead: () => ({ mutate: mocks.readAll, isPending: false }),
}))

const notification = {
  id: 'notification-1',
  type: 'COMMENT' as const,
  title: 'Bình luận mới',
  message: 'Minh Anh đã bình luận đánh giá của bạn.',
  isRead: false,
  createdAt: '2026-08-01T08:00:00Z',
}

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>
}

describe('notification center v2', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.notifications.mockReturnValue({
      data: {
        items: [notification],
        page: 2,
        pageSize: 12,
        totalItems: 25,
        totalPages: 3,
      },
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    })
    mocks.unreadCount.mockReturnValue({ data: { count: 27 }, isLoading: false })
  })

  it('uses server unread count and URL-backed status, category and pagination', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/notifications?view=unread&category=REVIEW&page=2']}>
        <NotificationsPage />
        <LocationProbe />
      </MemoryRouter>,
    )

    expect(mocks.notifications).toHaveBeenCalledWith({
      unreadOnly: true,
      category: 'REVIEW',
      page: 2,
      pageSize: 12,
    })
    expect(screen.getByText('27 thông báo chưa đọc')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Trang sau' }))
    expect(screen.getByTestId('location')).toHaveTextContent(
      '/notifications?view=unread&category=REVIEW&page=3',
    )
  })

  it('marks one item and all items through optimistic mutation hooks', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/notifications']}>
        <NotificationsPage />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: /Bình luận mới/ }))
    expect(mocks.readOne).toHaveBeenCalledWith(notification, expect.any(Object))

    await user.click(screen.getByRole('button', { name: 'Đánh dấu tất cả đã đọc' }))
    expect(mocks.readAll).toHaveBeenCalledWith(undefined, expect.any(Object))
  })

  it('switches category without preserving a stale page', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/notifications?category=REVIEW&page=3']}>
        <NotificationsPage />
        <LocationProbe />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Câu lạc bộ' }))
    expect(screen.getByTestId('location')).toHaveTextContent('/notifications?category=CLUB')
  })
})
