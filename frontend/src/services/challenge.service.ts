import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type { Challenge, ChallengeLeaderboardItem } from '../types/domain'

export const challengeService = {
  challenges: async () =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Challenge>>>('/challenges', {
        params: { page: 1, pageSize: 50 },
      }),
    ),

  detail: async (id: string) =>
    unwrap(await api.get<ApiEnvelope<Challenge>>(`/challenges/${id}`)),

  leaderboard: async (id: string, page: number, pageSize: number) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ChallengeLeaderboardItem>>>(
        `/challenges/${id}/leaderboard`,
        { params: { page, pageSize } },
      ),
    ),

  join: async (id: string) =>
    unwrap(await api.post<ApiEnvelope<Challenge>>(`/challenges/${id}/join`)),

  leave: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<Challenge>>(`/challenges/${id}/join`)),
}
