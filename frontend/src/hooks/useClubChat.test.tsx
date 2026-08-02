import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ClubChatMessage, User } from '../types/domain'
import { useClubChat } from './useClubChat'

const reader: User = {
  id: 'reader-1',
  displayName: 'Minh Anh',
  role: 'USER',
}

const friend: User = {
  id: 'reader-2',
  displayName: 'Hà Linh',
  role: 'USER',
}

function message(id: string, content: string, createdAt: string, sender = friend): ClubChatMessage {
  return { id, clubId: 'club-1', sender, content, createdAt }
}

const firstMessage = message('message-1', 'Tin đầu tiên', '2026-08-01T01:00:00Z')
const secondMessage = message('message-2', 'Tin mới hơn', '2026-08-01T01:01:00Z')
const olderMessage = message('message-0', 'Tin cũ hơn', '2026-08-01T00:59:00Z')

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isLoading: false,
  },
  shouldRead: false,
  messages: vi.fn(),
  unread: vi.fn(),
  send: vi.fn(),
  markRead: vi.fn(),
  createConnection: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
  reconnecting: null as (() => void) | null,
  reconnected: null as (() => void) | null,
  closed: null as (() => void) | null,
  start: vi.fn(),
  stop: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    ...mocks.auth,
    isAuthenticated: Boolean(mocks.auth.user),
  }),
}))

vi.mock('../services/club-chat.service', () => ({
  clubChatService: {
    messages: mocks.messages,
    unreadCount: mocks.unread,
    sendMessage: mocks.send,
    markRead: mocks.markRead,
  },
}))

vi.mock('../services/club-chat.connection', () => ({
  CLUB_CHAT_MESSAGE_EVENT: 'ClubChatMessageCreated',
  createClubChatConnection: mocks.createConnection,
}))

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  })
}

function Providers({ client, children }: { client: QueryClient; children: ReactNode }) {
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

function ChatProbe() {
  const chat = useClubChat({
    clubId: 'club-1',
    enabled: true,
    shouldMarkIncomingRead: () => mocks.shouldRead,
  })
  return (
    <>
      <output data-testid="messages">{chat.messages.map((item) => item.content).join('|')}</output>
      <output data-testid="unread">{chat.unreadCount}</output>
      <output data-testid="connection">{chat.connectionStatus}</output>
      <button type="button" onClick={() => void chat.loadOlderMessages()}>
        Tải cũ
      </button>
    </>
  )
}

function renderProbe(client = createQueryClient()) {
  return render(
    <Providers client={client}>
      <ChatProbe />
    </Providers>,
  )
}

describe('useClubChat', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.eventHandlers.clear()
    mocks.reconnecting = null
    mocks.reconnected = null
    mocks.closed = null
    mocks.shouldRead = false
    Object.assign(mocks.auth, { user: reader, isLoading: false })
    mocks.messages.mockImplementation(async (_clubId: string, cursor?: string | null) =>
      cursor
        ? { items: [olderMessage], nextCursor: null, hasMore: false }
        : { items: [secondMessage, firstMessage], nextCursor: 'older-cursor', hasMore: true },
    )
    mocks.unread.mockResolvedValue({
      clubId: 'club-1',
      count: 2,
      lastReadMessageId: null,
      lastReadAt: null,
    })
    mocks.markRead.mockImplementation(async (_clubId: string, lastReadMessageId: string) => ({
      clubId: 'club-1',
      count: 0,
      lastReadMessageId,
      lastReadAt: '2026-08-01T01:05:00Z',
    }))
    mocks.start.mockResolvedValue(undefined)
    mocks.stop.mockResolvedValue(undefined)
    mocks.createConnection.mockImplementation(() => ({
      on: (eventName: string, handler: (payload: unknown) => void) =>
        mocks.eventHandlers.set(eventName, handler),
      off: (eventName: string) => mocks.eventHandlers.delete(eventName),
      onreconnecting: (handler: () => void) => {
        mocks.reconnecting = handler
      },
      onreconnected: (handler: () => void) => {
        mocks.reconnected = handler
      },
      onclose: (handler: () => void) => {
        mocks.closed = handler
      },
      start: mocks.start,
      stop: mocks.stop,
    }))
  })

  it('loads newest-first pages but exposes one chronological, deduplicated transcript', async () => {
    const user = userEvent.setup()
    renderProbe()

    await waitFor(() =>
      expect(screen.getByTestId('messages')).toHaveTextContent('Tin đầu tiên|Tin mới hơn'),
    )
    expect(mocks.messages).toHaveBeenCalledWith('club-1', null)
    await user.click(screen.getByRole('button', { name: 'Tải cũ' }))

    await waitFor(() =>
      expect(screen.getByTestId('messages')).toHaveTextContent(
        'Tin cũ hơn|Tin đầu tiên|Tin mới hơn',
      ),
    )
    expect(mocks.messages).toHaveBeenLastCalledWith('club-1', 'older-cursor')
  })

  it('deduplicates hub echoes and only marks incoming messages read when the panel is readable', async () => {
    renderProbe()
    await waitFor(() => expect(screen.getByTestId('unread')).toHaveTextContent('2'))
    const receive = mocks.eventHandlers.get('ClubChatMessageCreated')
    const thirdMessage = message('message-3', 'Tin trực tiếp', '2026-08-01T01:02:00Z')

    act(() => {
      receive?.(thirdMessage)
      receive?.(thirdMessage)
    })
    await waitFor(() => expect(screen.getByTestId('unread')).toHaveTextContent('3'))
    expect(screen.getByTestId('messages').textContent?.match(/Tin trực tiếp/g)).toHaveLength(1)

    mocks.shouldRead = true
    const fourthMessage = message('message-4', 'Đang nhìn thấy', '2026-08-01T01:03:00Z')
    act(() => receive?.(fourthMessage))

    await waitFor(() => expect(mocks.markRead).toHaveBeenCalledWith('club-1', 'message-4'))
    expect(screen.getByTestId('unread')).toHaveTextContent('0')
  })

  it('refetches history and unread state after SignalR reconnects', async () => {
    renderProbe()
    await waitFor(() => {
      expect(mocks.messages).toHaveBeenCalledOnce()
      expect(mocks.unread).toHaveBeenCalledOnce()
      expect(screen.getByTestId('connection')).toHaveTextContent('connected')
    })

    act(() => mocks.reconnected?.())

    await waitFor(() => {
      expect(mocks.messages).toHaveBeenCalledTimes(2)
      expect(mocks.unread).toHaveBeenCalledTimes(2)
    })
  })

  it('does not fetch or create a connection before authentication is available', async () => {
    Object.assign(mocks.auth, { user: null, isLoading: true })
    const view = renderProbe()

    expect(mocks.messages).not.toHaveBeenCalled()
    expect(mocks.createConnection).not.toHaveBeenCalled()

    Object.assign(mocks.auth, { user: reader, isLoading: false })
    view.rerender(
      <Providers client={createQueryClient()}>
        <ChatProbe />
      </Providers>,
    )
    await waitFor(() => expect(mocks.createConnection).toHaveBeenCalledOnce())
  })
})
