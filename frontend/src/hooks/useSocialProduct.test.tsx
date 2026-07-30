import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Challenge, User } from '../types/domain'
import { challengeKeys, challengeViewerScope } from './challengeKeys'
import {
  useChallenge,
  useChallengeMembership,
  useChallenges,
} from './useSocialProduct'

const reader: User = {
  id: 'reader-1',
  email: 'reader@example.com',
  displayName: 'Bạn đọc',
  role: 'USER',
}

function challenge(overrides: Partial<Challenge> = {}): Challenge {
  return {
    id: 'challenge-123',
    title: 'Thử thách tháng bảy',
    description: 'Đọc đều mỗi ngày.',
    startDate: '2026-07-01T00:00:00Z',
    endDate: '2026-07-31T23:59:59Z',
    goalBooks: 3,
    currentBooks: 0,
    participantCount: 10,
    isJoined: false,
    isPublished: true,
    ...overrides,
  }
}

function page(item: Challenge) {
  return {
    items: [item],
    page: 1,
    pageSize: 50,
    totalItems: 1,
    totalPages: 1,
  }
}

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isAuthenticated: false,
    isLoading: false,
  },
  challenges: vi.fn(),
  detail: vi.fn(),
  join: vi.fn(),
  leave: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../services/challenge.service', () => ({
  challengeService: {
    challenges: mocks.challenges,
    detail: mocks.detail,
    join: mocks.join,
    leave: mocks.leave,
  },
}))

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  })
}

function Providers({ client, children }: { client: QueryClient; children: ReactNode }) {
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

function ChallengeListProbe({ renderId }: { renderId: number }) {
  const query = useChallenges()
  return (
    <output data-testid="challenge-list">
      {query.data?.items[0]?.title ?? 'Đang tải'}-{renderId}
    </output>
  )
}

function MembershipProbe() {
  const list = useChallenges()
  const detail = useChallenge('challenge-123')
  const membership = useChallengeMembership(
    'challenge-123',
    Boolean(detail.data?.isJoined),
  )

  return (
    <>
      <output data-testid="list-membership">
        {list.data?.items[0]?.isJoined ? 'joined' : 'not-joined'}
      </output>
      <output data-testid="detail-membership">
        {detail.data?.isJoined ? 'joined' : 'not-joined'}
      </output>
      <button
        type="button"
        disabled={membership.isPending}
        onClick={() => membership.mutate()}
      >
        Đổi trạng thái
      </button>
    </>
  )
}

describe('challenge query ownership and invalidation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, {
      user: null,
      isAuthenticated: false,
      isLoading: false,
    })
  })

  it('uses a distinct principal-scoped cache after guest login', async () => {
    mocks.challenges.mockImplementation(async () =>
      mocks.auth.user
        ? page(challenge({ title: 'Payload người dùng', isJoined: true, currentBooks: 2 }))
        : page(challenge({ title: 'Payload khách' })),
    )
    const client = createQueryClient()
    const view = render(
      <Providers client={client}>
        <ChallengeListProbe renderId={1} />
      </Providers>,
    )

    expect(await screen.findByText('Payload khách-1')).toBeInTheDocument()
    expect(mocks.challenges).toHaveBeenCalledOnce()

    Object.assign(mocks.auth, {
      user: reader,
      isAuthenticated: true,
    })
    view.rerender(
      <Providers client={client}>
        <ChallengeListProbe renderId={2} />
      </Providers>,
    )

    expect(await screen.findByText('Payload người dùng-2')).toBeInTheDocument()
    expect(mocks.challenges).toHaveBeenCalledTimes(2)
    expect(
      client.getQueryData<{ items: Challenge[] }>(
        challengeKeys.lists(challengeViewerScope(null)),
      )?.items[0]?.title,
    ).toBe('Payload khách')
    expect(
      client.getQueryData<{ items: Challenge[] }>(
        challengeKeys.lists(challengeViewerScope(reader.id)),
      )?.items[0]?.title,
    ).toBe('Payload người dùng')
  })

  it('waits for stored-session restoration before loading a guest response', async () => {
    mocks.auth.isLoading = true
    mocks.challenges.mockResolvedValue(page(challenge()))
    const client = createQueryClient()
    const view = render(
      <Providers client={client}>
        <ChallengeListProbe renderId={1} />
      </Providers>,
    )

    expect(mocks.challenges).not.toHaveBeenCalled()

    mocks.auth.isLoading = false
    view.rerender(
      <Providers client={client}>
        <ChallengeListProbe renderId={2} />
      </Providers>,
    )

    await waitFor(() => expect(mocks.challenges).toHaveBeenCalledOnce())
  })

  it('refetches both list and detail caches after join and leave', async () => {
    Object.assign(mocks.auth, {
      user: reader,
      isAuthenticated: true,
    })
    let isJoined = false
    mocks.challenges.mockImplementation(async () =>
      page(challenge({ isJoined, participantCount: isJoined ? 11 : 10 })),
    )
    mocks.detail.mockImplementation(async () =>
      challenge({ isJoined, participantCount: isJoined ? 11 : 10 }),
    )
    mocks.join.mockImplementation(async () => {
      isJoined = true
      return challenge({ isJoined: true, participantCount: 11 })
    })
    mocks.leave.mockImplementation(async () => {
      isJoined = false
      return challenge()
    })
    const client = createQueryClient()
    const user = userEvent.setup()
    render(
      <Providers client={client}>
        <MembershipProbe />
      </Providers>,
    )

    await waitFor(() => {
      expect(mocks.challenges).toHaveBeenCalledOnce()
      expect(mocks.detail).toHaveBeenCalledOnce()
    })

    await user.click(screen.getByRole('button', { name: 'Đổi trạng thái' }))

    await waitFor(() => {
      expect(mocks.join).toHaveBeenCalledOnce()
      expect(mocks.challenges).toHaveBeenCalledTimes(2)
      expect(mocks.detail).toHaveBeenCalledTimes(2)
      expect(screen.getByTestId('list-membership')).toHaveTextContent('joined')
      expect(screen.getByTestId('detail-membership')).toHaveTextContent('joined')
    })

    await user.click(screen.getByRole('button', { name: 'Đổi trạng thái' }))

    await waitFor(() => {
      expect(mocks.leave).toHaveBeenCalledOnce()
      expect(mocks.challenges).toHaveBeenCalledTimes(3)
      expect(mocks.detail).toHaveBeenCalledTimes(3)
      expect(screen.getByTestId('list-membership')).toHaveTextContent('not-joined')
      expect(screen.getByTestId('detail-membership')).toHaveTextContent('not-joined')
    })
  })
})
