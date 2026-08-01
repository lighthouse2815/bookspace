import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../types/api'
import type { Notification } from '../types/domain'
import { notificationKeys, useMarkNotificationRead } from './useNotifications'

const mocks = vi.hoisted(() => ({
  readNotification: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'reader-1' },
    isLoading: false,
  }),
}))

vi.mock('../services/account.service', () => ({
  accountService: {
    readNotification: (...args: unknown[]) => mocks.readNotification(...args),
  },
}))

const notification: Notification = {
  id: 'notification-1',
  type: 'COMMENT',
  title: 'Bình luận mới',
  message: 'Có bình luận mới.',
  isRead: false,
  createdAt: '2026-08-01T08:00:00Z',
}

describe('notification optimistic cache ownership', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('updates list/count immediately and rolls both back when mark-read fails', async () => {
    let rejectRequest: ((error: Error) => void) | undefined
    mocks.readNotification.mockImplementation(
      () =>
        new Promise((_resolve, reject) => {
          rejectRequest = reject
        }),
    )
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    const listKey = notificationKeys.list('reader-1', false, undefined, 1, 20)
    const countKey = notificationKeys.unreadCount('reader-1')
    client.setQueryData<PageResult<Notification>>(listKey, {
      items: [notification],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    })
    client.setQueryData(countKey, { count: 1 })
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    )
    const { result } = renderHook(() => useMarkNotificationRead(), { wrapper })

    act(() => result.current.mutate(notification))

    await waitFor(() => {
      expect(client.getQueryData<PageResult<Notification>>(listKey)?.items[0].isRead).toBe(true)
      expect(client.getQueryData<{ count: number }>(countKey)?.count).toBe(0)
    })

    act(() => rejectRequest?.(new Error('network failed')))

    await waitFor(() => {
      expect(client.getQueryData<PageResult<Notification>>(listKey)?.items[0].isRead).toBe(false)
      expect(client.getQueryData<{ count: number }>(countKey)?.count).toBe(1)
    })
  })
})
