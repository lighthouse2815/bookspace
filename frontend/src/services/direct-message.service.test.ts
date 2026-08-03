import { beforeEach, describe, expect, it, vi } from 'vitest'
import { directMessageService } from './direct-message.service'

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}))

vi.mock('../lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mocks.get(...args),
    post: (...args: unknown[]) => mocks.post(...args),
  },
  unwrap: (response: { data: { data: unknown } }) => response.data.data,
}))

describe('direct message service', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.get.mockResolvedValue({ data: { data: { items: [], nextCursor: null, hasMore: false } } })
    mocks.post.mockResolvedValue({ data: { data: { id: 'result-1' } } })
  })

  it('uses cursor-based inbox and message endpoints', async () => {
    await directMessageService.conversations('inbox-cursor')
    await directMessageService.messages('conversation-1', 'message-cursor')

    expect(mocks.get).toHaveBeenNthCalledWith(1, '/conversations', {
      params: { cursor: 'inbox-cursor', pageSize: 20 },
    })
    expect(mocks.get).toHaveBeenNthCalledWith(
      2,
      '/conversations/conversation-1/messages',
      { params: { cursor: 'message-cursor', pageSize: 30 } },
    )
  })

  it('starts, sends and marks read through the REST-authoritative contract', async () => {
    await directMessageService.startConversation('reader-2')
    await directMessageService.sendMessage('conversation-1', 'Xin chào')
    await directMessageService.markRead('conversation-1', 'message-1')

    expect(mocks.post).toHaveBeenNthCalledWith(1, '/conversations', {
      targetUserId: 'reader-2',
    })
    expect(mocks.post).toHaveBeenNthCalledWith(
      2,
      '/conversations/conversation-1/messages',
      { content: 'Xin chào' },
    )
    expect(mocks.post).toHaveBeenNthCalledWith(
      3,
      '/conversations/conversation-1/read',
      { lastReadMessageId: 'message-1' },
    )
  })
})
