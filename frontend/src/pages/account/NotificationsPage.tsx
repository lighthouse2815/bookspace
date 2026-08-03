import {
  Bell,
  ChatCircle,
  Check,
  EnvelopeSimple,
  Heart,
  Info,
  UserPlus,
  UsersThree,
} from '@phosphor-icons/react'
import { Link, useSearchParams } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
  useUnreadNotificationCount,
} from '../../hooks/useNotifications'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import type { Notification, NotificationCategory } from '../../types/domain'

const notificationIcons = {
  FOLLOW: UserPlus,
  REVIEW_LIKE: Heart,
  COMMENT: ChatCircle,
  CLUB: UsersThree,
  CHALLENGE: Check,
  DIRECT_MESSAGE: EnvelopeSimple,
  SYSTEM: Info,
}

const categoryOptions: Array<{ value?: NotificationCategory; label: string }> = [
  { label: 'Tất cả loại' },
  { value: 'FOLLOW', label: 'Theo dõi' },
  { value: 'REVIEW', label: 'Đánh giá' },
  { value: 'CLUB', label: 'Câu lạc bộ' },
  { value: 'CHALLENGE', label: 'Thử thách' },
  { value: 'DIRECT_MESSAGE', label: 'Tin nhắn' },
  { value: 'SYSTEM', label: 'Hệ thống' },
]

export function NotificationsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const { showToast } = useToast()
  const unreadOnly = searchParams.get('view') === 'unread'
  const requestedCategory = searchParams.get('category')
  const category = categoryOptions.some((option) => option.value === requestedCategory)
    ? (requestedCategory as NotificationCategory)
    : undefined
  const requestedPage = Number(searchParams.get('page') ?? '1')
  const page = Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1
  const notifications = useNotifications({ unreadOnly, category, page, pageSize: 12 })
  const unreadCount = useUnreadNotificationCount()
  const readOne = useMarkNotificationRead()
  const readAll = useMarkAllNotificationsRead()
  const totalUnread = unreadCount.data?.count ?? 0

  const changeView = (next: {
    unreadOnly?: boolean
    category?: NotificationCategory
    page?: number
  }) => {
    const params = new URLSearchParams()
    if (next.unreadOnly) params.set('view', 'unread')
    if (next.category) params.set('category', next.category)
    if ((next.page ?? 1) > 1) params.set('page', String(next.page))
    setSearchParams(params)
  }

  const markRead = (notification: Notification) => {
    if (notification.isRead || readOne.isPending) return
    readOne.mutate(notification, {
      onError: (error) => showToast(errorMessage(error, 'Không thể đánh dấu đã đọc'), 'error'),
    })
  }

  const markAllRead = () => {
    readAll.mutate(undefined, {
      onSuccess: () => showToast('Đã đánh dấu tất cả là đã đọc', 'success'),
      onError: (error) =>
        showToast(errorMessage(error, 'Không thể đánh dấu tất cả là đã đọc'), 'error'),
    })
  }

  return (
    <div className="container-page section-space max-w-4xl">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="eyebrow">Cập nhật dành cho bạn</p>
          <h1 className="page-title mt-4">Thông báo</h1>
          <p className="mt-3 text-muted" aria-live="polite">
            {unreadCount.isLoading ? 'Đang kiểm tra thông báo mới…' : `${totalUnread} thông báo chưa đọc`}
          </p>
        </div>
        {totalUnread > 0 ? (
          <Button
            variant="secondary"
            loading={readAll.isPending}
            icon={<Check size={17} />}
            onClick={markAllRead}
          >
            Đánh dấu tất cả đã đọc
          </Button>
        ) : null}
      </div>

      <section className="mt-8 rounded-2xl border border-border bg-surface p-2 sm:p-3">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex gap-1" aria-label="Trạng thái thông báo">
            <button
              type="button"
              className={`filter-tab ${!unreadOnly ? 'filter-tab-active' : ''}`}
              aria-current={!unreadOnly ? 'page' : undefined}
              onClick={() => changeView({ category })}
            >
              Tất cả
            </button>
            <button
              type="button"
              className={`filter-tab ${unreadOnly ? 'filter-tab-active' : ''}`}
              aria-current={unreadOnly ? 'page' : undefined}
              onClick={() => changeView({ unreadOnly: true, category })}
            >
              Chưa đọc
            </button>
          </div>
          <div className="flex gap-1 overflow-x-auto" aria-label="Loại thông báo">
            {categoryOptions.map((option) => (
              <button
                key={option.value ?? 'ALL'}
                type="button"
                className={`filter-tab whitespace-nowrap ${category === option.value ? 'filter-tab-active' : ''}`}
                onClick={() => changeView({ unreadOnly, category: option.value })}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      </section>

      <div className="mt-7">
        {notifications.isLoading ? (
          <LoadingRows count={6} />
        ) : notifications.isError ? (
          <ErrorState
            message="Không thể tải thông báo."
            retry={() => void notifications.refetch()}
          />
        ) : notifications.data?.items.length ? (
          <>
            <div className="mb-4 flex items-center justify-between gap-4 text-sm text-muted">
              <span>{notifications.data.totalItems} kết quả</span>
              {notifications.isFetching ? <span>Đang đồng bộ…</span> : null}
            </div>
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
                        <p className="break-words font-semibold text-heading">{notification.title}</p>
                        {!notification.isRead ? (
                          <span
                            className="mt-1 h-2 w-2 shrink-0 rounded-full bg-accent"
                            aria-label="Chưa đọc"
                          />
                        ) : null}
                      </div>
                      <p className="mt-1 break-words text-sm leading-6 text-muted">
                        {notification.message}
                      </p>
                      <p className="mt-2 text-xs text-muted">
                        {formatRelativeTime(notification.createdAt)}
                      </p>
                    </div>
                  </div>
                )
                return notification.link ? (
                  <Link
                    key={notification.id}
                    to={notification.link}
                    onClick={() => markRead(notification)}
                    aria-label={`${notification.title}. Mở nội dung liên quan`}
                  >
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
            <Pagination
              page={notifications.data.page}
              totalPages={notifications.data.totalPages}
              disabled={notifications.isFetching}
              onPageChange={(nextPage) =>
                changeView({ unreadOnly, category, page: nextPage })
              }
              className="mt-8"
            />
          </>
        ) : (
          <EmptyState
            icon={Bell}
            title={unreadOnly ? 'Không còn thông báo chưa đọc' : 'Chưa có thông báo phù hợp'}
            description={
              category
                ? 'Hãy chọn loại khác hoặc quay lại tất cả thông báo.'
                : 'Tương tác mới từ cộng đồng, câu lạc bộ và thử thách sẽ xuất hiện tại đây.'
            }
          />
        )}
      </div>
    </div>
  )
}
