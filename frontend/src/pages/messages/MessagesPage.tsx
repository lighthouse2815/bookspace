import {
  ArrowClockwise,
  ArrowLeft,
  ChatCircleDots,
  PaperPlaneTilt,
  Users,
} from '@phosphor-icons/react'
import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { Link, useParams } from 'react-router-dom'
import { MuteUserButton } from '../../components/community/UserSafetyActions'
import { ReportContentButton } from '../../components/moderation/ReportContentButton'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { EmptyState, ErrorState } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  flattenConversations,
  useConversation,
  useConversationInbox,
  useDirectMessageThread,
} from '../../hooks/useDirectMessages'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import type { Conversation, DirectMessage } from '../../types/domain'

const MESSAGE_MAX_LENGTH = 2000

const connectionLabels = {
  connecting: 'Đang kết nối',
  connected: 'Trực tuyến',
  reconnecting: 'Đang kết nối lại',
  disconnected: 'Mất kết nối',
} as const

export function MessagesPage() {
  const { conversationId } = useParams()
  const inbox = useConversationInbox()
  const conversations = useMemo(() => flattenConversations(inbox.data), [inbox.data])

  return (
    <div className="container-page section-space">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="eyebrow">Kết nối riêng tư</p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-heading">Tin nhắn</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
            Trò chuyện trực tiếp với những độc giả đang theo dõi bạn và được bạn theo dõi lại.
          </p>
        </div>
        <Link to="/people" className="button button-secondary button-md">
          <Users size={18} /> Tìm độc giả
        </Link>
      </div>

      <section className="surface grid min-h-[38rem] overflow-hidden lg:grid-cols-[21rem_minmax(0,1fr)]">
        <aside
          className={`${conversationId ? 'hidden lg:block' : 'block'} border-r border-border bg-page/35`}
          aria-label="Danh sách cuộc trò chuyện"
        >
          <div className="border-b border-border px-5 py-4">
            <h2 className="font-bold text-heading">Hộp thư</h2>
            <p className="mt-1 text-xs text-muted">
              {conversations.length ? `${conversations.length} cuộc trò chuyện` : 'Chưa có hội thoại'}
            </p>
          </div>
          <div className="max-h-[42rem] overflow-y-auto p-2">
            {inbox.isLoading ? (
              <div className="space-y-2 p-2" aria-label="Đang tải hộp thư">
                {[0, 1, 2, 3].map((item) => (
                  <div key={item} className="h-20 animate-pulse rounded-2xl bg-surface-muted" />
                ))}
              </div>
            ) : inbox.isError ? (
              <div className="p-3">
                <ErrorState message="Không thể tải hộp thư." retry={() => void inbox.refetch()} />
              </div>
            ) : conversations.length ? (
              <div className="space-y-1">
                {conversations.map((conversation) => (
                  <ConversationRow
                    key={conversation.id}
                    conversation={conversation}
                    active={conversation.id === conversationId}
                  />
                ))}
                {inbox.hasNextPage ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="mt-3 w-full"
                    loading={inbox.isFetchingNextPage}
                    onClick={() => void inbox.fetchNextPage()}
                  >
                    Xem thêm hội thoại
                  </Button>
                ) : null}
              </div>
            ) : (
              <div className="p-3">
                <EmptyState
                  title="Hộp thư đang trống"
                  description="Khi hai người theo dõi lẫn nhau, bạn có thể bắt đầu trò chuyện từ trang hồ sơ."
                  icon={ChatCircleDots}
                  action={
                    <Link to="/people" className="button button-primary button-sm">
                      Khám phá độc giả
                    </Link>
                  }
                />
              </div>
            )}
          </div>
        </aside>

        {conversationId ? (
          <ConversationThread conversationId={conversationId} />
        ) : (
          <div className="hidden place-items-center p-8 text-center lg:grid">
            <div className="max-w-sm">
              <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
                <ChatCircleDots size={32} weight="duotone" />
              </div>
              <h2 className="mt-5 text-xl font-bold text-heading">Chọn một cuộc trò chuyện</h2>
              <p className="mt-2 text-sm leading-6 text-muted">
                Tin nhắn được lưu trên BookSpace và đồng bộ theo thời gian thực khi bạn đang trực tuyến.
              </p>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}

function ConversationRow({
  conversation,
  active,
}: {
  conversation: Conversation
  active: boolean
}) {
  return (
    <Link
      to={`/messages/${conversation.id}`}
      className={`flex gap-3 rounded-2xl px-3 py-3 transition-colors focus-visible:focus-ring ${
        active ? 'bg-accent-soft' : 'hover:bg-surface-muted'
      }`}
      aria-current={active ? 'page' : undefined}
    >
      <Avatar
        src={conversation.otherParticipant.avatarUrl}
        name={conversation.otherParticipant.displayName}
        size="md"
      />
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <p className="truncate text-sm font-bold text-heading">
            {conversation.otherParticipant.displayName}
          </p>
          <span className="shrink-0 text-[11px] text-muted">
            {formatRelativeTime(conversation.lastActivityAt)}
          </span>
        </div>
        <div className="mt-1 flex items-center gap-2">
          <p className="min-w-0 flex-1 truncate text-xs text-muted">
            {conversation.lastMessage?.content ?? 'Chưa có tin nhắn'}
          </p>
          {conversation.unreadCount ? (
            <span className="grid min-h-5 min-w-5 place-items-center rounded-full bg-accent px-1.5 text-[10px] font-bold text-white">
              {conversation.unreadCount > 99 ? '99+' : conversation.unreadCount}
            </span>
          ) : null}
        </div>
      </div>
    </Link>
  )
}

function ConversationThread({ conversationId }: { conversationId: string }) {
  const { user } = useAuth()
  const { showToast } = useToast()
  const conversation = useConversation(conversationId)
  const thread = useDirectMessageThread(conversationId)
  const logRef = useRef<HTMLDivElement>(null)
  const formRef = useRef<HTMLFormElement>(null)
  const lastReadRequestRef = useRef<string | null>(null)
  const previousLatestIdRef = useRef<string | null>(null)
  const sendLock = useRef(false)
  const [draft, setDraft] = useState('')
  const [draftError, setDraftError] = useState('')
  const latestMessage = thread.messages.at(-1) ?? null

  useEffect(() => {
    lastReadRequestRef.current = null
    previousLatestIdRef.current = null
  }, [conversationId])

  useEffect(() => {
    if (!latestMessage || document.visibilityState !== 'visible') return
    if (lastReadRequestRef.current === latestMessage.id) return
    lastReadRequestRef.current = latestMessage.id
    thread.markRead(latestMessage.id)
  }, [latestMessage, thread])

  useLayoutEffect(() => {
    if (!latestMessage || previousLatestIdRef.current === latestMessage.id) return
    const log = logRef.current
    if (log && typeof log.scrollTo === 'function') {
      log.scrollTo({ top: log.scrollHeight, behavior: 'smooth' })
    }
    previousLatestIdRef.current = latestMessage.id
  }, [latestMessage])

  const submitMessage = async (event: FormEvent) => {
    event.preventDefault()
    const content = draft.trim()
    if (!content) {
      setDraftError('Hãy nhập nội dung tin nhắn.')
      return
    }
    if (content.length > MESSAGE_MAX_LENGTH) {
      setDraftError('Tin nhắn không được vượt quá 2.000 ký tự.')
      return
    }
    if (sendLock.current || !conversation.data?.canSend) return

    sendLock.current = true
    setDraftError('')
    try {
      await thread.sendMessage(content)
      setDraft('')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể gửi tin nhắn.'), 'error')
    } finally {
      sendLock.current = false
    }
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      formRef.current?.requestSubmit()
    }
  }

  if (conversation.isLoading) {
    return <div className="m-5 animate-pulse rounded-2xl bg-surface-muted" />
  }
  if (conversation.isError || !conversation.data) {
    return (
      <div className="p-6">
        <ErrorState
          message="Không thể mở cuộc trò chuyện này."
          retry={() => void conversation.refetch()}
        />
      </div>
    )
  }

  const participant = conversation.data.otherParticipant
  return (
    <div className="grid min-w-0 grid-rows-[auto_minmax(0,1fr)_auto]">
      <header className="flex items-center justify-between gap-3 border-b border-border px-4 py-3 sm:px-5">
        <div className="flex min-w-0 items-center gap-3">
          <Link to="/messages" className="icon-button lg:hidden" aria-label="Quay lại hộp thư">
            <ArrowLeft size={18} />
          </Link>
          <Avatar src={participant.avatarUrl} name={participant.displayName} size="md" />
          <div className="min-w-0">
            <Link
              to={`/users/${participant.id}`}
              className="block truncate font-bold text-heading hover:text-accent-strong"
            >
              {participant.displayName}
            </Link>
            <p className="mt-0.5 flex items-center gap-1.5 text-xs text-muted" aria-live="polite">
              <span
                className={`h-1.5 w-1.5 rounded-full ${
                  thread.connectionStatus === 'connected'
                    ? 'bg-emerald-500'
                    : thread.connectionStatus === 'connecting' || thread.connectionStatus === 'reconnecting'
                      ? 'animate-pulse bg-amber-500'
                      : 'bg-red-500'
                }`}
              />
              {connectionLabels[thread.connectionStatus]}
            </p>
          </div>
        </div>
        {thread.connectionStatus === 'disconnected' ? (
          <Button
            variant="ghost"
            size="sm"
            icon={<ArrowClockwise size={15} />}
            onClick={thread.retryConnection}
          >
            Kết nối lại
          </Button>
        ) : null}
      </header>

      <div
        ref={logRef}
        role="log"
        aria-live="polite"
        aria-label={`Tin nhắn với ${participant.displayName}`}
        className="h-[34rem] space-y-4 overflow-y-auto bg-page/35 p-4 sm:p-5"
      >
        {thread.hasOlderMessages ? (
          <div className="flex justify-center">
            <Button
              variant="ghost"
              size="sm"
              loading={thread.isLoadingOlderMessages}
              onClick={() => void thread.loadOlderMessages()}
            >
              Tải tin nhắn cũ hơn
            </Button>
          </div>
        ) : null}
        {thread.isLoading ? (
          <div className="space-y-4" aria-label="Đang tải tin nhắn">
            {[0, 1, 2].map((item) => (
              <div key={item} className={`flex animate-pulse ${item % 2 ? 'justify-end' : ''}`}>
                <div className="h-16 w-3/5 rounded-2xl bg-surface-muted" />
              </div>
            ))}
          </div>
        ) : thread.isError ? (
          <ErrorState message="Không thể tải lịch sử tin nhắn." retry={() => void thread.refetch()} />
        ) : thread.messages.length ? (
          thread.messages.map((message) => (
            <MessageRow
              key={message.id}
              message={message}
              ownMessage={message.sender.id === user?.id}
            />
          ))
        ) : (
          <div className="grid h-full place-items-center text-center">
            <div>
              <ChatCircleDots size={34} weight="duotone" className="mx-auto text-accent-strong" />
              <p className="mt-3 font-semibold text-heading">Bắt đầu câu chuyện</p>
              <p className="mt-1 text-sm text-muted">Gửi một lời chào về cuốn sách hai bạn cùng quan tâm.</p>
            </div>
          </div>
        )}
      </div>

      {conversation.data.canSend ? (
        <form ref={formRef} onSubmit={submitMessage} className="border-t border-border p-4" noValidate>
          <div className="flex items-end gap-2">
            <textarea
              value={draft}
              maxLength={MESSAGE_MAX_LENGTH}
              rows={2}
              className={`input min-h-12 flex-1 resize-none ${draftError ? 'input-error' : ''}`}
              placeholder={`Nhắn cho ${participant.displayName}…`}
              aria-label={`Nhắn cho ${participant.displayName}`}
              aria-invalid={Boolean(draftError)}
              onChange={(event) => {
                setDraft(event.target.value)
                if (draftError) setDraftError('')
              }}
              onKeyDown={handleKeyDown}
            />
            <Button
              type="submit"
              size="lg"
              loading={thread.isSending}
              icon={<PaperPlaneTilt size={18} weight="fill" />}
              aria-label="Gửi tin nhắn"
            >
              Gửi
            </Button>
          </div>
          <div className="mt-2 flex justify-between gap-3 text-xs">
            {draftError ? (
              <p className="field-error mt-0" role="alert">{draftError}</p>
            ) : (
              <p className="text-muted">Enter để gửi · Shift + Enter để xuống dòng</p>
            )}
            <span className="shrink-0 text-muted">{draft.length}/{MESSAGE_MAX_LENGTH}</span>
          </div>
        </form>
      ) : (
        <div className="border-t border-border bg-surface-muted/60 px-5 py-4 text-sm text-muted">
          Hai người cần theo dõi lẫn nhau để tiếp tục nhắn tin.
        </div>
      )}
    </div>
  )
}

function MessageRow({ message, ownMessage }: { message: DirectMessage; ownMessage: boolean }) {
  return (
    <article className={`flex items-end gap-2.5 ${ownMessage ? 'justify-end' : ''}`}>
      {!ownMessage ? (
        <Avatar src={message.sender.avatarUrl} name={message.sender.displayName} size="sm" />
      ) : null}
      <div className={`max-w-[min(34rem,82%)] ${ownMessage ? 'text-right' : ''}`}>
        <div
          className={`rounded-2xl px-3.5 py-2.5 text-left text-sm leading-6 ${
            ownMessage
              ? 'rounded-br-md bg-accent text-white'
              : 'rounded-bl-md bg-surface-muted text-body'
          }`}
        >
          <p className="whitespace-pre-wrap break-words">{message.content}</p>
        </div>
        <div className={`mt-1 flex items-center gap-1 px-1 ${ownMessage ? 'justify-end' : ''}`}>
          <p className="text-[11px] text-muted">{formatRelativeTime(message.createdAt)}</p>
          {!ownMessage ? (
            <>
              <ReportContentButton
                targetType="DIRECT_MESSAGE"
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
            </>
          ) : null}
        </div>
      </div>
    </article>
  )
}
