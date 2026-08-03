import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  BookListDetail,
  BookListSummary,
  BookListVisibility,
} from '../types/domain'

export interface BookListInput {
  name: string
  description?: string | null
  visibility: BookListVisibility
}

export const bookListService = {
  mine: async ({
    page = 1,
    pageSize = 20,
    visibility,
    bookId,
  }: {
    page?: number
    pageSize?: number
    visibility?: BookListVisibility
    bookId?: string
  } = {}) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<BookListSummary>>>('/book-lists', {
        params: { page, pageSize, visibility, bookId },
      }),
    ),

  publicByUser: async (userId: string, page = 1, pageSize = 20) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<BookListSummary>>>(
        `/users/${userId}/book-lists`,
        { params: { page, pageSize } },
      ),
    ),

  detail: async (listId: string) =>
    unwrap(await api.get<ApiEnvelope<BookListDetail>>(`/book-lists/${listId}`)),

  create: async (input: BookListInput) =>
    unwrap(await api.post<ApiEnvelope<BookListDetail>>('/book-lists', input)),

  update: async (listId: string, input: BookListInput) =>
    unwrap(await api.patch<ApiEnvelope<BookListDetail>>(`/book-lists/${listId}`, input)),

  delete: async (listId: string) => {
    await api.delete(`/book-lists/${listId}`)
  },

  addBook: async (listId: string, bookId: string) =>
    unwrap(
      await api.post<ApiEnvelope<BookListDetail>>(`/book-lists/${listId}/books`, {
        bookId,
      }),
    ),

  removeBook: async (listId: string, bookId: string) =>
    unwrap(
      await api.delete<ApiEnvelope<BookListDetail>>(
        `/book-lists/${listId}/books/${bookId}`,
      ),
    ),

  reorder: async (listId: string, bookIds: string[]) =>
    unwrap(
      await api.put<ApiEnvelope<BookListDetail>>(`/book-lists/${listId}/books/reorder`, {
        bookIds,
      }),
    ),
}
