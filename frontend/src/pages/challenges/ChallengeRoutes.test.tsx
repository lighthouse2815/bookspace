import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Challenge } from '../../types/domain'
import { ChallengeDetailPage } from './ChallengeDetailPage'
import { ChallengesPage } from './ChallengesPage'

const challenge: Challenge = {
  id: 'challenge-123',
  title: 'Đọc sâu mỗi ngày',
  description: 'Một thử thách có dữ liệu thật.',
  startDate: '2026-07-01T00:00:00Z',
  endDate: '2026-07-31T23:59:59Z',
  goalBooks: 3,
  currentBooks: 2,
  participantCount: 12,
  isJoined: true,
  coverImageUrl: undefined,
  isPublished: true,
  completedAt: undefined,
}

const mocks = vi.hoisted(() => ({
  detail: vi.fn(),
  list: vi.fn(),
  membership: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ isAuthenticated: true }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../hooks/useSocialProduct', () => ({
  useChallenge: (id: string) => mocks.detail(id),
  useChallenges: () => mocks.list(),
  useChallengeMembership: (id: string, joined: boolean) => mocks.membership(id, joined),
}))

describe('challenge routes', () => {
  beforeEach(() => {
    mocks.detail.mockReturnValue({
      data: challenge,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    mocks.list.mockReturnValue({
      data: { items: [challenge], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    })
    mocks.membership.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
  })

  it('resolves a direct detail deep-link and loads the requested id', async () => {
    render(
      <MemoryRouter initialEntries={['/challenges/challenge-123']}>
        <Routes>
          <Route path="/challenges/:id" element={<ChallengeDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: 'Đọc sâu mỗi ngày' })).toBeInTheDocument()
    expect(mocks.detail).toHaveBeenCalledWith('challenge-123')
    expect(screen.getByText('2/3 cuốn đã hoàn thành')).toBeInTheDocument()
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Lưu' })).not.toBeInTheDocument()
  })

  it('links every list card to its detail route', () => {
    render(
      <MemoryRouter>
        <ChallengesPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Đọc sâu mỗi ngày' })).toHaveAttribute(
      'href',
      '/challenges/challenge-123',
    )
  })
})
