import type { InfiniteData } from '@tanstack/react-query'
import type { DirectMessage, DirectMessagePage } from '../types/domain'

export type DirectMessageHistory = InfiniteData<DirectMessagePage>

export function mergeDirectMessage(
  history: DirectMessageHistory | undefined,
  message: DirectMessage,
): DirectMessageHistory {
  if (history?.pages.some((page) => page.items.some((item) => item.id === message.id))) {
    return history
  }
  if (!history?.pages.length) {
    return {
      pages: [{ items: [message], nextCursor: null, hasMore: false }],
      pageParams: [null],
    }
  }

  const [newestPage, ...olderPages] = history.pages
  return {
    ...history,
    pages: [{ ...newestPage, items: [message, ...newestPage.items] }, ...olderPages],
  }
}
