import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ToastProvider } from '../../contexts/ToastContext'
import type { ExternalBookSearchResult } from '../../types/domain'
import { ExternalBookImportPanel } from './ExternalBookImportPanel'

const mocks = vi.hoisted(() => ({
  search: vi.fn(),
  importBook: vi.fn(),
}))

vi.mock('../../services/admin.service', () => ({
  adminService: {
    searchExternalBooks: mocks.search,
    importExternalBook: mocks.importBook,
  },
}))

const availableResult: ExternalBookSearchResult = {
  available: true,
  provider: 'bookstore',
  message: 'Đã tải dữ liệu.',
  items: [
    {
      externalId: 'external-1',
      title: 'Kiến trúc phần mềm thực chiến',
      authors: ['Nguyễn Minh An'],
      coverImageUrl: 'https://images.example.test/book.jpg',
      isbn: '9781234567890',
      description: 'Mô tả từ provider.',
      pageCount: 320,
      publishedYear: 2025,
      language: 'vi',
      categories: ['Công nghệ'],
      price: 199000,
      purchaseUrl: 'https://store.example/books/external-1',
    },
  ],
}

function Providers({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <ToastProvider>{children}</ToastProvider>
    </QueryClientProvider>
  )
}

function renderPanel() {
  return render(
    <ExternalBookImportPanel
      authors={[{ id: 'author-existing', name: 'Tác giả hiện có' }]}
      categories={[{ id: 'category-existing', name: 'Kỹ năng' }]}
      onClose={vi.fn()}
    />,
    { wrapper: Providers },
  )
}

describe('ExternalBookImportPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.search.mockResolvedValue(availableResult)
    mocks.importBook.mockResolvedValue({
      status: 'IMPORTED',
      provider: 'bookstore',
      externalId: 'external-1',
      book: {
        id: 'book-1',
        title: 'Kiến trúc phần mềm thực chiến',
        language: 'vi',
      },
    })
  })

  it('searches, previews provider metadata and submits a BookSpace-owned import', async () => {
    const user = userEvent.setup()
    renderPanel()

    await user.type(screen.getByLabelText('Tìm sách từ nguồn ngoài'), 'kiến trúc phần mềm')
    await user.click(screen.getByRole('button', { name: 'Tìm metadata' }))

    expect(await screen.findByText('Kiến trúc phần mềm thực chiến')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Xem trước và import' }))

    expect(screen.getByLabelText('Tên tác giả từ nguồn')).toHaveValue('Nguyễn Minh An')
    expect(screen.getByLabelText('Số trang')).toHaveValue(320)
    expect(screen.getByLabelText('Thể loại mới')).toHaveValue('Công nghệ')

    await user.click(screen.getByRole('button', { name: 'Import vào BookSpace' }))

    await waitFor(() => expect(mocks.importBook).toHaveBeenCalledOnce())
    expect(mocks.importBook.mock.calls[0][0]).toEqual({
        provider: 'bookstore',
        externalId: 'external-1',
        authorId: undefined,
        authorName: 'Nguyễn Minh An',
        categoryIds: [],
        categoryNames: ['Công nghệ'],
        description: 'Mô tả từ provider.',
        pageCount: 320,
        publishedYear: 2025,
        language: 'vi',
      })
    expect(await screen.findByText(/Đã import “Kiến trúc phần mềm thực chiến”/)).toBeInTheDocument()
  })

  it('shows a controlled provider-disabled state without rendering fake results', async () => {
    mocks.search.mockResolvedValue({
      available: false,
      provider: 'bookstore',
      message: 'Kết nối Bookstore đang tắt. BookSpace vẫn hoạt động độc lập.',
      items: [],
    })
    const user = userEvent.setup()
    renderPanel()

    await user.type(screen.getByLabelText('Tìm sách từ nguồn ngoài'), 'clean code')
    await user.click(screen.getByRole('button', { name: 'Tìm metadata' }))

    expect(await screen.findByRole('status')).toHaveTextContent('BookSpace vẫn hoạt động độc lập')
    expect(screen.queryByRole('button', { name: 'Xem trước và import' })).not.toBeInTheDocument()
  })

  it('keeps the preview editable and blocks import when required metadata is missing', async () => {
    mocks.search.mockResolvedValue({
      ...availableResult,
      items: [
        {
          ...availableResult.items[0],
          authors: [],
          categories: [],
          pageCount: null,
        },
      ],
    })
    const user = userEvent.setup()
    renderPanel()

    await user.type(screen.getByLabelText('Tìm sách từ nguồn ngoài'), 'thiếu metadata')
    await user.click(screen.getByRole('button', { name: 'Tìm metadata' }))
    await user.click(await screen.findByRole('button', { name: 'Xem trước và import' }))
    await user.type(screen.getByLabelText('Tên tác giả từ nguồn'), 'Tác giả bổ sung')
    await user.type(screen.getByLabelText('Số trang'), '180')
    await user.click(screen.getByRole('button', { name: 'Import vào BookSpace' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Hãy chọn hoặc nhập ít nhất một thể loại')
    expect(mocks.importBook).not.toHaveBeenCalled()
  })
})
