import { api, unwrap } from '../lib/api'
import type { ApiEnvelope } from '../types/api'
import type {
  Conversation,
  ConversationPage,
  DirectMessage,
  DirectMessagePage,
  DirectMessageReadState,
} from '../types/domain'

const CONVERSATION_PAGE_SIZE = 20
const MESSAGE_PAGE_SIZE = 30

export const directMessageService = {
  conversations: async (cursor?: string | null) =>
    unwrap(
      await api.get<ApiEnvelope<ConversationPage>>('/conversations', {
        params: { cursor: cursor || undefined, pageSize: CONVERSATION_PAGE_SIZE },
      }),
    ),

  conversation: async (conversationId: string) =>
    unwrap(await api.get<ApiEnvelope<Conversation>>(`/conversations/${conversationId}`)),

  startConversation: async (targetUserId: string) =>
    unwrap(
      await api.post<ApiEnvelope<Conversation>>('/conversations', { targetUserId }),
    ),

  messages: async (conversationId: string, cursor?: string | null) =>
    unwrap(
      await api.get<ApiEnvelope<DirectMessagePage>>(
        `/conversations/${conversationId}/messages`,
        { params: { cursor: cursor || undefined, pageSize: MESSAGE_PAGE_SIZE } },
      ),
    ),

  sendMessage: async (conversationId: string, content: string) =>
    unwrap(
      await api.post<ApiEnvelope<DirectMessage>>(
        `/conversations/${conversationId}/messages`,
        { content },
      ),
    ),

  markRead: async (conversationId: string, lastReadMessageId: string) =>
    unwrap(
      await api.post<ApiEnvelope<DirectMessageReadState>>(
        `/conversations/${conversationId}/read`,
        { lastReadMessageId },
      ),
    ),

  unreadCount: async () =>
    unwrap(await api.get<ApiEnvelope<{ count: number }>>('/conversations/unread-count')),
}
