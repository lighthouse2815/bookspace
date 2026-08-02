export const clubChatKeys = {
  all: ['club-chat'] as const,
  scope: (userId: string) => [...clubChatKeys.all, userId] as const,
  room: (userId: string, clubId: string) => [...clubChatKeys.scope(userId), clubId] as const,
  messages: (userId: string, clubId: string) =>
    [...clubChatKeys.room(userId, clubId), 'messages'] as const,
  unread: (userId: string, clubId: string) =>
    [...clubChatKeys.room(userId, clubId), 'unread'] as const,
}
