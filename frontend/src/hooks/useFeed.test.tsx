import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../types/api'
import type { FeedItem, User } from '../types/domain'
import { feedKeys, useFeed } from './useCommunity'

const readerA: User = {
  id: 'reader-a',
  displayName: 'Độc giả A',
  role: 'USER',
}

const readerB: User = {
  id: 'reader-b',
  displayName: 'Độc giả B',
  role: 'USER',
}

const emptyPage: PageResult<FeedItem> = {
  items: [],
  page: 1,
  pageSize: 10,
  totalItems: 0,
  totalPages: 0,
}

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isLoading: false,
  },
  feed: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../services/community.service', () => ({
  communityService: {
    feed: (...args: unknown[]) => mocks.feed(...args),
  },
}))

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
    },
  })
}

function Providers({ client, children }: { client: QueryClient; children: ReactNode }) {
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('feed query ownership', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.auth.user = readerA
    mocks.auth.isLoading = false
    mocks.feed.mockResolvedValue(emptyPage)
  })

  it('scopes cache and requests by principal, filter, page and page size', async () => {
    const client = createQueryClient()
    const view = renderHook(
      ({ type, page }: { type: 'READING' | 'CLUB'; page: number }) =>
        useFeed({ type, page, pageSize: 10 }),
      {
        wrapper: ({ children }) => <Providers client={client}>{children}</Providers>,
        initialProps: { type: 'READING', page: 2 },
      },
    )

    await waitFor(() =>
      expect(mocks.feed).toHaveBeenCalledWith({ type: 'READING', page: 2, pageSize: 10 }),
    )
    expect(
      client.getQueryData(feedKeys.page(readerA.id, 'READING', 2, 10)),
    ).toEqual(emptyPage)

    mocks.auth.user = readerB
    view.rerender({ type: 'CLUB', page: 3 })

    await waitFor(() =>
      expect(mocks.feed).toHaveBeenLastCalledWith({ type: 'CLUB', page: 3, pageSize: 10 }),
    )
    expect(client.getQueryData(feedKeys.page(readerB.id, 'CLUB', 3, 10))).toEqual(
      emptyPage,
    )
    expect(client.getQueryData(feedKeys.page(readerA.id, 'READING', 2, 10))).toEqual(
      emptyPage,
    )
  })
})
