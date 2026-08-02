import { api, unwrap } from '../lib/api'
import type { ApiEnvelope } from '../types/api'
import type {
  ClubChatMessage,
  ClubChatMessagePage,
  ClubChatReadState,
} from '../types/domain'

const CHAT_PAGE_SIZE = 30

export const clubChatService = {
  messages: async (clubId: string, cursor?: string | null) =>
    unwrap(
      await api.get<ApiEnvelope<ClubChatMessagePage>>(`/clubs/${clubId}/chat/messages`, {
        params: { cursor: cursor || undefined, pageSize: CHAT_PAGE_SIZE },
      }),
    ),

  sendMessage: async (clubId: string, content: string) =>
    unwrap(
      await api.post<ApiEnvelope<ClubChatMessage>>(`/clubs/${clubId}/chat/messages`, {
        content,
      }),
    ),

  unreadCount: async (clubId: string) =>
    unwrap(
      await api.get<ApiEnvelope<ClubChatReadState>>(`/clubs/${clubId}/chat/unread-count`),
    ),

  markRead: async (clubId: string, lastReadMessageId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ClubChatReadState>>(`/clubs/${clubId}/chat/read`, {
        lastReadMessageId,
      }),
    ),
}
