import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { API_BASE_URL, getRealtimeAccessToken } from '../lib/api'

export const CLUB_CHAT_MESSAGE_EVENT = 'ClubChatMessageCreated'

export function clubChatHubUrl() {
  const apiUrl = new URL(API_BASE_URL, window.location.origin)
  const apiBasePath = apiUrl.pathname.replace(/\/api\/?$/, '')
  apiUrl.pathname = `${apiBasePath}/hubs/club-chat`.replace(/\/{2,}/g, '/')
  apiUrl.search = ''
  apiUrl.hash = ''
  return apiUrl.toString()
}

export function createClubChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(clubChatHubUrl(), { accessTokenFactory: getRealtimeAccessToken })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build()
}
