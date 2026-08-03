import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { OnboardingState, User } from '../types/domain'
import { recommendationKeys } from './recommendationKeys'
import {
  onboardingKeys,
  useOnboarding,
  useSaveOnboardingPreferences,
} from './useOnboarding'

const readerA: User = { id: 'reader-a', displayName: 'Độc giả A', role: 'USER' }
const readerB: User = { id: 'reader-b', displayName: 'Độc giả B', role: 'USER' }

const stateA: OnboardingState = {
  status: 'PENDING',
  finishedAt: null,
  preferredCategoryIds: ['category-1', 'category-2', 'category-3'],
  referenceBookIds: [],
}

const mocks = vi.hoisted(() => ({
  auth: {
    user: null as User | null,
    isLoading: false,
  },
  state: vi.fn(),
  save: vi.fn(),
}))

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    ...mocks.auth,
    isAuthenticated: Boolean(mocks.auth.user),
  }),
}))

vi.mock('../services/onboarding.service', () => ({
  onboardingService: {
    state: (...args: unknown[]) => mocks.state(...args),
    savePreferences: (...args: unknown[]) => mocks.save(...args),
    complete: vi.fn(),
    skip: vi.fn(),
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

describe('onboarding query ownership', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.auth.user = readerA
    mocks.auth.isLoading = false
    mocks.state.mockImplementation(async () => ({
      ...stateA,
      preferredCategoryIds: [mocks.auth.user?.id ?? 'guest'],
    }))
    mocks.save.mockResolvedValue(stateA)
  })

  it('keeps onboarding state in a cache owned by the authenticated principal', async () => {
    const client = createQueryClient()
    const view = renderHook(() => useOnboarding(), {
      wrapper: ({ children }) => <Providers client={client}>{children}</Providers>,
    })

    await waitFor(() => expect(client.getQueryData(onboardingKeys.principal(readerA.id))).toBeDefined())
    expect(client.getQueryData<OnboardingState>(onboardingKeys.principal(readerA.id))?.preferredCategoryIds).toEqual([
      readerA.id,
    ])

    mocks.auth.user = readerB
    view.rerender()

    await waitFor(() => expect(mocks.state).toHaveBeenCalledTimes(2))
    expect(client.getQueryData<OnboardingState>(onboardingKeys.principal(readerB.id))?.preferredCategoryIds).toEqual([
      readerB.id,
    ])
    expect(client.getQueryData(onboardingKeys.principal(readerA.id))).toBeDefined()
  })

  it('stores the saved state and invalidates every personalized consumer', async () => {
    const client = createQueryClient()
    const recommendationKey = recommendationKeys.page(readerA.id, 1, 6)
    const keys = [
      recommendationKey,
      ['library'],
      ['people', readerA.id, 'suggestions'],
      ['dashboard'],
      ['reading-goals'],
    ] as const
    keys.forEach((key) => client.setQueryData(key, { cached: true }))

    const view = renderHook(() => useSaveOnboardingPreferences(), {
      wrapper: ({ children }) => <Providers client={client}>{children}</Providers>,
    })

    await act(async () => {
      await view.result.current.mutateAsync({
        preferredCategoryIds: stateA.preferredCategoryIds,
        referenceBookIds: stateA.referenceBookIds,
      })
    })

    expect(client.getQueryData(onboardingKeys.principal(readerA.id))).toEqual(stateA)
    keys.forEach((key) => expect(client.getQueryState(key)?.isInvalidated).toBe(true))
  })
})
