import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminAuthorsPage, AdminCategoriesPage } from './AdminCatalogMetadataPages'

const mocks = vi.hoisted(() => ({
  authors: vi.fn(),
  categories: vi.fn(),
  createAuthor: vi.fn(),
  updateAuthor: vi.fn(),
  deleteAuthor: vi.fn(),
  createCategory: vi.fn(),
  updateCategory: vi.fn(),
  deleteCategory: vi.fn(),
  toast: vi.fn(),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: mocks.toast }),
}))

vi.mock('../../services/admin.service', () => ({
  adminService: {
    authors: mocks.authors,
    categories: mocks.categories,
    createAuthor: mocks.createAuthor,
    updateAuthor: mocks.updateAuthor,
    deleteAuthor: mocks.deleteAuthor,
    createCategory: mocks.createCategory,
    updateCategory: mocks.updateCategory,
    deleteCategory: mocks.deleteCategory,
  },
}))

function page<T>(items: T[], currentPage = 1, totalPages = 1) {
  return {
    items,
    page: currentPage,
    pageSize: 20,
    totalItems: items.length,
    totalPages,
  }
}

function renderPage(element: React.ReactNode, path: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>{element}</MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('AdminCatalogMetadataPages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.authors.mockResolvedValue(
      page([
        {
          id: 'author-unused',
          name: 'Ursula K. Le Guin',
          biography: 'Nhà văn khoa học viễn tưởng.',
          bookCount: 0,
        },
        {
          id: 'author-used',
          name: 'Octavia E. Butler',
          biography: 'Tác giả Kindred.',
          bookCount: 3,
        },
      ]),
    )
    mocks.categories.mockResolvedValue(page([]))
    mocks.createAuthor.mockResolvedValue({ id: 'author-new', name: 'Tác giả mới', bookCount: 0 })
    mocks.createCategory.mockResolvedValue({
      id: 'category-new',
      name: 'Khí hậu viễn tưởng',
      bookCount: 0,
    })
    mocks.deleteAuthor.mockResolvedValue(null)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    vi.stubGlobal('scrollTo', vi.fn())
  })

  it('keeps author search and paging in the URL-backed request', async () => {
    renderPage(<AdminAuthorsPage />, '/admin/authors?search=Ursula&page=2')

    expect(await screen.findByText('Ursula K. Le Guin')).toBeInTheDocument()
    expect(mocks.authors).toHaveBeenCalledWith({ search: 'Ursula', page: 2, pageSize: 20 })
  })

  it('creates an author and prevents deletion while books are attached', async () => {
    const user = userEvent.setup()
    renderPage(<AdminAuthorsPage />, '/admin/authors')
    await screen.findByText('Ursula K. Le Guin')

    expect(screen.getByRole('button', { name: 'Xóa Octavia E. Butler' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Xóa Ursula K. Le Guin' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'Thêm tác giả' }))
    await user.type(screen.getByLabelText('Tên tác giả'), '  Tác giả mới  ')
    await user.type(screen.getByLabelText('Tiểu sử'), '  Hồ sơ mới.  ')
    await user.click(screen.getByRole('button', { name: 'Thêm tác giả' }))

    await waitFor(() =>
      expect(mocks.createAuthor).toHaveBeenCalledWith({
        name: 'Tác giả mới',
        biography: 'Hồ sơ mới.',
        avatarUrl: undefined,
      }),
    )
    expect(mocks.toast).toHaveBeenCalledWith('Đã thêm tác giả vào catalog', 'success')

    await user.click(screen.getByRole('button', { name: 'Xóa Ursula K. Le Guin' }))
    await waitFor(() => expect(mocks.deleteAuthor).toHaveBeenCalledWith('author-unused'))
  })

  it('creates a category with its description', async () => {
    const user = userEvent.setup()
    renderPage(<AdminCategoriesPage />, '/admin/categories')

    await screen.findByText('Chưa có thể loại phù hợp')
    await user.click(screen.getAllByRole('button', { name: 'Thêm thể loại' })[0])
    await user.type(screen.getByLabelText('Tên thể loại'), 'Khí hậu viễn tưởng')
    await user.type(screen.getByLabelText('Mô tả'), 'Tiểu thuyết về biến đổi khí hậu.')
    await user.click(screen.getAllByRole('button', { name: 'Thêm thể loại' })[0])

    await waitFor(() =>
      expect(mocks.createCategory).toHaveBeenCalledWith({
        name: 'Khí hậu viễn tưởng',
        description: 'Tiểu thuyết về biến đổi khí hậu.',
      }),
    )
  })
})
