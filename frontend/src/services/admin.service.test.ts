import { beforeEach, describe, expect, it, vi } from 'vitest'
import { adminService, type ExternalBookImportInput } from './admin.service'

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}))

vi.mock('../lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mocks.get(...args),
    post: (...args: unknown[]) => mocks.post(...args),
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
