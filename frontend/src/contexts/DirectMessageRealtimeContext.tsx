import { useQueryClient } from '@tanstack/react-query'
import {
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { useAuth } from './AuthContext'
import {
  createDirectMessageConnection,
  DIRECT_MESSAGE_CREATED_EVENT,
} from '../services/direct-message.connection'
import { directMessageKeys } from '../hooks/directMessageKeys'
import { mergeDirectMessage, type DirectMessageHistory } from '../hooks/directMessageCache'
import { notificationKeys } from '../hooks/useNotifications'
import type { DirectMessage } from '../types/domain'
import {
  DirectMessageRealtimeContext,
  type DirectMessageConnectionStatus,
} from './direct-message-realtime'

export function DirectMessageRealtimeProvider({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<DirectMessageConnectionStatus>('disconnected')
  const [attempt, setAttempt] = useState(0)
  const userId = user?.id

  useEffect(() => {
    if (!userId || isLoading) {
      setStatus('disconnected')
      return
    }

    let disposed = false
    const connection = createDirectMessageConnection()
    const receiveMessage = (message: DirectMessage) => {
      const messageKey = directMessageKeys.messages(userId, message.conversationId)
      const current = queryClient.getQueryData<DirectMessageHistory>(messageKey)
      queryClient.setQueryData<DirectMessageHistory>(
        messageKey,
        mergeDirectMessage(current, message),
      )
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: directMessageKeys.inbox(userId) }),
        queryClient.invalidateQueries({
          queryKey: directMessageKeys.conversation(userId, message.conversationId),
          exact: true,
        }),
        queryClient.invalidateQueries({ queryKey: directMessageKeys.unread(userId) }),
        queryClient.invalidateQueries({ queryKey: notificationKeys.scope(userId) }),
      ])
    }

    connection.on(DIRECT_MESSAGE_CREATED_EVENT, receiveMessage)
    connection.onreconnecting(() => {
      if (!disposed) setStatus('reconnecting')
    })
    connection.onreconnected(() => {
      if (disposed) return
      setStatus('connected')
      void queryClient.invalidateQueries({ queryKey: directMessageKeys.scope(userId) })
    })
    connection.onclose(() => {
      if (!disposed) setStatus('disconnected')
    })

    setStatus('connecting')
    void connection
      .start()
      .then(() => {
        if (disposed) void connection.stop()
        else setStatus('connected')
      })
      .catch(() => {
        if (!disposed) setStatus('disconnected')
      })

    return () => {
      disposed = true
      connection.off(DIRECT_MESSAGE_CREATED_EVENT, receiveMessage)
      void connection.stop()
    }
  }, [attempt, isLoading, queryClient, userId])

  const value = useMemo(
    () => ({ status, retry: () => setAttempt((current) => current + 1) }),
    [status],
  )
  return (
    <DirectMessageRealtimeContext.Provider value={value}>
      {children}
    </DirectMessageRealtimeContext.Provider>
  )
}
