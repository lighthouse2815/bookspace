import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdminDashboard } from '../../types/domain'
import { AdminDashboardPage } from './AdminDashboardPage'

const mocks = vi.hoisted(() => ({ dashboard: vi.fn() }))

vi.mock('../../services/admin.service', () => ({
  adminService: { dashboard: mocks.dashboard },
}))

const dashboard: AdminDashboard = {
  generatedAt: '2026-08-10T02:00:00Z',
  totalUsers: 128,
  lockedUsers: 2,
  newUsersLast30Days: 19,
  activeReadersLast30Days: 74,
  totalBooks: 312,
  totalAuthors: 146,
  totalCategories: 18,
  totalReviews: 842,
  totalClubs: 27,
  publishedChallenges: 9,
  activeChallenges: 4,
  pendingReports: 3,
  readingSessionsLast30Days: 436,
  pagesReadLast30Days: 18427,
  readingMinutesLast30Days: 9612,
  activityLast7Days: Array.from({ length: 7 }, (_, index) => ({
    date: `2026-08-${String(index + 4).padStart(2, '0')}`,
    newUsers: index,
    readingSessions: index * 3,
    pagesRead: index * 120,
  })),
  recentUsers: [
    {
      id: 'reader-1',
      displayName: 'Mai Phương',
      role: 'USER',
      isLocked: false,
      joinedAt: '2026-08-10T01:30:00Z',
    },
  ],
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/admin']}>
        <AdminDashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('AdminDashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.dashboard.mockResolvedValue(dashboard)
  })

  it('shows operational metrics, moderation work and recent accounts', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Tổng quan quản trị' })).toBeInTheDocument()
    expect(screen.getByText('128')).toBeInTheDocument()
    expect(screen.getByText('3 báo cáo cần xem xét')).toBeInTheDocument()
    expect(screen.getByText('Mai Phương')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Mở hàng đợi/ })).toHaveAttribute(
      'href',
      '/admin/moderation',
    )
  })
})
