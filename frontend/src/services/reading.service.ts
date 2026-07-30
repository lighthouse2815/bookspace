import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type { LibraryEntry, ReadingSession, Shelf } from '../types/domain'

export interface LibraryInput {
  bookId: string
  shelf: Shelf
}

export interface LibraryUpdate {
  shelf?: Shelf
  currentPage?: number
  progressPercent?: number
}

export interface SessionInput {
  bookId: string
  startedAt: string
  endedAt?: string
  durationMinutes: number
  pagesRead: number
  note?: string
}

export const readingService = {
  library: async (shelf?: Shelf) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<LibraryEntry>>>('/library', {
        params: { shelf, page: 1, pageSize: 100 },
      }),
    ),

  addToLibrary: async (input: LibraryInput) =>
    unwrap(await api.post<ApiEnvelope<LibraryEntry>>('/library', input)),

  updateLibrary: async (id: string, input: LibraryUpdate) =>
    unwrap(await api.patch<ApiEnvelope<LibraryEntry>>(`/library/${id}`, input)),

  removeFromLibrary: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/library/${id}`)),

  sessions: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingSession>>>('/reading-sessions', {
        params: { page: 1, pageSize: 100 },
      }),
    ),

  createSession: async (input: SessionInput) =>
    unwrap(await api.post<ApiEnvelope<ReadingSession>>('/reading-sessions', input)),
}
