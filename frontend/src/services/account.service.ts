import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type { Dashboard, Notification } from '../types/domain'

export const accountService = {
  dashboard: async () => unwrap(await api.get<ApiEnvelope<Dashboard>>('/dashboard')),

  notifications: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Notification>>>('/notifications', {
        params: { page: 1, pageSize: 50 },
      }),
    ),

  readNotification: async (id: string) =>
    unwrap(await api.patch<ApiEnvelope<Notification>>(`/notifications/${id}/read`)),

  readAllNotifications: async () =>
    unwrap(await api.patch<ApiEnvelope<null>>('/notifications/read-all')),
}
