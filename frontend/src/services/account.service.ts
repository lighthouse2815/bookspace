import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  Dashboard,
  Notification,
  NotificationCategory,
  NotificationPreferences,
} from '../types/domain'

export interface NotificationQuery {
  unreadOnly?: boolean
  category?: NotificationCategory
  page?: number
  pageSize?: number
}

export const accountService = {
  dashboard: async () => unwrap(await api.get<ApiEnvelope<Dashboard>>('/dashboard')),

  notifications: async ({ unreadOnly, category, page = 1, pageSize = 20 }: NotificationQuery) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Notification>>>('/notifications', {
        params: { unreadOnly, category, page, pageSize },
      }),
    ),

  unreadNotificationCount: async (category?: NotificationCategory) =>
    unwrap(
      await api.get<ApiEnvelope<{ count: number }>>('/notifications/unread-count', {
        params: { category },
      }),
    ),

  readNotification: async (id: string) =>
    unwrap(await api.patch<ApiEnvelope<Notification>>(`/notifications/${id}/read`)),

  readAllNotifications: async () =>
    unwrap(await api.patch<ApiEnvelope<null>>('/notifications/read-all')),

  notificationPreferences: async () =>
    unwrap(
      await api.get<ApiEnvelope<NotificationPreferences>>('/notifications/preferences'),
    ),

  updateNotificationPreferences: async (input: NotificationPreferences) =>
    unwrap(
      await api.patch<ApiEnvelope<NotificationPreferences>>(
        '/notifications/preferences',
        input,
      ),
    ),
}
