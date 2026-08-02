import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ContentReport } from '../../types/domain'
import { AdminModerationPage } from './AdminModerationPage'

const mocks = vi.hoisted(() => ({
  reports: vi.fn(),
  resolve: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../services/moderation.service', () => ({
  moderationService: {
    reports: mocks.reports,
    resolve: mocks.resolve,
  },
}))

const report: ContentReport = {
  id: 'report-1',
  reporter: { id: 'reader-1', displayName: 'Người báo cáo', role: 'USER' },
  targetType: 'REVIEW',
  targetId: 'review-1',
  targetOwner: { id: 'author-1', displayName: 'Tác giả', role: 'USER' },
  reason: 'HARASSMENT',
  details: 'Nội dung công kích.',
  targetPreview: 'Snapshot đánh giá cần xử lý.',
  targetLink: '/books/book-1',
  status: 'PENDING',
  action: 'NONE',
  moderator: null,
  resolutionNote: null,
  resolvedAt: null,
  createdAt: '2026-08-02T08:00:00Z',
}

function renderPage(item: ContentReport = report) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  mocks.reports.mockResolvedValue({
    items: [item],
    page: 1,
    pageSize: 20,
    totalItems: 1,
    totalPages: 1,
  })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/admin/moderation']}>
        <AdminModerationPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('AdminModerationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.resolve.mockResolvedValue({ ...report, status: 'RESOLVED' })
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  it('loads the pending queue and removes reported content with an audit note', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Snapshot đánh giá cần xử lý.')).toBeInTheDocument()
    await user.type(screen.getByLabelText(/Ghi chú quyết định/), 'Đã xác minh vi phạm.')
    await user.click(screen.getByRole('button', { name: 'Ẩn nội dung' }))

    await waitFor(() =>
      expect(mocks.resolve).toHaveBeenCalledWith('report-1', {
        status: 'RESOLVED',
        action: 'CONTENT_REMOVED',
        resolutionNote: 'Đã xác minh vi phạm.',
      }),
    )
  })

  it('only offers account-level action for a profile report', async () => {
    renderPage({ ...report, targetType: 'USER', targetId: 'author-1', targetLink: '/users/author-1' })

    expect(await screen.findByText('Snapshot đánh giá cần xử lý.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ẩn nội dung' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Khóa tài khoản' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bác bỏ' })).toBeInTheDocument()
  })
})
