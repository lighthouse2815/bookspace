import {
  Bell,
  ChatCircle,
  Check,
  Heart,
  Info,
  UserPlus,
  UsersThree,
} from '@phosphor-icons/react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useNotifications } from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import { accountService } from '../../services/account.service'
import type { Notification } from '../../types/domain'

const notificationIcons = {
  FOLLOW: UserPlus,
  REVIEW_LIKE: Heart,
  COMMENT: ChatCircle,
  CLUB: UsersThree,
  CHALLENGE: Check,
  SYSTEM: Info,
}

export function NotificationsPage() {
  const notifications = useNotifications()
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const readOne = useMutation({
    mutationFn: accountService.readNotification,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
    onError: (error) => showToast(errorMessage(error), 'error'),
  })
  const readAll = useMutation({
    mutationFn: accountService.readAllNotifications,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
      showToast('Đã đánh dấu tất cả là đã đọc', 'success')
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const markRead = (notification: Notification) => {
    if (!notification.isRead) readOne.mutate(notification.id)
  }

  const unreadCount = notifications.data?.items.filter((item) => !item.isRead).length ?? 0

  return (
    <div className="container-page section-space max-w-4xl">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="eyebrow">Cập nhật dành cho bạn</p>
          <h1 className="page-title mt-4">Thông báo</h1>
          <p className="mt-3 text-muted">{unreadCount} thông báo chưa đọc</p>
        </div>
        {unreadCount > 0 ? (
          <Button
            variant="secondary"
            loading={readAll.isPending}
            icon={<Check size={17} />}
            onClick={() => readAll.mutate()}
          >
            Đánh dấu tất cả đã đọc
          </Button>
        ) : null}
      </div>

      <div className="mt-9">
        {notifications.isLoading ? (
          <LoadingRows count={6} />
        ) : notifications.isError ? (
          <ErrorState
            message="Không thể tải thông báo."
            retry={() => void notifications.refetch()}
          />
        ) : notifications.data?.items.length ? (
          <div className="space-y-2">
            {notifications.data.items.map((notification) => {
              const Icon = notificationIcons[notification.type]
              const content = (
                <div
                  className={`flex gap-4 rounded-2xl border p-4 transition-colors sm:p-5 ${
                    notification.isRead
                      ? 'border-border bg-surface'
                      : 'border-accent/30 bg-accent-soft/60'
                  }`}
                >
                  {notification.actor ? (
                    <Avatar
                      src={notification.actor.avatarUrl}
                      name={notification.actor.displayName}
                    />
                  ) : (
                    <div className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-surface text-accent-strong">
                      <Icon size={20} weight="duotone" />
                    </div>
                  )}
                  <div className="min-w-0 flex-1">
                    <div className="flex items-start justify-between gap-3">
                      <p className="font-semibold text-heading">{notification.title}</p>
                      {!notification.isRead ? (
                        <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-accent" aria-label="Chưa đọc" />
                      ) : null}
                    </div>
                    <p className="mt-1 text-sm leading-6 text-muted">{notification.message}</p>
                    <p className="mt-2 text-xs text-muted">{formatRelativeTime(notification.createdAt)}</p>
                  </div>
                </div>
              )
              return notification.link ? (
                <Link key={notification.id} to={notification.link} onClick={() => markRead(notification)}>
                  {content}
                </Link>
              ) : (
                <button
                  key={notification.id}
                  type="button"
                  className="block w-full text-left"
                  onClick={() => markRead(notification)}
                >
                  {content}
                </button>
              )
            })}
          </div>
        ) : (
          <EmptyState
            icon={Bell}
            title="Bạn đã xem hết thông báo"
            description="Tương tác mới từ cộng đồng, câu lạc bộ và thử thách sẽ xuất hiện tại đây."
          />
        )}
      </div>
    </div>
  )
}
