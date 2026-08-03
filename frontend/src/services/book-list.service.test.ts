import { beforeEach, describe, expect, it, vi } from 'vitest'
import { bookListService } from './book-list.service'

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  patch: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}))

vi.mock('../lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mocks.get(...args),
    post: (...args: unknown[]) => mocks.post(...args),
    patch: (...args: unknown[]) => mocks.patch(...args),
    put: (...args: unknown[]) => mocks.put(...args),
    delete: (...args: unknown[]) => mocks.delete(...args),
  },
  unwrap: (response: { data: { data: unknown } }) => response.data.data,
}))

describe('book list service', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    const response = { data: { data: { id: 'list-1', items: [] } } }
    for (const mock of Object.values(mocks)) mock.mockResolvedValue(response)
  })

  it('uses mine and public collection contracts', async () => {
    await bookListService.mine({ page: 2, visibility: 'PRIVATE', bookId: 'book-1' })
    await bookListService.publicByUser('user-1', 3, 12)

    expect(mocks.get).toHaveBeenNthCalledWith(1, '/book-lists', {
      params: { page: 2, pageSize: 20, visibility: 'PRIVATE', bookId: 'book-1' },
    })
    expect(mocks.get).toHaveBeenNthCalledWith(2, '/users/user-1/book-lists', {
      params: { page: 3, pageSize: 12 },
    })
  })

  it('uses owner mutation and ordering contracts', async () => {
    const input = { name: 'Mùa mưa', description: null, visibility: 'PUBLIC' as const }
    await bookListService.create(input)
    await bookListService.update('list-1', input)
    await bookListService.addBook('list-1', 'book-1')
    await bookListService.removeBook('list-1', 'book-1')
    await bookListService.reorder('list-1', ['book-2', 'book-1'])
    await bookListService.delete('list-1')

    expect(mocks.post).toHaveBeenNthCalledWith(1, '/book-lists', input)
    expect(mocks.patch).toHaveBeenCalledWith('/book-lists/list-1', input)
    expect(mocks.post).toHaveBeenNthCalledWith(2, '/book-lists/list-1/books', { bookId: 'book-1' })
    expect(mocks.delete).toHaveBeenNthCalledWith(1, '/book-lists/list-1/books/book-1')
    expect(mocks.put).toHaveBeenCalledWith('/book-lists/list-1/books/reorder', {
      bookIds: ['book-2', 'book-1'],
    })
    expect(mocks.delete).toHaveBeenNthCalledWith(2, '/book-lists/list-1')
  })
})
