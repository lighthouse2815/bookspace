import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  ReadingGoal,
  ReadingGoalMetric,
  ReadingGoalPeriod,
  ReadingNote,
} from '../types/domain'

export interface SaveReadingGoalInput {
  metric: ReadingGoalMetric
  period: ReadingGoalPeriod
  targetValue: number
  startDate: string
  endDate: string
}

export interface SaveReadingNoteInput {
  bookId: string
  pageNumber?: number
  quote?: string
  content?: string
  tags: string[]
}

export type UpdateReadingNoteInput = Omit<SaveReadingNoteInput, 'bookId'>

export interface ReadingNoteQuery {
  bookId?: string
  search?: string
  tag?: string
  page?: number
  pageSize?: number
}

export const readingProductService = {
  goals: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingGoal>>>('/reading-goals', {
        params: { page: 1, pageSize: 100 },
      }),
    ),

  createGoal: async (input: SaveReadingGoalInput) =>
    unwrap(await api.post<ApiEnvelope<ReadingGoal>>('/reading-goals', input)),

  updateGoal: async (id: string, input: SaveReadingGoalInput) =>
    unwrap(await api.patch<ApiEnvelope<ReadingGoal>>(`/reading-goals/${id}`, input)),

  deleteGoal: async (id: string) => unwrap(await api.delete<ApiEnvelope<null>>(`/reading-goals/${id}`)),

  notes: async (query: ReadingNoteQuery = {}) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingNote>>>('/reading-notes', {
        params: {
          bookId: query.bookId,
          search: query.search,
          tag: query.tag,
          page: query.page ?? 1,
          pageSize: query.pageSize ?? 100,
        },
      }),
    ),

  createNote: async (input: SaveReadingNoteInput) =>
    unwrap(await api.post<ApiEnvelope<ReadingNote>>('/reading-notes', input)),

  updateNote: async (id: string, input: UpdateReadingNoteInput) => {
    const { pageNumber, quote, content, tags } = input
    return unwrap(
      await api.patch<ApiEnvelope<ReadingNote>>(`/reading-notes/${id}`, {
        pageNumber,
        quote,
        content,
        tags,
      }),
    )
  },

  deleteNote: async (id: string) => unwrap(await api.delete<ApiEnvelope<null>>(`/reading-notes/${id}`)),
}
