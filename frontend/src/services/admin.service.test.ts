import { beforeEach, describe, expect, it, vi } from 'vitest'
import { adminService, type ExternalBookImportInput } from './admin.service'

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}))

vi.mock('../lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mocks.get(...args),
    post: (...args: unknown[]) => mocks.post(...args),
    patch: (...args: unknown[]) => mocks.patch(...args),
    delete: (...args: unknown[]) => mocks.delete(...args),
  },
  unwrap: (response: { data: { data: unknown } }) => response.data.data,
}))

describe('admin external catalog service', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('uses the controlled external search endpoint', async () => {
    mocks.get.mockResolvedValue({
      data: { data: { available: false, provider: 'bookstore', message: 'Đang tắt.', items: [] } },
    })

    await adminService.searchExternalBooks('clean code')

    expect(mocks.get).toHaveBeenCalledWith('/external-books/search', {
      params: { query: 'clean code', limit: 12 },
    })
  })

  it('posts the reviewed import payload to the admin contract', async () => {
    const input: ExternalBookImportInput = {
      provider: 'bookstore',
      externalId: 'external-1',
      authorName: 'Nguyễn Minh An',
      categoryIds: [],
      categoryNames: ['Công nghệ'],
      pageCount: 320,
      language: 'vi',
    }
    mocks.post.mockResolvedValue({
      data: {
        data: {
          status: 'IMPORTED',
          provider: 'bookstore',
          externalId: 'external-1',
          book: { id: 'book-1', title: 'Clean Code' },
        },
      },
    })

    await adminService.importExternalBook(input)

    expect(mocks.post).toHaveBeenCalledWith('/admin/books/import', input)
  })
})

describe('admin catalog metadata service', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    const response = { data: { data: null } }
    mocks.get.mockResolvedValue(response)
    mocks.post.mockResolvedValue(response)
    mocks.patch.mockResolvedValue(response)
    mocks.delete.mockResolvedValue(response)
  })

  it('sends search and paging to the protected author list', async () => {
    await adminService.authors({ search: 'Nguyễn', page: 2, pageSize: 20 })

    expect(mocks.get).toHaveBeenCalledWith('/admin/authors', {
      params: { search: 'Nguyễn', page: 2, pageSize: 20 },
    })
  })

  it('uses the author and category mutation contracts', async () => {
    const author = { name: 'Ursula K. Le Guin', biography: 'Nhà văn.' }
    const category = { name: 'Khoa học viễn tưởng', description: 'Tác phẩm giả tưởng.' }

    await adminService.createAuthor(author)
    await adminService.updateAuthor('author-1', author)
    await adminService.deleteAuthor('author-1')
    await adminService.createCategory(category)
    await adminService.updateCategory('category-1', category)
    await adminService.deleteCategory('category-1')

    expect(mocks.post).toHaveBeenCalledWith('/admin/authors', author)
    expect(mocks.patch).toHaveBeenCalledWith('/admin/authors/author-1', author)
    expect(mocks.delete).toHaveBeenCalledWith('/admin/authors/author-1')
    expect(mocks.post).toHaveBeenCalledWith('/admin/categories', category)
    expect(mocks.patch).toHaveBeenCalledWith('/admin/categories/category-1', category)
    expect(mocks.delete).toHaveBeenCalledWith('/admin/categories/category-1')
  })
})
