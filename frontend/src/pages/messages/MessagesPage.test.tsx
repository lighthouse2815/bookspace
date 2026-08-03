import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { DirectMessage } from '../../types/domain'
import { MessagesPage } from './MessagesPage'

const participant = {
  id: 'reader-2',
  displayName: 'Hà Linh',
  avatarUrl: undefined,
  role: 'USER' as const,
}

const message: DirectMessage = {
  id: 'message-1',
  conversationId: 'conversation-1',
  sender: participant,
  content: 'Bạn đang đọc cuốn nào?',
  createdAt: '2026-08-04T08:00:00Z',
}

const mocks = vi.hoisted(() => ({
  sendMessage: vi.fn(),
  markRead: vi.fn(),
  toast: vi.fn(),
  canSend: true,
  message: {
    id: 'message-1',
    conversationId: 'conversation-1',
    sender: {
      id: 'reader-2',
      displayName: 'Hà Linh',
      avatarUrl: undefined,
      role: 'USER' as const,
    },
    content: 'Bạn đang đọc cuốn nào?',
    createdAt: '2026-08-04T08:00:00Z',
  },
  conversation: {
    id: 'conversation-1',
    otherParticipant: {
      id: 'reader-2',
      displayName: 'Hà Linh',
      avatarUrl: undefined,
      role: 'USER' as const,
    },
    unreadCount: 1,
    canSend: true,
    lastActivityAt: '2026-08-04T08:00:00Z',
    createdAt: '2026-08-04T07:00:00Z',
  },
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'reader-1', displayName: 'Minh An', role: 'USER' },
    isLoading: false,
  }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useDirectMessages', () => ({
  flattenConversations: () => [
    { ...mocks.conversation, lastMessage: mocks.message, canSend: mocks.canSend },
  ],
  useConversationInbox: () => ({
    data: {
      pages: [{ items: [mocks.conversation], nextCursor: null, hasMore: false }],
    },
    isLoading: false,
    isError: false,
    hasNextPage: false,
    isFetchingNextPage: false,
    fetchNextPage: vi.fn(),
    refetch: vi.fn(),
  }),
  useConversation: () => ({
    data: {
      ...mocks.conversation,
      lastMessage: mocks.message,
      canSend: mocks.canSend,
    },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  }),
  useDirectMessageThread: () => ({
    messages: [mocks.message],
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
    hasOlderMessages: false,
    loadOlderMessages: vi.fn(),
    isLoadingOlderMessages: false,
    sendMessage: mocks.sendMessage,
    isSending: false,
    markRead: mocks.markRead,
    isMarkingRead: false,
    connectionStatus: 'connected',
    retryConnection: vi.fn(),
  }),
}))

vi.mock('../../components/community/UserSafetyActions', () => ({
  MuteUserButton: () => <button type="button">Ẩn người gửi</button>,
}))

vi.mock('../../components/moderation/ReportContentButton', () => ({
  ReportContentButton: ({ targetType }: { targetType: string }) => (
    <button type="button">Báo cáo {targetType}</button>
  ),
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/messages/conversation-1']}>
      <Routes>
        <Route path="/messages/:conversationId" element={<MessagesPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('direct messages page', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.canSend = true
    mocks.sendMessage.mockResolvedValue(message)
  })

  it('renders the selected thread, marks it read and sends trimmed text', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(screen.getByRole('heading', { name: 'Tin nhắn' })).toBeInTheDocument()
    expect(screen.getAllByText('Bạn đang đọc cuốn nào?')).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Báo cáo DIRECT_MESSAGE' })).toBeInTheDocument()
    await waitFor(() => expect(mocks.markRead).toHaveBeenCalledWith('message-1'))

    const composer = screen.getByRole('textbox', { name: 'Nhắn cho Hà Linh' })
    await user.type(composer, '  Mình đang đọc Số đỏ  ')
    await user.click(screen.getByRole('button', { name: 'Gửi tin nhắn' }))
    expect(mocks.sendMessage).toHaveBeenCalledWith('Mình đang đọc Số đỏ')
  })

  it('disables the composer when the mutual follow relationship no longer exists', () => {
    mocks.canSend = false
    renderPage()

    expect(screen.queryByRole('textbox', { name: 'Nhắn cho Hà Linh' })).not.toBeInTheDocument()
    expect(screen.getByText(/cần theo dõi lẫn nhau/)).toBeInTheDocument()
  })
})
