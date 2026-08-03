import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PageResult } from '../types/api'
import type { Category } from '../types/domain'
import { catalogService } from './catalog.service'

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
}))

vi.mock('../lib/api', () => ({
  api: { get: (...args: unknown[]) => mocks.get(...args) },
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
