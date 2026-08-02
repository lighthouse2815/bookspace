import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type InfiniteData,
} from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAuth } from '../contexts/AuthContext'
import {
  CLUB_CHAT_MESSAGE_EVENT,
  createClubChatConnection,
} from '../services/club-chat.connection'
import { clubChatService } from '../services/club-chat.service'
import type {
  ClubChatMessage,
  ClubChatMessagePage,
  ClubChatReadState,
} from '../types/domain'
import { clubChatKeys } from './clubChatKeys'

export type ClubChatConnectionStatus =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'

type ChatHistory = InfiniteData<ClubChatMessagePage>

function containsMessage(history: ChatHistory | undefined, messageId: string) {
  return history?.pages.some((page) => page.items.some((item) => item.id === messageId)) ?? false
}

function mergeMessage(history: ChatHistory | undefined, message: ClubChatMessage): ChatHistory {
  if (containsMessage(history, message.id)) return history!
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

function chronologicalMessages(history: ChatHistory | undefined) {
  const uniqueMessages = new Map<string, ClubChatMessage>()
  for (const page of history?.pages ?? []) {
    for (const message of page.items) uniqueMessages.set(message.id, message)
  }
  return [...uniqueMessages.values()].sort((left, right) => {
    const timeDifference = Date.parse(left.createdAt) - Date.parse(right.createdAt)
    return timeDifference || left.id.localeCompare(right.id)
  })
}

export function useClubChat({
  clubId,
  enabled,
  shouldMarkIncomingRead,
}: {
  clubId: string
  enabled: boolean
  shouldMarkIncomingRead: () => boolean
}) {
  const { user, isLoading: isAuthLoading } = useAuth()
  const queryClient = useQueryClient()
  const userId = user?.id ?? 'guest'
  const canConnect = Boolean(clubId && user && !isAuthLoading && enabled)
  const messageKey = useMemo(() => clubChatKeys.messages(userId, clubId), [clubId, userId])
  const unreadKey = useMemo(() => clubChatKeys.unread(userId, clubId), [clubId, userId])
  const [connectionStatus, setConnectionStatus] =
    useState<ClubChatConnectionStatus>('disconnected')
  const [connectionAttempt, setConnectionAttempt] = useState(0)
  const lastReadRequest = useRef<string | null>(null)
  const incomingHandler = useRef<(message: ClubChatMessage) => void>(() => undefined)
  const shouldMarkReadRef = useRef(shouldMarkIncomingRead)

  useEffect(() => {
    shouldMarkReadRef.current = shouldMarkIncomingRead
  }, [shouldMarkIncomingRead])

  const history = useInfiniteQuery({
    queryKey: messageKey,
    queryFn: ({ pageParam }) => clubChatService.messages(clubId, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) =>
      lastPage.hasMore ? (lastPage.nextCursor ?? undefined) : undefined,
    enabled: canConnect,
    staleTime: 10_000,
  })

  const unread = useQuery({
    queryKey: unreadKey,
    queryFn: () => clubChatService.unreadCount(clubId),
    enabled: canConnect,
    staleTime: 10_000,
  })

  const markReadMutation = useMutation({
    mutationFn: (lastReadMessageId: string) =>
      clubChatService.markRead(clubId, lastReadMessageId),
    onMutate: async (lastReadMessageId) => {
      await queryClient.cancelQueries({ queryKey: unreadKey })
      const previous = queryClient.getQueryData<ClubChatReadState>(unreadKey)
      queryClient.setQueryData<ClubChatReadState>(unreadKey, {
        clubId,
        count: 0,
        lastReadMessageId,
        lastReadAt: previous?.lastReadAt ?? null,
      })
      return { previous }
    },
    onError: (_error, _messageId, context) => {
      lastReadRequest.current = null
      if (context?.previous) queryClient.setQueryData(unreadKey, context.previous)
    },
    onSuccess: (readState) => queryClient.setQueryData(unreadKey, readState),
  })
  const mutateMarkRead = markReadMutation.mutate

  const markRead = useCallback(
    (lastReadMessageId: string) => {
      if (!canConnect || lastReadRequest.current === lastReadMessageId) return
      lastReadRequest.current = lastReadMessageId
      mutateMarkRead(lastReadMessageId)
    },
    [canConnect, mutateMarkRead],
  )

  const mergeIntoHistory = useCallback(
    (message: ClubChatMessage) => {
      const currentHistory = queryClient.getQueryData<ChatHistory>(messageKey)
      if (containsMessage(currentHistory, message.id)) return false
      queryClient.setQueryData<ChatHistory>(messageKey, mergeMessage(currentHistory, message))
      if (!currentHistory) {
        void queryClient.invalidateQueries({ queryKey: messageKey })
      }
      return true
    },
    [messageKey, queryClient],
  )

  const incrementUnread = useCallback(() => {
    queryClient.setQueryData<ClubChatReadState>(unreadKey, (current) => ({
      clubId,
      count: (current?.count ?? 0) + 1,
      lastReadMessageId: current?.lastReadMessageId ?? null,
      lastReadAt: current?.lastReadAt ?? null,
    }))
  }, [clubId, queryClient, unreadKey])

  const sendMessage = useMutation({
    mutationFn: (content: string) => clubChatService.sendMessage(clubId, content),
    onSuccess: (message) => {
      mergeIntoHistory(message)
      markRead(message.id)
    },
  })

  incomingHandler.current = (message) => {
    if (message.clubId !== clubId || !mergeIntoHistory(message)) return
    if (message.sender.id === user?.id || shouldMarkReadRef.current()) {
      markRead(message.id)
    } else {
      incrementUnread()
    }
  }

  useEffect(() => {
    if (!canConnect) {
      setConnectionStatus('disconnected')
      return
    }

    let disposed = false
    const connection = createClubChatConnection()
    const receiveMessage = (message: ClubChatMessage) => incomingHandler.current(message)

    connection.on(CLUB_CHAT_MESSAGE_EVENT, receiveMessage)
    connection.onreconnecting(() => {
      if (!disposed) setConnectionStatus('reconnecting')
    })
    connection.onreconnected(() => {
      if (disposed) return
      setConnectionStatus('connected')
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: messageKey }),
        queryClient.invalidateQueries({ queryKey: unreadKey }),
      ])
    })
    connection.onclose(() => {
      if (!disposed) setConnectionStatus('disconnected')
    })

    setConnectionStatus('connecting')
    void connection
      .start()
      .then(() => {
        if (disposed) void connection.stop()
        else setConnectionStatus('connected')
      })
      .catch(() => {
        if (!disposed) setConnectionStatus('disconnected')
      })

    return () => {
      disposed = true
      connection.off(CLUB_CHAT_MESSAGE_EVENT, receiveMessage)
      void connection.stop()
    }
  }, [canConnect, clubId, connectionAttempt, messageKey, queryClient, unreadKey, userId])

  useEffect(() => {
    if (!canConnect || connectionStatus !== 'disconnected') return
    const retryWhenOnline = () => setConnectionAttempt((attempt) => attempt + 1)
    window.addEventListener('online', retryWhenOnline)
    return () => window.removeEventListener('online', retryWhenOnline)
  }, [canConnect, connectionStatus])

  const messages = useMemo(() => chronologicalMessages(history.data), [history.data])

  return {
    messages,
    isLoading: history.isLoading,
    isError: history.isError,
    error: history.error,
    refetch: history.refetch,
    hasOlderMessages: Boolean(history.hasNextPage),
    loadOlderMessages: history.fetchNextPage,
    isLoadingOlderMessages: history.isFetchingNextPage,
    unreadCount: unread.data?.count ?? 0,
    isUnreadError: unread.isError,
    markRead,
    sendMessage: sendMessage.mutateAsync,
    isSending: sendMessage.isPending,
    connectionStatus,
    retryConnection: () => setConnectionAttempt((attempt) => attempt + 1),
  }
}
