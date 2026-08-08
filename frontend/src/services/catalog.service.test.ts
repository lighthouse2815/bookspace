import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../types/api'
import type { Author, Category } from '../types/domain'
import { catalogService } from './catalog.service'

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}))

vi.mock('../lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mocks.get(...args),
    put: (...args: unknown[]) => mocks.put(...args),
    delete: (...args: unknown[]) => mocks.delete(...args),
  },
  unwrap: (response: { data: { data: unknown } }) => response.data.data,
}))

function categoryPage(items: Category[], page: number, totalPages: number): PageResult<Category> {
  return {
    items,
    page,
    pageSize: 100,
    totalItems: 3,
    totalPages,
  }
}

describe('catalog category lookup', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('aggregates every API page into the existing data.items contract', async () => {
    const pages = new Map([
      [1, categoryPage([{ id: 'category-1', name: 'Văn học' }], 1, 3)],
      [2, categoryPage([{ id: 'category-2', name: 'Lịch sử' }], 2, 3)],
      [3, categoryPage([{ id: 'category-3', name: 'Khoa học' }], 3, 3)],
    ])
    mocks.get.mockImplementation(async (_path, options: { params: { page: number } }) => ({
      data: { data: pages.get(options.params.page) },
    }))

    const result = await catalogService.categories()

    expect(mocks.get).toHaveBeenCalledTimes(3)
    expect(mocks.get).toHaveBeenNthCalledWith(1, '/categories', {
      params: { page: 1, pageSize: 100 },
    })
    expect(mocks.get).toHaveBeenNthCalledWith(2, '/categories', {
      params: { page: 2, pageSize: 100 },
    })
    expect(mocks.get).toHaveBeenNthCalledWith(3, '/categories', {
      params: { page: 3, pageSize: 100 },
    })
    expect(result.items.map((category) => category.id)).toEqual([
      'category-1',
      'category-2',
      'category-3',
    ])
    expect(result).toMatchObject({ page: 1, pageSize: 3, totalItems: 3, totalPages: 1 })
  })

  it('deduplicates a category repeated while pages are being read', async () => {
    mocks.get.mockImplementation(async (_path, options: { params: { page: number } }) => ({
      data: {
        data:
          options.params.page === 1
            ? categoryPage([{ id: 'category-1', name: 'Văn học' }], 1, 2)
            : categoryPage(
                [
                  { id: 'category-1', name: 'Văn học' },
                  { id: 'category-2', name: 'Lịch sử' },
                ],
                2,
                2,
              ),
      },
    }))

    const result = await catalogService.categories()

    expect(result.items.map((category) => category.id)).toEqual(['category-1', 'category-2'])
    expect(result.totalItems).toBe(2)
  })
})

describe('catalog author lookup', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads every page so admin book forms can use more than 100 authors', async () => {
    const pages = new Map<number, PageResult<Author>>([
      [
        1,
        {
          items: [{ id: 'author-1', name: 'Tác giả 1' }],
          page: 1,
          pageSize: 100,
          totalItems: 2,
          totalPages: 2,
        },
      ],
      [
        2,
        {
          items: [{ id: 'author-2', name: 'Tác giả 2' }],
          page: 2,
          pageSize: 100,
          totalItems: 2,
          totalPages: 2,
        },
      ],
    ])
    mocks.get.mockImplementation(async (_path, options: { params: { page: number } }) => ({
      data: { data: pages.get(options.params.page) },
    }))

    const result = await catalogService.authors()

    expect(mocks.get).toHaveBeenCalledTimes(2)
    expect(mocks.get).toHaveBeenNthCalledWith(2, '/authors', {
      params: { page: 2, pageSize: 100 },
    })
    expect(result.items.map((author) => author.id)).toEqual(['author-1', 'author-2'])
  })
})

describe('public catalog metadata details', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads one author and one category from their public detail contracts', async () => {
    mocks.get
      .mockResolvedValueOnce({
        data: { data: { id: 'author-1', name: 'Ursula K. Le Guin', bookCount: 4 } },
      })
      .mockResolvedValueOnce({
        data: { data: { id: 'category-1', name: 'Khoa học viễn tưởng', bookCount: 6 } },
      })

    await catalogService.author('author-1')
    await catalogService.category('category-1')

    expect(mocks.get).toHaveBeenNthCalledWith(1, '/authors/author-1')
    expect(mocks.get).toHaveBeenNthCalledWith(2, '/categories/category-1')
  })
})

describe('catalog discovery v2 contracts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('passes public directory search, sort, and pagination to metadata endpoints', async () => {
    mocks.get.mockResolvedValue({
      data: {
        data: {
          items: [],
          page: 2,
          pageSize: 12,
          totalItems: 0,
          totalPages: 0,
        },
      },
    })

    const query = { search: 'văn học', sort: 'bookCount' as const, page: 2, pageSize: 12 }
    await catalogService.authorDirectory(query)
    await catalogService.categoryDirectory(query)

    expect(mocks.get).toHaveBeenNthCalledWith(1, '/authors', { params: query })
    expect(mocks.get).toHaveBeenNthCalledWith(2, '/categories', { params: query })
  })

  it('loads a bounded related-book list for one book', async () => {
    mocks.get.mockResolvedValue({ data: { data: [] } })

    await catalogService.relatedBooks('book-1', 4)

    expect(mocks.get).toHaveBeenCalledWith('/books/book-1/related', {
      params: { limit: 4 },
    })
  })
})

describe('catalog following contracts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.get.mockResolvedValue({ data: { data: { authors: [], categories: [] } } })
    mocks.put.mockResolvedValue({ data: { data: null } })
    mocks.delete.mockResolvedValue({ data: { data: null } })
  })

  it('uses current-user endpoints for following authors and categories', async () => {
    await catalogService.following()
    await catalogService.followAuthor('author-1')
    await catalogService.unfollowAuthor('author-1')
    await catalogService.followCategory('category-1')
    await catalogService.unfollowCategory('category-1')

    expect(mocks.get).toHaveBeenCalledWith('/catalog-follows')
    expect(mocks.put).toHaveBeenNthCalledWith(1, '/catalog-follows/authors/author-1')
    expect(mocks.delete).toHaveBeenNthCalledWith(1, '/catalog-follows/authors/author-1')
    expect(mocks.put).toHaveBeenNthCalledWith(2, '/catalog-follows/categories/category-1')
    expect(mocks.delete).toHaveBeenNthCalledWith(2, '/catalog-follows/categories/category-1')
  })
})
