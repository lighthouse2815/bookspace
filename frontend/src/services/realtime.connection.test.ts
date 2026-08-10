import { describe, expect, it } from 'vitest'
import { clubChatHubUrl } from './club-chat.connection'
import { directMessageHubUrl } from './direct-message.connection'

describe('same-origin realtime URLs', () => {
  it('derives both SignalR hubs from the browser origin when API uses /api', () => {
    expect(clubChatHubUrl()).toBe(new URL('/hubs/club-chat', window.location.origin).toString())
    expect(directMessageHubUrl()).toBe(
      new URL('/hubs/direct-messages', window.location.origin).toString(),
    )
  })
})
