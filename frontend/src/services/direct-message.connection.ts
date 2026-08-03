import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { API_BASE_URL, getRealtimeAccessToken } from '../lib/api'

export const DIRECT_MESSAGE_CREATED_EVENT = 'DirectMessageCreated'

export function directMessageHubUrl() {
  const apiUrl = new URL(API_BASE_URL, window.location.origin)
  const apiBasePath = apiUrl.pathname.replace(/\/api\/?$/, '')
  apiUrl.pathname = `${apiBasePath}/hubs/direct-messages`.replace(/\/{2,}/g, '/')
  apiUrl.search = ''
  apiUrl.hash = ''
  return apiUrl.toString()
}

export function createDirectMessageConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(directMessageHubUrl(), { accessTokenFactory: getRealtimeAccessToken })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build()
}
