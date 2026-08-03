import { createContext, useContext } from 'react'

export type DirectMessageConnectionStatus =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'

export interface DirectMessageRealtimeValue {
  status: DirectMessageConnectionStatus
  retry: () => void
}

export const DirectMessageRealtimeContext = createContext<DirectMessageRealtimeValue>({
  status: 'disconnected',
  retry: () => undefined,
})

export function useDirectMessageRealtime() {
  return useContext(DirectMessageRealtimeContext)
}
