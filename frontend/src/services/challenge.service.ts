import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type { Challenge } from '../types/domain'

export const challengeService = {
  challenges: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Challenge>>>('/challenges', {
        params: { page: 1, pageSize: 50 },
      }),
    ),

  join: async (id: string) =>
    unwrap(await api.post<ApiEnvelope<Challenge>>(`/challenges/${id}/join`)),

  leave: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<Challenge>>(`/challenges/${id}/join`)),

  updateProgress: async (id: string, currentBooks: number) =>
    unwrap(
      await api.patch<ApiEnvelope<Challenge>>(`/challenges/${id}/progress`, { currentBooks }),
    ),
}
