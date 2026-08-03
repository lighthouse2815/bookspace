import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type { Author, Book, BookRecommendation, Category } from '../types/domain'

export interface BookQuery {
  search?: string
  categoryId?: string
  authorId?: string
  sort?: string
  page?: number
  pageSize?: number
}

export interface RecommendationQuery {
  page?: number
  pageSize?: number
}

const CATALOG_LOOKUP_PAGE_SIZE = 100

async function loadAllCategories() {
  const firstPage = unwrap(
    await api.get<ApiEnvelope<PageResult<Category>>>('/categories', {
      params: { page: 1, pageSize: CATALOG_LOOKUP_PAGE_SIZE },
    }),
  )

  const remainingPages = Math.max(0, firstPage.totalPages - 1)
  const pages = remainingPages
    ? await Promise.all(
        Array.from({ length: remainingPages }, async (_, index) =>
          unwrap(
            await api.get<ApiEnvelope<PageResult<Category>>>('/categories', {
              params: { page: index + 2, pageSize: CATALOG_LOOKUP_PAGE_SIZE },
            }),
          ),
        ),
      )
    : []

  const categories = new Map<string, Category>()
  for (const category of [firstPage, ...pages].flatMap((page) => page.items)) {
    if (!categories.has(category.id)) categories.set(category.id, category)
  }

  const items = Array.from(categories.values())
  return {
    items,
    page: 1,
    pageSize: items.length || CATALOG_LOOKUP_PAGE_SIZE,
    totalItems: items.length,
    totalPages: items.length ? 1 : 0,
  } satisfies PageResult<Category>
}

export const catalogService = {
  books: async (params: BookQuery = {}) =>
    unwrap(await api.get<ApiEnvelope<PageResult<Book>>>('/books', { params })),

  featured: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Book>>>('/books', {
        params: { sort: 'popular', page: 1, pageSize: 8 },
      }),
    ),

  book: async (id: string) => unwrap(await api.get<ApiEnvelope<Book>>(`/books/${id}`)),

  recommendations: async (params: RecommendationQuery = {}) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<BookRecommendation>>>('/books/recommendations', {
        params,
      }),
    ),

  authors: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Author>>>('/authors', {
        params: { page: 1, pageSize: 100 },
      }),
    ),

  categories: loadAllCategories,
}
