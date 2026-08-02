import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ClubChatMessage, User } from '../../types/domain'
import { ClubChatPanel } from './ClubChatPanel'

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

const firstMessage: ClubChatMessage = {
  id: 'message-1',
  clubId: 'club-1',
  sender: friend,
  content: 'Bạn đã đọc đến đâu rồi?',
  createdAt: '2026-08-01T01:00:00Z',
}

const secondMessage: ClubChatMessage = {
  id: 'message-2',
  clubId: 'club-1',
  sender: reader,
  content: 'Mình vừa xong chương ba.',
  createdAt: '2026-08-01T01:01:00Z',
}

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  send: vi.fn(),
  markRead: vi.fn(),
  loadOlder: vi.fn(),
  refetch: vi.fn(),
  retry: vi.fn(),
  chat: {} as Record<string, unknown>,
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ user: reader, isAuthenticated: true, isLoading: false }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useClubChat', () => ({
  useClubChat: () => mocks.chat,
}))

vi.mock('../community/UserSafetyActions', () => ({
  MuteUserButton: () => null,
}))

function setDefaultChat(overrides: Record<string, unknown> = {}) {
  mocks.chat = {
    messages: [firstMessage, secondMessage],
    isLoading: false,
    isError: false,
    error: null,
    refetch: mocks.refetch,
    hasOlderMessages: false,
    loadOlderMessages: mocks.loadOlder,
    isLoadingOlderMessages: false,
    unreadCount: 0,
    isUnreadError: false,
    markRead: mocks.markRead,
    sendMessage: mocks.send,
    isSending: false,
    connectionStatus: 'connected',
    retryConnection: mocks.retry,
    ...overrides,
  }
}

describe('ClubChatPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setDefaultChat()
    Object.defineProperty(HTMLElement.prototype, 'scrollTo', {
      configurable: true,
      value: vi.fn(),
    })
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        private readonly callback: IntersectionObserverCallback

        constructor(callback: IntersectionObserverCallback) {
          this.callback = callback
        }

        observe(element: Element) {
          this.callback(
            [{ isIntersecting: true, target: element } as IntersectionObserverEntry],
            this as unknown as IntersectionObserver,
          )
        }

        disconnect() {}
      },
    )
  })

  it('renders the transcript and protects the composer from duplicate submission', async () => {
    let resolveSend: ((value: ClubChatMessage) => void) | undefined
    mocks.send.mockReturnValue(
      new Promise((resolve) => {
        resolveSend = resolve
      }),
    )
    const user = userEvent.setup()
    render(<ClubChatPanel clubId="club-1" />)

    expect(screen.getByText(firstMessage.content)).toBeInTheDocument()
    expect(screen.getByText(secondMessage.content)).toBeInTheDocument()
    const composer = screen.getByRole('textbox', { name: 'Tin nhắn mới' })
    await user.type(composer, 'Chương này rất hay')
    await user.dblClick(screen.getByRole('button', { name: 'Gửi tin nhắn' }))

    expect(mocks.send).toHaveBeenCalledOnce()
    expect(mocks.send).toHaveBeenCalledWith('Chương này rất hay')
    await act(async () => resolveSend?.({ ...secondMessage, id: 'message-3' }))
    await waitFor(() => expect(composer).toHaveValue(''))
  })

  it('validates empty content and keeps Shift+Enter from submitting', async () => {
    const user = userEvent.setup()
    render(<ClubChatPanel clubId="club-1" />)

    await user.click(screen.getByRole('button', { name: 'Gửi tin nhắn' }))
    expect(screen.getByRole('alert')).toHaveTextContent('Hãy nhập nội dung tin nhắn.')

    const composer = screen.getByRole('textbox', { name: 'Tin nhắn mới' })
    fireEvent.keyDown(composer, { key: 'Enter', shiftKey: true })
    expect(mocks.send).not.toHaveBeenCalled()
  })

  it('does not jump while reading old messages and exposes a button for unread messages', async () => {
    const user = userEvent.setup()
    const view = render(<ClubChatPanel clubId="club-1" />)
    const log = screen.getByRole('log', { name: 'Tin nhắn câu lạc bộ' })
    Object.defineProperties(log, {
      scrollHeight: { configurable: true, value: 1000 },
      clientHeight: { configurable: true, value: 300 },
      scrollTop: { configurable: true, writable: true, value: 100 },
    })
    fireEvent.scroll(log)
    mocks.markRead.mockClear()

    setDefaultChat({ unreadCount: 2 })
    view.rerender(<ClubChatPanel clubId="club-1" />)
    const newMessages = screen.getByRole('button', { name: '2 tin nhắn mới' })
    await user.click(newMessages)

    expect(HTMLElement.prototype.scrollTo).toHaveBeenCalled()
    expect(mocks.markRead).toHaveBeenCalledWith(secondMessage.id)
  })
})
