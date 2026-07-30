import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type { Book, Challenge } from '../types/domain'

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

export const adminService = {
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
