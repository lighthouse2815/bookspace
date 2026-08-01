import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../types/api'
import type { BookRecommendation, User } from '../types/domain'
import { recommendationKeys } from './recommendationKeys'
import { useBookRecommendations } from './useCatalog'

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

const emptyPage: PageResult<BookRecommendation> = {
  items: [],
  page: 2,
  pageSize: 12,
  totalItems: 0,
  totalPages: 0,
}

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isLoading: false,
  },
  recommendations: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mocks.auth,
}))

vi.mock('../services/catalog.service', () => ({
  catalogService: {
    recommendations: (...args: unknown[]) => mocks.recommendations(...args),
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

describe('book recommendation query ownership', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.auth.user = null
    mocks.auth.isLoading = false
    mocks.recommendations.mockResolvedValue(emptyPage)
  })

  it('does not request personalized recommendations for a guest', () => {
    const client = createQueryClient()
    renderHook(() => useBookRecommendations({ page: 2, pageSize: 12 }), {
      wrapper: ({ children }) => <Providers client={client}>{children}</Providers>,
    })

    expect(mocks.recommendations).not.toHaveBeenCalled()
    expect(client.getQueryData(recommendationKeys.page('guest', 2, 12))).toBeUndefined()
  })

  it('scopes cached pages to the authenticated principal', async () => {
    mocks.auth.user = readerA
    const client = createQueryClient()
    const view = renderHook(() => useBookRecommendations({ page: 2, pageSize: 12 }), {
      wrapper: ({ children }) => <Providers client={client}>{children}</Providers>,
    })

    await waitFor(() =>
      expect(mocks.recommendations).toHaveBeenCalledWith({ page: 2, pageSize: 12 }),
    )
    expect(client.getQueryData(recommendationKeys.page(readerA.id, 2, 12))).toEqual(
      emptyPage,
    )

    mocks.auth.user = readerB
    view.rerender()

    await waitFor(() => expect(mocks.recommendations).toHaveBeenCalledTimes(2))
    expect(client.getQueryData(recommendationKeys.page(readerB.id, 2, 12))).toEqual(
      emptyPage,
    )
    expect(client.getQueryData(recommendationKeys.page(readerA.id, 2, 12))).toEqual(
      emptyPage,
    )
  })
})
