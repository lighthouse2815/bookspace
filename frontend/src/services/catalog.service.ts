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

  categories: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Category>>>('/categories', {
        params: { page: 1, pageSize: 100 },
      }),
    ),
}
