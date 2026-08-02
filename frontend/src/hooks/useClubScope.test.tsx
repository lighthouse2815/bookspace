import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Club, User } from '../types/domain'
import { clubKeys, useClub } from './useSocialProduct'

const reader: User = {
  id: 'reader-1',
  displayName: 'Bạn đọc',
  role: 'USER',
}

function club(name: string, isJoined: boolean): Club {
  return {
    id: 'club-1',
    name,
    description: null,
    coverImageUrl: null,
    memberCount: 3,
    isPrivate: false,
    isJoined,
    currentBook: null,
    owner: reader,
    posts: [],
    viewerRole: isJoined ? 'MEMBER' : null,
    permissions: {
      canEdit: false,
      canInvite: false,
      canManageMembers: false,
      canManageCurrentBook: false,
      canLeave: isJoined,
    },
    createdAt: '2026-08-01T00:00:00Z',
  }
}

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isLoading: true,
  },
  club: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    ...mocks.auth,
    isAuthenticated: Boolean(mocks.auth.user),
  }),
}))

vi.mock('../services/club.service', () => ({
  clubService: { club: mocks.club },
}))

function Providers({ client, children }: { client: QueryClient; children: ReactNode }) {
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

function ClubProbe() {
  const detail = useClub('club-1')
  return <output>{detail.data?.name ?? 'Đang tải'}</output>
}

describe('club detail principal scope', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, { user: null, isLoading: true })
    mocks.club.mockImplementation(async () =>
      mocks.auth.user ? club('Payload thành viên', true) : club('Payload khách', false),
    )
  })

  it('waits for auth bootstrap and does not reuse a guest membership payload after login', async () => {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
    })
    const view = render(
      <Providers client={client}>
        <ClubProbe />
      </Providers>,
    )
    expect(mocks.club).not.toHaveBeenCalled()

    mocks.auth.isLoading = false
    view.rerender(
      <Providers client={client}>
        <ClubProbe />
      </Providers>,
    )
    expect(await screen.findByText('Payload khách')).toBeInTheDocument()

    mocks.auth.user = reader
    view.rerender(
      <Providers client={client}>
        <ClubProbe />
      </Providers>,
    )
    expect(await screen.findByText('Payload thành viên')).toBeInTheDocument()
    expect(mocks.club).toHaveBeenCalledTimes(2)
    await waitFor(() => {
      expect(client.getQueryData<Club>(clubKeys.detail('club-1', 'guest'))?.isJoined).toBe(false)
      expect(client.getQueryData<Club>(clubKeys.detail('club-1', reader.id))?.isJoined).toBe(true)
    })
  })
})
