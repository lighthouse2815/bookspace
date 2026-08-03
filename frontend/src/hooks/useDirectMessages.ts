import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type InfiniteData,
} from '@tanstack/react-query'
import { useMemo } from 'react'
import { useDirectMessageRealtime } from '../contexts/direct-message-realtime'
import { useAuth } from '../contexts/AuthContext'
import { directMessageService } from '../services/direct-message.service'
import type {
  Conversation,
  ConversationPage,
  DirectMessage,
  DirectMessagePage,
} from '../types/domain'
import { directMessageKeys } from './directMessageKeys'
import { mergeDirectMessage } from './directMessageCache'

type MessageHistory = InfiniteData<DirectMessagePage>

function chronologicalMessages(history: MessageHistory | undefined) {
  const uniqueMessages = new Map<string, DirectMessage>()
  for (const page of history?.pages ?? []) {
    for (const message of page.items) uniqueMessages.set(message.id, message)
  }
  return [...uniqueMessages.values()].sort((left, right) => {
    const timeDifference = Date.parse(left.createdAt) - Date.parse(right.createdAt)
    return timeDifference || left.id.localeCompare(right.id)
  })
}

export function useConversationInbox() {
  const { user, isLoading } = useAuth()
  const userId = user?.id ?? 'guest'
  return useInfiniteQuery({
    queryKey: directMessageKeys.inbox(userId),
    queryFn: ({ pageParam }) => directMessageService.conversations(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) =>
      lastPage.hasMore ? (lastPage.nextCursor ?? undefined) : undefined,
    enabled: Boolean(user) && !isLoading,
  })
}

export function useConversation(conversationId?: string) {
  const { user, isLoading } = useAuth()
  const userId = user?.id ?? 'guest'
  return useQuery({
    queryKey: directMessageKeys.conversation(userId, conversationId ?? ''),
    queryFn: () => directMessageService.conversation(conversationId!),
    enabled: Boolean(user && conversationId) && !isLoading,
  })
}

export function useDirectMessageThread(conversationId?: string) {
  const { user, isLoading } = useAuth()
  const queryClient = useQueryClient()
  const realtime = useDirectMessageRealtime()
  const userId = user?.id ?? 'guest'
  const messageKey = directMessageKeys.messages(userId, conversationId ?? '')
  const history = useInfiniteQuery({
    queryKey: messageKey,
    queryFn: ({ pageParam }) => directMessageService.messages(conversationId!, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) =>
      lastPage.hasMore ? (lastPage.nextCursor ?? undefined) : undefined,
    enabled: Boolean(user && conversationId) && !isLoading,
    staleTime: 10_000,
  })

  const sendMessage = useMutation({
    mutationFn: (content: string) => directMessageService.sendMessage(conversationId!, content),
    onSuccess: (message) => {
      queryClient.setQueryData<MessageHistory>(messageKey, (current) =>
        mergeDirectMessage(current, message),
      )
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: directMessageKeys.inbox(userId) }),
        queryClient.invalidateQueries({
          queryKey: directMessageKeys.conversation(userId, conversationId ?? ''),
          exact: true,
        }),
      ])
    },
  })

  const markRead = useMutation({
    mutationFn: (lastReadMessageId: string) =>
      directMessageService.markRead(conversationId!, lastReadMessageId),
    onSuccess: () => {
      queryClient.setQueryData<Conversation>(
        directMessageKeys.conversation(userId, conversationId ?? ''),
        (current) => (current ? { ...current, unreadCount: 0 } : current),
      )
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: directMessageKeys.inbox(userId) }),
        queryClient.invalidateQueries({ queryKey: directMessageKeys.unread(userId) }),
      ])
    },
  })

  return {
    messages: useMemo(() => chronologicalMessages(history.data), [history.data]),
    isLoading: history.isLoading,
    isError: history.isError,
    refetch: history.refetch,
    hasOlderMessages: Boolean(history.hasNextPage),
    loadOlderMessages: history.fetchNextPage,
    isLoadingOlderMessages: history.isFetchingNextPage,
    sendMessage: sendMessage.mutateAsync,
    isSending: sendMessage.isPending,
    markRead: markRead.mutate,
    isMarkingRead: markRead.isPending,
    connectionStatus: realtime.status,
    retryConnection: realtime.retry,
  }
}

export function useStartConversation() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const userId = user?.id ?? 'guest'
  return useMutation({
    mutationFn: directMessageService.startConversation,
    onSuccess: (conversation) => {
      queryClient.setQueryData(
        directMessageKeys.conversation(userId, conversation.id),
        conversation,
      )
      void queryClient.invalidateQueries({ queryKey: directMessageKeys.inbox(userId) })
    },
  })
}

export function useUnreadDirectMessageCount() {
  const { user, isLoading } = useAuth()
  const userId = user?.id ?? 'guest'
  return useQuery({
    queryKey: directMessageKeys.unread(userId),
    queryFn: directMessageService.unreadCount,
    enabled: Boolean(user) && !isLoading,
  })
}

export function flattenConversations(data?: InfiniteData<ConversationPage>) {
  const conversations = new Map<string, Conversation>()
  for (const page of data?.pages ?? []) {
    for (const conversation of page.items) conversations.set(conversation.id, conversation)
  }
  return [...conversations.values()].sort((left, right) =>
    Date.parse(right.lastActivityAt) - Date.parse(left.lastActivityAt) ||
    right.id.localeCompare(left.id),
  )
}
