import { ArrowClockwise, ArrowUp, ChatCircleDots, PaperPlaneTilt } from '@phosphor-icons/react'
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { useClubChat, type ClubChatConnectionStatus } from '../../hooks/useClubChat'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import type { ClubChatMessage } from '../../types/domain'
import { Avatar } from '../ui/Avatar'
import { Button } from '../ui/Button'
import { ErrorState } from '../ui/States'
import { ReportContentButton } from '../moderation/ReportContentButton'
import { MuteUserButton } from '../community/UserSafetyActions'

const MESSAGE_MAX_LENGTH = 2000
const BOTTOM_THRESHOLD = 56

const connectionLabels: Record<ClubChatConnectionStatus, string> = {
  connecting: 'Đang kết nối',
  connected: 'Trực tuyến',
  reconnecting: 'Đang kết nối lại',
  disconnected: 'Mất kết nối',
}

function MessageRow({ message, ownMessage }: { message: ClubChatMessage; ownMessage: boolean }) {
  return (
    <article className={`flex items-end gap-2.5 ${ownMessage ? 'justify-end' : ''}`}>
      {!ownMessage ? (
        <Avatar src={message.sender.avatarUrl} name={message.sender.displayName} size="sm" />
      ) : null}
      <div className={`max-w-[min(34rem,82%)] ${ownMessage ? 'text-right' : ''}`}>
        {!ownMessage ? (
          <p className="mb-1 px-1 text-xs font-semibold text-heading">{message.sender.displayName}</p>
        ) : null}
        <div
          className={`rounded-2xl px-3.5 py-2.5 text-left text-sm leading-6 ${
            ownMessage ? 'rounded-br-md bg-accent text-white' : 'rounded-bl-md bg-surface-muted text-body'
          }`}
        >
          <p className="whitespace-pre-wrap break-words">{message.content}</p>
        </div>
        <div className={`mt-1 flex items-center gap-1 px-1 ${ownMessage ? 'justify-end' : ''}`}>
          <p className="text-[11px] text-muted">{formatRelativeTime(message.createdAt)}</p>
          <ReportContentButton
            targetType="CLUB_CHAT_MESSAGE"
            targetId={message.id}
            ownerId={message.sender.id}
            label="Báo cáo tin nhắn"
            compact
          />
          <MuteUserButton
            targetId={message.sender.id}
            displayName={message.sender.displayName}
            compact
          />
        </div>
      </div>
    </article>
  )
}

export function ClubChatPanel({ clubId }: { clubId: string }) {
  const { user } = useAuth()
  const { showToast } = useToast()
  const panelRef = useRef<HTMLElement>(null)
  const logRef = useRef<HTMLDivElement>(null)
  const formRef = useRef<HTMLFormElement>(null)
  const isAtBottomRef = useRef(true)
  const isPanelVisibleRef = useRef(false)
  const initializedScrollRef = useRef(false)
  const previousLatestIdRef = useRef<string | null>(null)
  const latestMessageRef = useRef<ClubChatMessage | null>(null)
  const sendLock = useRef(false)
  const [draft, setDraft] = useState('')
  const [draftError, setDraftError] = useState('')

  const shouldMarkIncomingRead = useCallback(
    () =>
      document.visibilityState === 'visible' &&
      isAtBottomRef.current &&
      isPanelVisibleRef.current,
    [],
  )

  const chat = useClubChat({ clubId, enabled: Boolean(user), shouldMarkIncomingRead })
  const latestMessage = chat.messages.at(-1) ?? null
  latestMessageRef.current = latestMessage
  const markRead = chat.markRead

  const markLatestRead = useCallback(() => {
    const latest = latestMessageRef.current
    if (latest && shouldMarkIncomingRead()) markRead(latest.id)
  }, [markRead, shouldMarkIncomingRead])

  const scrollToBottom = useCallback((behavior: ScrollBehavior = 'smooth') => {
    const log = logRef.current
    if (!log) return
    log.scrollTo({ top: log.scrollHeight, behavior })
    isAtBottomRef.current = true
  }, [])

  useLayoutEffect(() => {
    if (!latestMessage) return
    const isInitialHistory = !initializedScrollRef.current
    const hasNewLatestMessage = previousLatestIdRef.current !== latestMessage.id

    if (isInitialHistory) {
      scrollToBottom('auto')
      initializedScrollRef.current = true
      markLatestRead()
    } else if (hasNewLatestMessage && (isAtBottomRef.current || latestMessage.sender.id === user?.id)) {
      scrollToBottom()
      markLatestRead()
    }
    previousLatestIdRef.current = latestMessage.id
  }, [latestMessage, markLatestRead, scrollToBottom, user?.id])

  useEffect(() => {
    const panel = panelRef.current
    if (!panel) return
    if (!('IntersectionObserver' in window)) {
      isPanelVisibleRef.current = true
      markLatestRead()
      return
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        isPanelVisibleRef.current = entry.isIntersecting
        if (entry.isIntersecting) markLatestRead()
      },
      { threshold: 0.25 },
    )
    observer.observe(panel)
    return () => observer.disconnect()
  }, [markLatestRead])

  useEffect(() => {
    const markWhenVisible = () => {
      if (document.visibilityState === 'visible') markLatestRead()
    }
    document.addEventListener('visibilitychange', markWhenVisible)
    return () => document.removeEventListener('visibilitychange', markWhenVisible)
  }, [markLatestRead])

  const handleScroll = () => {
    const log = logRef.current
    if (!log) return
    const wasAtBottom = isAtBottomRef.current
    isAtBottomRef.current =
      log.scrollHeight - log.scrollTop - log.clientHeight <= BOTTOM_THRESHOLD
    if (!wasAtBottom && isAtBottomRef.current) markLatestRead()
  }

  const loadOlder = async () => {
    const log = logRef.current
    const previousHeight = log?.scrollHeight ?? 0
    await chat.loadOlderMessages()
    window.requestAnimationFrame(() => {
      if (log) log.scrollTop += log.scrollHeight - previousHeight
    })
  }

  const submitMessage = async (event: FormEvent) => {
    event.preventDefault()
    const content = draft.trim()
    if (!content) {
      setDraftError('Hãy nhập nội dung tin nhắn.')
      return
    }
    if (content.length > MESSAGE_MAX_LENGTH) {
      setDraftError(`Tin nhắn không được vượt quá ${MESSAGE_MAX_LENGTH.toLocaleString('vi-VN')} ký tự.`)
      return
    }
    if (sendLock.current) return

    sendLock.current = true
    setDraftError('')
    try {
      await chat.sendMessage(content)
      setDraft('')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gửi tin nhắn.'), 'error')
    } finally {
      sendLock.current = false
    }
  }

  const handleComposerKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (
      event.key === 'Enter' &&
      !event.shiftKey &&
      !event.nativeEvent.isComposing
    ) {
      event.preventDefault()
      formRef.current?.requestSubmit()
    }
  }

  const showNewMessageButton = chat.unreadCount > 0 && !isAtBottomRef.current

  return (
    <section ref={panelRef} className="surface overflow-hidden" aria-labelledby="club-chat-title">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-5 py-4 sm:px-6">
        <div>
          <p className="eyebrow">Đang diễn ra</p>
          <h2 id="club-chat-title" className="mt-1 flex items-center gap-2 text-xl font-bold text-heading">
            <ChatCircleDots size={22} weight="duotone" className="text-accent-strong" />
            Trò chuyện trực tiếp
          </h2>
        </div>
        <div className="flex items-center gap-2">
          {chat.unreadCount ? (
            <span className="rounded-full bg-accent px-2.5 py-1 text-xs font-bold text-white">
              {chat.unreadCount > 99 ? '99+' : chat.unreadCount} mới
            </span>
          ) : null}
          <span className="inline-flex items-center gap-2 text-xs font-semibold text-muted" aria-live="polite">
            <span
              className={`h-2 w-2 rounded-full ${
                chat.connectionStatus === 'connected'
                  ? 'bg-emerald-500'
                  : chat.connectionStatus === 'reconnecting' || chat.connectionStatus === 'connecting'
                    ? 'animate-pulse bg-amber-500'
                    : 'bg-red-500'
              }`}
              aria-hidden
            />
            {connectionLabels[chat.connectionStatus]}
          </span>
        </div>
      </header>

      {chat.connectionStatus === 'disconnected' ? (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-amber-50 px-5 py-3 text-xs text-amber-900 dark:bg-amber-950/25 dark:text-amber-200">
          <p>Tin nhắn cũ vẫn đọc được. Kết nối lại để nhận tin mới ngay lập tức.</p>
          <Button
            variant="ghost"
            size="sm"
            icon={<ArrowClockwise size={15} />}
            onClick={chat.retryConnection}
          >
            Thử lại
          </Button>
        </div>
      ) : null}

      <div className="relative">
        <div
          ref={logRef}
          role="log"
          aria-live="polite"
          aria-label="Tin nhắn câu lạc bộ"
          aria-busy={chat.isLoading}
          className="h-[30rem] space-y-4 overflow-y-auto bg-page/45 p-4 sm:p-5"
          onScroll={handleScroll}
        >
          {chat.hasOlderMessages ? (
            <div className="flex justify-center pb-1">
              <Button
                variant="ghost"
                size="sm"
                loading={chat.isLoadingOlderMessages}
                onClick={() => void loadOlder()}
              >
                Tải tin nhắn cũ hơn
              </Button>
            </div>
          ) : null}

          {chat.isLoading ? (
            <div className="space-y-4" aria-label="Đang tải tin nhắn">
              {[0, 1, 2, 3].map((item) => (
                <div key={item} className={`flex animate-pulse ${item % 2 ? 'justify-end' : ''}`}>
                  <div className="h-16 w-3/5 rounded-2xl bg-surface-muted" />
                </div>
              ))}
            </div>
          ) : chat.isError ? (
            <ErrorState message="Không thể tải lịch sử trò chuyện." retry={() => void chat.refetch()} />
          ) : chat.messages.length ? (
            chat.messages.map((message) => (
              <MessageRow key={message.id} message={message} ownMessage={message.sender.id === user?.id} />
            ))
          ) : (
            <div className="grid h-full place-items-center text-center">
              <div>
                <ChatCircleDots size={32} weight="duotone" className="mx-auto text-accent-strong" />
                <p className="mt-3 font-semibold text-heading">Chưa có tin nhắn</p>
                <p className="mt-1 text-sm text-muted">Hãy gửi lời chào đầu tiên đến câu lạc bộ.</p>
              </div>
            </div>
          )}
        </div>

        {showNewMessageButton ? (
          <button
            type="button"
            className="absolute bottom-4 left-1/2 inline-flex -translate-x-1/2 items-center gap-2 rounded-full bg-accent px-4 py-2 text-xs font-bold text-white shadow-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
            onClick={() => {
              scrollToBottom()
              markLatestRead()
            }}
          >
            <ArrowUp size={15} />
            {chat.unreadCount} tin nhắn mới
          </button>
        ) : null}
      </div>

      <form ref={formRef} onSubmit={submitMessage} className="border-t border-border p-4 sm:p-5" noValidate>
        <label htmlFor={`club-chat-message-${clubId}`} className="sr-only">
          Tin nhắn mới
        </label>
        <div className="flex items-end gap-2">
          <textarea
            id={`club-chat-message-${clubId}`}
            value={draft}
            maxLength={MESSAGE_MAX_LENGTH}
            rows={2}
            className={`input min-h-12 flex-1 resize-none ${draftError ? 'input-error' : ''}`}
            placeholder="Nhắn điều gì đó với câu lạc bộ…"
            aria-invalid={Boolean(draftError)}
            onChange={(event) => {
              setDraft(event.target.value)
              if (draftError) setDraftError('')
            }}
            onKeyDown={handleComposerKeyDown}
          />
          <Button
            type="submit"
            size="lg"
            loading={chat.isSending}
            icon={<PaperPlaneTilt size={18} weight="fill" />}
            aria-label="Gửi tin nhắn"
          >
            Gửi
          </Button>
        </div>
        <div className="mt-2 flex items-start justify-between gap-3 text-xs">
          {draftError ? (
            <p className="field-error mt-0" role="alert">
              {draftError}
            </p>
          ) : (
            <p className="text-muted">Enter để gửi · Shift + Enter để xuống dòng</p>
          )}
          <span className="shrink-0 text-muted">
            {draft.length}/{MESSAGE_MAX_LENGTH}
          </span>
        </div>
        {chat.isUnreadError ? (
          <p className="mt-2 text-xs text-amber-700 dark:text-amber-400">
            Chưa thể đồng bộ trạng thái đã đọc.
          </p>
        ) : null}
      </form>
    </section>
  )
}
