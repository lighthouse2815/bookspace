import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { accountService } from '../services/account.service'
import type { PageResult } from '../types/api'
import type {
  Notification,
  NotificationCategory,
  NotificationPreferences,
} from '../types/domain'

export const notificationKeys = {
  all: ['notifications'] as const,
  scope: (scope: string) => [...notificationKeys.all, scope] as const,
  lists: (scope: string) => [...notificationKeys.scope(scope), 'list'] as const,
  list: (
    scope: string,
    unreadOnly: boolean,
    category: NotificationCategory | undefined,
    page: number,
    pageSize: number,
  ) =>
    [
      ...notificationKeys.lists(scope),
      unreadOnly ? 'UNREAD' : 'ALL',
      category ?? 'ALL',
      page,
      pageSize,
    ] as const,
  unreadCounts: (scope: string) => [...notificationKeys.scope(scope), 'unread-count'] as const,
  unreadCount: (scope: string, category?: NotificationCategory) =>
    [...notificationKeys.unreadCounts(scope), category ?? 'ALL'] as const,
  preferences: (scope: string) => [...notificationKeys.scope(scope), 'preferences'] as const,
}

const notificationScope = (userId?: string | null) => userId ?? 'guest'

const categoryForType = (type: Notification['type']): NotificationCategory => {
  if (type === 'REVIEW_LIKE' || type === 'COMMENT') return 'REVIEW'
  return type
}

export function useNotifications({
  unreadOnly = false,
  category,
  page = 1,
  pageSize = 20,
}: {
  unreadOnly?: boolean
  category?: NotificationCategory
  page?: number
  pageSize?: number
} = {}) {
  const { user, isLoading } = useAuth()
  const scope = notificationScope(user?.id)
  return useQuery({
    queryKey: notificationKeys.list(scope, unreadOnly, category, page, pageSize),
    queryFn: () => accountService.notifications({ unreadOnly, category, page, pageSize }),
    enabled: Boolean(user) && !isLoading,
  })
}

export function useUnreadNotificationCount(category?: NotificationCategory) {
  const { user, isLoading } = useAuth()
  const scope = notificationScope(user?.id)
  return useQuery({
    queryKey: notificationKeys.unreadCount(scope, category),
    queryFn: () => accountService.unreadNotificationCount(category),
    enabled: Boolean(user) && !isLoading,
  })
}

export function useMarkNotificationRead() {
  const { user } = useAuth()
  const scope = notificationScope(user?.id)
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (notification: Notification) =>
      accountService.readNotification(notification.id),
    onMutate: async (notification) => {
      await queryClient.cancelQueries({ queryKey: notificationKeys.scope(scope) })
      const pages = queryClient.getQueriesData<PageResult<Notification>>({
        queryKey: notificationKeys.lists(scope),
      })
      const counts = queryClient.getQueriesData<{ count: number }>({
        queryKey: notificationKeys.unreadCounts(scope),
      })

      for (const [key, page] of pages) {
        if (!page) continue
        const containsNotification = page.items.some((item) => item.id === notification.id)
        if (!containsNotification) continue
        const unreadOnly = key[3] === 'UNREAD'
        queryClient.setQueryData<PageResult<Notification>>(key, {
          ...page,
          items: unreadOnly
            ? page.items.filter((item) => item.id !== notification.id)
            : page.items.map((item) =>
                item.id === notification.id ? { ...item, isRead: true } : item,
              ),
          totalItems: unreadOnly ? Math.max(0, page.totalItems - 1) : page.totalItems,
          totalPages: unreadOnly
            ? Math.ceil(Math.max(0, page.totalItems - 1) / page.pageSize)
            : page.totalPages,
        })
      }

      const category = categoryForType(notification.type)
      for (const [key, count] of counts) {
        if (!count) continue
        const cachedCategory = key[key.length - 1]
        if (cachedCategory === 'ALL' || cachedCategory === category) {
          queryClient.setQueryData(key, { count: Math.max(0, count.count - 1) })
        }
      }

      return { pages, counts }
    },
    onError: (_error, _notification, context) => {
      for (const [key, page] of context?.pages ?? []) queryClient.setQueryData(key, page)
      for (const [key, count] of context?.counts ?? []) queryClient.setQueryData(key, count)
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: notificationKeys.lists(scope) }),
        queryClient.invalidateQueries({ queryKey: notificationKeys.unreadCounts(scope) }),
      ])
    },
  })
}

export function useMarkAllNotificationsRead() {
  const { user } = useAuth()
  const scope = notificationScope(user?.id)
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: accountService.readAllNotifications,
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: notificationKeys.scope(scope) })
      const pages = queryClient.getQueriesData<PageResult<Notification>>({
        queryKey: notificationKeys.lists(scope),
      })
      const counts = queryClient.getQueriesData<{ count: number }>({
        queryKey: notificationKeys.unreadCounts(scope),
      })

      for (const [key, page] of pages) {
        if (!page) continue
        const unreadOnly = key[3] === 'UNREAD'
        queryClient.setQueryData<PageResult<Notification>>(key, {
          ...page,
          items: unreadOnly ? [] : page.items.map((item) => ({ ...item, isRead: true })),
          totalItems: unreadOnly ? 0 : page.totalItems,
          totalPages: unreadOnly ? 0 : page.totalPages,
        })
      }
      for (const [key] of counts) queryClient.setQueryData(key, { count: 0 })

      return { pages, counts }
    },
    onError: (_error, _variables, context) => {
      for (const [key, page] of context?.pages ?? []) queryClient.setQueryData(key, page)
      for (const [key, count] of context?.counts ?? []) queryClient.setQueryData(key, count)
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: notificationKeys.lists(scope) }),
        queryClient.invalidateQueries({ queryKey: notificationKeys.unreadCounts(scope) }),
      ])
    },
  })
}

export function useNotificationPreferences() {
  const { user, isLoading } = useAuth()
  const scope = notificationScope(user?.id)
  return useQuery({
    queryKey: notificationKeys.preferences(scope),
    queryFn: accountService.notificationPreferences,
    enabled: Boolean(user) && !isLoading,
  })
}

export function useUpdateNotificationPreferences() {
  const { user } = useAuth()
  const scope = notificationScope(user?.id)
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (preferences: NotificationPreferences) =>
      accountService.updateNotificationPreferences(preferences),
    onSuccess: (preferences) => {
      queryClient.setQueryData(notificationKeys.preferences(scope), preferences)
    },
  })
}
