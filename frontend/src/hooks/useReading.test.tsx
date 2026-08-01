import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { challengeKeys, challengeViewerScope } from './challengeKeys'
import { useUpdateLibrary } from './useReading'

const mocks = vi.hoisted(() => ({
  updateLibrary: vi.fn(),
  challengeQuery: vi.fn(),
  notificationsQuery: vi.fn(),
  feedQuery: vi.fn(),
}))

vi.mock('../services/reading.service', () => ({
  readingService: {
    library: vi.fn(),
    addToLibrary: vi.fn(),
    updateLibrary: mocks.updateLibrary,
    removeFromLibrary: vi.fn(),
    sessions: vi.fn(),
    createSession: vi.fn(),
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

function ReadingProgressProbe() {
  const challenge = useQuery({
    queryKey: challengeKeys.detail(
      challengeViewerScope('reader-1'),
      'challenge-123',
    ),
    queryFn: mocks.challengeQuery,
  })
  const notifications = useQuery({
    queryKey: ['notifications'],
    queryFn: mocks.notificationsQuery,
  })
  const feed = useQuery({
    queryKey: ['feed', 'reader-1', 'ALL', 1, 10],
    queryFn: mocks.feedQuery,
  })
  const update = useUpdateLibrary()

  return (
    <>
      <output data-testid="challenge-progress">
        {challenge.data?.currentBooks ?? 'Đang tải'}
      </output>
      <output data-testid="notification-count">
        {notifications.data?.length ?? 'Đang tải'}
      </output>
      <output data-testid="feed-count">{feed.data?.length ?? 'Đang tải'}</output>
      <button
        type="button"
        disabled={update.isPending}
        onClick={() =>
          update.mutate({
            id: 'library-item-1',
            input: { shelf: 'READ' },
          })
        }
      >
        Đánh dấu đã đọc
      </button>
    </>
  )
}

describe('reading mutation challenge invalidation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('refetches challenge and notification queries after marking a library item READ', async () => {
    let currentBooks = 0
    let notificationCount = 0
    let feedCount = 0
    mocks.challengeQuery.mockImplementation(async () => ({ currentBooks }))
    mocks.notificationsQuery.mockImplementation(async () =>
      Array.from({ length: notificationCount }, (_, index) => ({ id: `${index}` })),
    )
    mocks.feedQuery.mockImplementation(async () =>
      Array.from({ length: feedCount }, (_, index) => ({ id: `${index}` })),
    )
    mocks.updateLibrary.mockImplementation(async () => {
      currentBooks = 1
      notificationCount = 1
      feedCount = 1
      return { id: 'library-item-1', shelf: 'READ' }
    })
    const client = createQueryClient()
    const user = userEvent.setup()
    render(
      <Providers client={client}>
        <ReadingProgressProbe />
      </Providers>,
    )

    await waitFor(() => {
      expect(mocks.challengeQuery).toHaveBeenCalledOnce()
      expect(mocks.notificationsQuery).toHaveBeenCalledOnce()
      expect(mocks.feedQuery).toHaveBeenCalledOnce()
      expect(screen.getByTestId('challenge-progress')).toHaveTextContent('0')
    })

    await user.click(screen.getByRole('button', { name: 'Đánh dấu đã đọc' }))

    await waitFor(() => {
      expect(mocks.updateLibrary).toHaveBeenCalledWith('library-item-1', {
        shelf: 'READ',
      })
      expect(mocks.challengeQuery).toHaveBeenCalledTimes(2)
      expect(mocks.notificationsQuery).toHaveBeenCalledTimes(2)
      expect(mocks.feedQuery).toHaveBeenCalledTimes(2)
      expect(screen.getByTestId('challenge-progress')).toHaveTextContent('1')
      expect(screen.getByTestId('notification-count')).toHaveTextContent('1')
      expect(screen.getByTestId('feed-count')).toHaveTextContent('1')
    })
  })
})
