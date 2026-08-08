import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  Author,
  Book,
  BookRecommendation,
  CatalogFollowing,
  Category,
} from '../types/domain'

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

export interface MetadataDirectoryQuery {
  search?: string
  sort?: 'name' | 'bookCount'
  page?: number
  pageSize?: number
}

const CATALOG_LOOKUP_PAGE_SIZE = 100

async function loadAllLookup<T>(path: '/authors' | '/categories') {
  const firstPage = unwrap(
    await api.get<ApiEnvelope<PageResult<T>>>(path, {
      params: { page: 1, pageSize: CATALOG_LOOKUP_PAGE_SIZE },
    }),
  )

  const remainingPages = Math.max(0, firstPage.totalPages - 1)
  const pages = remainingPages
    ? await Promise.all(
        Array.from({ length: remainingPages }, async (_, index) =>
          unwrap(
            await api.get<ApiEnvelope<PageResult<T>>>(path, {
              params: { page: index + 2, pageSize: CATALOG_LOOKUP_PAGE_SIZE },
            }),
          ),
        ),
      )
    : []

  const lookup = new Map<string, T>()
  for (const item of [firstPage, ...pages].flatMap((page) => page.items)) {
    const id = (item as { id: string }).id
    if (!lookup.has(id)) lookup.set(id, item)
  }

  const items = Array.from(lookup.values())
  return {
    items,
    page: 1,
    pageSize: items.length || CATALOG_LOOKUP_PAGE_SIZE,
    totalItems: items.length,
    totalPages: items.length ? 1 : 0,
  } satisfies PageResult<T>
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

  relatedBooks: async (id: string, limit = 4) =>
    unwrap(
      await api.get<ApiEnvelope<Book[]>>(`/books/${id}/related`, {
        params: { limit },
      }),
    ),

  author: async (id: string) =>
    unwrap(await api.get<ApiEnvelope<Author>>(`/authors/${id}`)),

  category: async (id: string) =>
    unwrap(await api.get<ApiEnvelope<Category>>(`/categories/${id}`)),

  recommendations: async (params: RecommendationQuery = {}) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<BookRecommendation>>>('/books/recommendations', {
        params,
      }),
    ),

  authorDirectory: async (params: MetadataDirectoryQuery = {}) =>
    unwrap(await api.get<ApiEnvelope<PageResult<Author>>>('/authors', { params })),

  categoryDirectory: async (params: MetadataDirectoryQuery = {}) =>
    unwrap(await api.get<ApiEnvelope<PageResult<Category>>>('/categories', { params })),

  authors: () => loadAllLookup<Author>('/authors'),

  categories: () => loadAllLookup<Category>('/categories'),

  following: async () =>
    unwrap(await api.get<ApiEnvelope<CatalogFollowing>>('/catalog-follows')),

  followAuthor: async (id: string) =>
    unwrap(await api.put<ApiEnvelope<null>>(`/catalog-follows/authors/${id}`)),

  unfollowAuthor: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/catalog-follows/authors/${id}`)),

  followCategory: async (id: string) =>
    unwrap(await api.put<ApiEnvelope<null>>(`/catalog-follows/categories/${id}`)),

  unfollowCategory: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/catalog-follows/categories/${id}`)),
}
