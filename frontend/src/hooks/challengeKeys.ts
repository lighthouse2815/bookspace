export type ChallengeViewerScope = 'guest' | `user:${string}`

export function challengeViewerScope(userId?: string | null): ChallengeViewerScope {
  return userId ? `user:${userId}` : 'guest'
}

export const challengeKeys = {
  all: ['challenges'] as const,
  viewer: (scope: ChallengeViewerScope) => [...challengeKeys.all, 'viewer', scope] as const,
  lists: (scope: ChallengeViewerScope) =>
    [...challengeKeys.viewer(scope), 'list'] as const,
  detail: (scope: ChallengeViewerScope, id: string) =>
    [...challengeKeys.viewer(scope), 'detail', id] as const,
  leaderboards: (scope: ChallengeViewerScope, id: string) =>
    [...challengeKeys.detail(scope, id), 'leaderboard'] as const,
  leaderboard: (
    scope: ChallengeViewerScope,
    id: string,
    page: number,
    pageSize: number,
  ) => [...challengeKeys.leaderboards(scope, id), page, pageSize] as const,
}
