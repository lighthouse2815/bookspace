export const directMessageKeys = {
  all: ['direct-messages'] as const,
  scope: (userId: string) => [...directMessageKeys.all, userId] as const,
  inbox: (userId: string) => [...directMessageKeys.scope(userId), 'inbox'] as const,
  conversation: (userId: string, conversationId: string) =>
    [...directMessageKeys.scope(userId), 'conversation', conversationId] as const,
  messages: (userId: string, conversationId: string) =>
    [...directMessageKeys.conversation(userId, conversationId), 'messages'] as const,
  unread: (userId: string) => [...directMessageKeys.scope(userId), 'unread'] as const,
}
