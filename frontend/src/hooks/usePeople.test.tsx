import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../types/api'
import type { User, UserDiscoveryItem } from '../types/domain'
import {
  feedKeys,
  peopleKeys,
  useFollowUser,
  usePeopleSearch,
  userKeys,
  viewerScope,
} from './useCommunity'

const reader: User = {
  id: 'reader-1',
  email: 'reader@example.com',
  displayName: 'Bạn đọc',
  role: 'USER',
}

const target: UserDiscoveryItem = {
  id: 'target-1',
  displayName: 'Lan Chi',
  bio: 'Đọc truyện ngắn.',
  avatarUrl: undefined,
  followerCount: 3,
  booksReadCount: 7,
  isFollowing: false,
  followsYou: true,
  mutualFollowCount: 1,
  reason: 'MUTUAL_FOLLOWS',
  reasonText: '1 người bạn theo dõi cũng theo dõi độc giả này.',
}

function page(item: UserDiscoveryItem): PageResult<UserDiscoveryItem> {
  return {
    items: [item],
    page: 1,
    pageSize: 20,
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
  people: vi.fn(),
  suggestions: vi.fn(),
  follow: vi.fn(),
  unfollow: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../services/community.service', () => ({
  communityService: {
    people: mocks.people,
    suggestions: mocks.suggestions,
    follow: mocks.follow,
    unfollow: mocks.unfollow,
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

function SearchProbe({ renderId }: { renderId: number }) {
  const query = usePeopleSearch('', 1)
  return (
    <output data-testid="people-result">
      {query.data?.items[0]?.displayName ?? 'Đang tải'}-{renderId}
    </output>
  )
}

function FollowProbe() {
  const follow = useFollowUser(target.id, false)
  return (
    <button type="button" onClick={() => follow.mutate()} disabled={follow.isPending}>
      Theo dõi
    </button>
  )
}

describe('people query ownership and follow invalidation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.assign(mocks.auth, {
      user: null,
      isAuthenticated: false,
      isLoading: false,
    })
  })

  it('keeps guest and authenticated people payloads in separate principal scopes', async () => {
    mocks.people.mockImplementation(async () =>
      page({
        ...target,
        displayName: mocks.auth.user ? 'Payload của tài khoản' : 'Payload khách',
        isFollowing: Boolean(mocks.auth.user),
      }),
    )
    const client = createQueryClient()
    const view = render(
      <Providers client={client}>
        <SearchProbe renderId={1} />
      </Providers>,
    )

    expect(await screen.findByText('Payload khách-1')).toBeInTheDocument()
    Object.assign(mocks.auth, {
      user: reader,
      isAuthenticated: true,
    })
    view.rerender(
      <Providers client={client}>
        <SearchProbe renderId={2} />
      </Providers>,
    )

    expect(await screen.findByText('Payload của tài khoản-2')).toBeInTheDocument()
    expect(mocks.people).toHaveBeenCalledTimes(2)
    expect(
      client.getQueryData<PageResult<UserDiscoveryItem>>(
        peopleKeys.search(viewerScope(null), '', 1, 20),
      )?.items[0]?.displayName,
    ).toBe('Payload khách')
    expect(
      client.getQueryData<PageResult<UserDiscoveryItem>>(
        peopleKeys.search(viewerScope(reader.id), '', 1, 20),
      )?.items[0]?.displayName,
    ).toBe('Payload của tài khoản')
  })

  it('waits for auth restoration before loading the public directory', async () => {
    mocks.auth.isLoading = true
    mocks.people.mockResolvedValue(page(target))
    const client = createQueryClient()
    const view = render(
      <Providers client={client}>
        <SearchProbe renderId={1} />
      </Providers>,
    )
    expect(mocks.people).not.toHaveBeenCalled()

    mocks.auth.isLoading = false
    view.rerender(
      <Providers client={client}>
        <SearchProbe renderId={2} />
      </Providers>,
    )
    await waitFor(() => expect(mocks.people).toHaveBeenCalledOnce())
  })

  it('patches server follow state and invalidates every dependent principal-scoped cache', async () => {
    Object.assign(mocks.auth, {
      user: reader,
      isAuthenticated: true,
    })
    mocks.follow.mockResolvedValue({
      ...reader,
      id: target.id,
      displayName: target.displayName,
      isFollowing: true,
      followerCount: 4,
    })
    const client = createQueryClient()
    const scope = viewerScope(reader.id)
    const searchKey = peopleKeys.search(scope, '', 1, 20)
    const suggestionsKey = peopleKeys.suggestionPage(scope, 1, 20)
    const targetProfileKey = userKeys.detail(scope, target.id)
    const actorProfileKey = userKeys.detail(scope, reader.id)
    const targetFollowersKey = userKeys.followers(scope, target.id)
    const actorFollowingKey = userKeys.following(scope, reader.id)
    const feedKey = feedKeys.scoped(scope)
    client.setQueryData(searchKey, page(target))
    client.setQueryData(suggestionsKey, page(target))
    client.setQueryData(targetProfileKey, { ...reader, id: target.id, isFollowing: false })
    client.setQueryData(actorProfileKey, reader)
    client.setQueryData(targetFollowersKey, page(target))
    client.setQueryData(actorFollowingKey, page(target))
    client.setQueryData(feedKey, { items: [], page: 1 })
    client.setQueryData(['dashboard'], { booksRead: 1 })

    const user = userEvent.setup()
    render(
      <Providers client={client}>
        <FollowProbe />
      </Providers>,
    )
    await user.click(screen.getByRole('button', { name: 'Theo dõi' }))

    await waitFor(() => expect(mocks.follow).toHaveBeenCalledWith(target.id))
    expect(client.getQueryData<PageResult<UserDiscoveryItem>>(searchKey)?.items[0].isFollowing).toBe(
      true,
    )
    expect(client.getQueryData<PageResult<UserDiscoveryItem>>(suggestionsKey)?.items).toEqual([])
    expect(client.getQueryData<User>(targetProfileKey)?.isFollowing).toBe(true)
    for (const key of [
      searchKey,
      suggestionsKey,
      targetProfileKey,
      actorProfileKey,
      targetFollowersKey,
      actorFollowingKey,
      feedKey,
      ['dashboard'],
    ]) {
      expect(client.getQueryState(key)?.isInvalidated).toBe(true)
    }
  })
})
