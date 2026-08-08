import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  Author,
  Book,
  Category,
  Challenge,
  ExternalBookImportResult,
  ExternalBookSearchResult,
} from '../types/domain'

export interface AdminMetadataQuery {
  search?: string
  page?: number
  pageSize?: number
}

export interface AuthorAdminInput {
  name: string
  biography?: string
  avatarUrl?: string
}

export interface CategoryAdminInput {
  name: string
  description?: string
}

export interface BookAdminInput {
  title: string
  authorId: string
  categoryIds: string[]
  description?: string
  isbn?: string
  coverImageUrl?: string
  pageCount?: number
  publishedYear?: number
}

export interface ChallengeAdminInput {
  title: string
  description: string
  startDate: string
  endDate: string
  goalBooks: number
  coverImageUrl?: string
}

export interface ExternalBookImportInput {
  provider: string
  externalId: string
  authorId?: string
  authorName?: string
  categoryIds: string[]
  categoryNames: string[]
  description?: string
  pageCount?: number
  publishedYear?: number
  language?: string
}

export const adminService = {
  authors: async (params: AdminMetadataQuery = {}) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Author>>>('/admin/authors', { params }),
    ),

  categories: async (params: AdminMetadataQuery = {}) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Category>>>('/admin/categories', { params }),
    ),

  createAuthor: async (input: AuthorAdminInput) =>
    unwrap(await api.post<ApiEnvelope<Author>>('/admin/authors', input)),

  updateAuthor: async (id: string, input: AuthorAdminInput) =>
    unwrap(await api.patch<ApiEnvelope<Author>>(`/admin/authors/${id}`, input)),

  deleteAuthor: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/admin/authors/${id}`)),

  createCategory: async (input: CategoryAdminInput) =>
    unwrap(await api.post<ApiEnvelope<Category>>('/admin/categories', input)),

  updateCategory: async (id: string, input: CategoryAdminInput) =>
    unwrap(await api.patch<ApiEnvelope<Category>>(`/admin/categories/${id}`, input)),

  deleteCategory: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/admin/categories/${id}`)),

  challenges: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Challenge>>>('/admin/challenges', {
        params: { page: 1, pageSize: 50 },
      }),
    ),

  createBook: async (input: BookAdminInput) =>
    unwrap(await api.post<ApiEnvelope<Book>>('/admin/books', input)),

  updateBook: async (id: string, input: BookAdminInput) =>
    unwrap(await api.patch<ApiEnvelope<Book>>(`/admin/books/${id}`, input)),

  deleteBook: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/admin/books/${id}`)),

  searchExternalBooks: async (query: string) =>
    unwrap(
      await api.get<ApiEnvelope<ExternalBookSearchResult>>('/external-books/search', {
        params: { query, limit: 12 },
      }),
    ),

  importExternalBook: async (input: ExternalBookImportInput) =>
    unwrap(
      await api.post<ApiEnvelope<ExternalBookImportResult>>('/admin/books/import', input),
    ),

  createChallenge: async (input: ChallengeAdminInput) =>
    unwrap(await api.post<ApiEnvelope<Challenge>>('/admin/challenges', input)),

  updateChallenge: async (id: string, input: ChallengeAdminInput) =>
    unwrap(await api.patch<ApiEnvelope<Challenge>>(`/admin/challenges/${id}`, input)),

  publishChallenge: async (id: string, isPublished: boolean) =>
    unwrap(
      await api.patch<ApiEnvelope<Challenge>>(`/admin/challenges/${id}/publish`, {
        isPublished,
      }),
    ),

  deleteChallenge: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/admin/challenges/${id}`)),
}
