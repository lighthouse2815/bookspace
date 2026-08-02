import { useIsMutating, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { communityService, type FeedQuery } from '../services/community.service'
import type { PageResult } from '../types/api'
import type { FeedFilter, Shelf, User, UserDiscoveryItem } from '../types/domain'
import { clubChatKeys } from './clubChatKeys'
import { recommendationKeys } from './recommendationKeys'

export const viewerScope = (userId?: string | null) => userId ?? 'guest'

export const peopleKeys = {
  all: ['people'] as const,
  scope: (scope: string) => [...peopleKeys.all, scope] as const,
  searches: (scope: string) => [...peopleKeys.scope(scope), 'search'] as const,
  search: (scope: string, search: string, page: number, pageSize: number) =>
    [...peopleKeys.searches(scope), search, page, pageSize] as const,
  suggestions: (scope: string) => [...peopleKeys.scope(scope), 'suggestions'] as const,
  suggestionPage: (scope: string, page: number, pageSize: number) =>
    [...peopleKeys.suggestions(scope), page, pageSize] as const,
}

export const userKeys = {
  all: ['users'] as const,
  scope: (scope: string) => [...userKeys.all, scope] as const,
  detail: (scope: string, id: string) => [...userKeys.scope(scope), 'detail', id] as const,
  followers: (scope: string, id: string) =>
    [...userKeys.scope(scope), 'followers', id] as const,
  followerPage: (scope: string, id: string, page: number) =>
    [...userKeys.followers(scope, id), page] as const,
  following: (scope: string, id: string) =>
    [...userKeys.scope(scope), 'following', id] as const,
  followingPage: (scope: string, id: string, page: number) =>
    [...userKeys.following(scope, id), page] as const,
  library: (scope: string, id: string, shelf: Shelf | undefined, page: number, pageSize: number) =>
    [...userKeys.scope(scope), 'library', id, shelf ?? 'ALL', page, pageSize] as const,
  reviews: (scope: string, id: string, page: number, pageSize: number) =>
    [...userKeys.scope(scope), 'reviews', id, page, pageSize] as const,
  activity: (scope: string, id: string, page: number, pageSize: number) =>
    [...userKeys.scope(scope), 'activity', id, page, pageSize] as const,
}

export const feedKeys = {
  all: ['feed'] as const,
  scoped: (scope: string) => [...feedKeys.all, scope] as const,
  page: (scope: string, type: FeedFilter | undefined, page: number, pageSize: number) =>
    [...feedKeys.scoped(scope), type ?? 'ALL', page, pageSize] as const,
}

export const followKeys = {
  all: ['follow-mutation'] as const,
  target: (scope: string, targetId: string) => [...followKeys.all, scope, targetId] as const,
}

export const safetyKeys = {
  all: ['user-safety'] as const,
  scope: (scope: string) => [...safetyKeys.all, scope] as const,
  list: (scope: string, page: number, pageSize: number) =>
    [...safetyKeys.scope(scope), page, pageSize] as const,
}

export function useFeed({
  type,
  page = 1,
  pageSize = 20,
}: FeedQuery = {}) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: feedKeys.page(scope, type, page, pageSize),
    queryFn: () => communityService.feed({ type, page, pageSize }),
    enabled: Boolean(user) && !isLoading,
  })
}

export function useReviews(bookId?: string) {
  return useQuery({
    queryKey: ['reviews', bookId],
    queryFn: () => communityService.reviews(bookId!),
    enabled: Boolean(bookId),
  })
}

export function useCreateReview(bookId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { rating: number; content: string; containsSpoilers: boolean }) =>
      communityService.createReview({ bookId, ...input }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: userKeys.all })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
      void queryClient.invalidateQueries({ queryKey: recommendationKeys.all })
    },
  })
}

export function useUpdateReview(_bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      reviewId,
      ...input
    }: {
      reviewId: string
      rating: number
      content: string
      containsSpoilers: boolean
    }) => communityService.updateReview(reviewId, input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: userKeys.all })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
      void queryClient.invalidateQueries({ queryKey: recommendationKeys.all })
    },
  })
}

export function useDeleteReview(_bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reviewId: string) => communityService.deleteReview(reviewId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: userKeys.all })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
      void queryClient.invalidateQueries({ queryKey: recommendationKeys.all })
    },
  })
}

export function useLikeReview(_bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reviewId, liked }: { reviewId: string; liked: boolean }) =>
      communityService.toggleReviewLike(reviewId, liked),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: userKeys.all })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useCommentReview(_bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reviewId, content }: { reviewId: string; content: string }) =>
      communityService.comment(reviewId, content),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: userKeys.all })
      void queryClient.invalidateQueries({ queryKey: ['review-comments', variables.reviewId] })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useReviewComments(reviewId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['review-comments', reviewId],
    queryFn: () => communityService.comments(reviewId),
    enabled,
  })
}

export function useDeleteReviewComment(_bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ commentId }: { reviewId: string; commentId: string }) =>
      communityService.deleteComment(commentId),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['reviews'] })
      void queryClient.invalidateQueries({ queryKey: userKeys.all })
      void queryClient.invalidateQueries({ queryKey: ['review-comments', variables.reviewId] })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useUser(id?: string) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: userKeys.detail(scope, id ?? ''),
    queryFn: () => communityService.user(id!),
    enabled: Boolean(id) && !isLoading,
  })
}

export function useUserLibrary(
  id: string | undefined,
  shelf: Shelf | undefined,
  page: number,
  pageSize = 12,
  enabled = true,
) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: userKeys.library(scope, id ?? '', shelf, page, pageSize),
    queryFn: () => communityService.userLibrary(id!, shelf, page, pageSize),
    enabled: enabled && Boolean(id) && !isLoading,
  })
}

export function useUserReviews(
  id: string | undefined,
  page: number,
  pageSize = 10,
  enabled = true,
) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: userKeys.reviews(scope, id ?? '', page, pageSize),
    queryFn: () => communityService.userReviews(id!, page, pageSize),
    enabled: enabled && Boolean(id) && !isLoading,
  })
}

export function useUserActivity(
  id: string | undefined,
  page: number,
  pageSize = 10,
  enabled = true,
) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: userKeys.activity(scope, id ?? '', page, pageSize),
    queryFn: () => communityService.userActivity(id!, page, pageSize),
    enabled: enabled && Boolean(id) && !isLoading,
  })
}

export function useUserConnections(
  id: string | undefined,
  kind: 'followers' | 'following',
  page: number,
  enabled = true,
) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey:
      kind === 'followers'
        ? userKeys.followerPage(scope, id ?? '', page)
        : userKeys.followingPage(scope, id ?? '', page),
    queryFn: () => communityService[kind](id!, page),
    enabled: enabled && Boolean(id) && !isLoading,
  })
}

export function useUpdateProfilePrivacy(id?: string) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const scope = viewerScope(user?.id)
  return useMutation({
    mutationFn: communityService.updateProfilePrivacy,
    onSuccess: (profile) => {
      if (id) queryClient.setQueryData(userKeys.detail(scope, id), profile)
      void queryClient.invalidateQueries({ queryKey: userKeys.scope(scope) })
    },
  })
}

export function usePeopleSearch(
  search: string,
  page: number,
  pageSize = 20,
  enabled = true,
) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: peopleKeys.search(scope, search, page, pageSize),
    queryFn: () => communityService.people(search, page, pageSize),
    enabled: enabled && !isLoading,
  })
}

export function usePeopleSuggestions(page: number, pageSize = 20) {
  const { user, isAuthenticated, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: peopleKeys.suggestionPage(scope, page, pageSize),
    queryFn: () => communityService.suggestions(page, pageSize),
    enabled: isAuthenticated && !isLoading,
  })
}

export function useFollowUser(targetId: string, isFollowing: boolean) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const scope = viewerScope(user?.id)
  const mutationKey = followKeys.target(scope, targetId)
  const relatedMutationCount = useIsMutating({ mutationKey })

  const mutation = useMutation({
    mutationKey,
    mutationFn: () =>
      isFollowing ? communityService.unfollow(targetId) : communityService.follow(targetId),
    onSuccess: async (profile) => {
      const serverFollowing = Boolean(profile.isFollowing)
      queryClient.setQueryData(userKeys.detail(scope, targetId), profile)
      queryClient.setQueriesData<PageResult<UserDiscoveryItem>>(
        { queryKey: peopleKeys.searches(scope) },
        (page) =>
          page
            ? {
                ...page,
                items: page.items.map((item) =>
                  item.id === targetId ? { ...item, isFollowing: serverFollowing } : item,
                ),
              }
            : page,
      )
      if (serverFollowing) {
        queryClient.setQueriesData<PageResult<UserDiscoveryItem>>(
          { queryKey: peopleKeys.suggestions(scope) },
          (page) =>
            page && page.items.some((item) => item.id === targetId)
              ? {
                  ...page,
                  items: page.items.filter((item) => item.id !== targetId),
                  totalItems: Math.max(0, page.totalItems - 1),
                }
              : page,
        )
      }

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: peopleKeys.scope(scope) }),
        queryClient.invalidateQueries({ queryKey: userKeys.detail(scope, targetId) }),
        user
          ? queryClient.invalidateQueries({ queryKey: userKeys.detail(scope, user.id) })
          : Promise.resolve(),
        queryClient.invalidateQueries({ queryKey: userKeys.followers(scope, targetId) }),
        user
          ? queryClient.invalidateQueries({ queryKey: userKeys.following(scope, user.id) })
          : Promise.resolve(),
        queryClient.invalidateQueries({ queryKey: feedKeys.scoped(scope) }),
        queryClient.invalidateQueries({ queryKey: recommendationKeys.scoped(scope) }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ])
    },
  })

  return {
    ...mutation,
    isPending: mutation.isPending || relatedMutationCount > 0,
  }
}

export function useUserSafetyList(page = 1, pageSize = 50) {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: safetyKeys.list(scope, page, pageSize),
    queryFn: () => communityService.safetyList(page, pageSize),
    enabled: Boolean(user) && !isLoading,
  })
}

async function invalidateSafetyViews(
  queryClient: ReturnType<typeof useQueryClient>,
  scope: string,
) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: safetyKeys.scope(scope) }),
    queryClient.invalidateQueries({ queryKey: peopleKeys.scope(scope) }),
    queryClient.invalidateQueries({ queryKey: userKeys.scope(scope) }),
    queryClient.invalidateQueries({ queryKey: feedKeys.scoped(scope) }),
    queryClient.invalidateQueries({ queryKey: ['reviews'] }),
    queryClient.invalidateQueries({ queryKey: clubChatKeys.scope(scope) }),
    queryClient.invalidateQueries({ queryKey: ['notifications', scope] }),
    queryClient.invalidateQueries({ queryKey: recommendationKeys.scoped(scope) }),
    queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
  ])
}

export function useMuteUser(targetId: string, isMuted: boolean) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const scope = viewerScope(user?.id)
  return useMutation({
    mutationFn: () =>
      isMuted ? communityService.unmute(targetId) : communityService.mute(targetId),
    onSuccess: async () => {
      queryClient.setQueryData<User>(userKeys.detail(scope, targetId), (current) =>
        current ? { ...current, isMuted: !isMuted } : current,
      )
      await invalidateSafetyViews(queryClient, scope)
    },
  })
}

export function useBlockUser(targetId: string) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const scope = viewerScope(user?.id)
  return useMutation({
    mutationFn: () => communityService.block(targetId),
    onSuccess: async () => {
      await invalidateSafetyViews(queryClient, scope)
      queryClient.removeQueries({ queryKey: userKeys.detail(scope, targetId), exact: true })
    },
  })
}

export function useUnblockUser(targetId: string) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const scope = viewerScope(user?.id)
  return useMutation({
    mutationFn: () => communityService.unblock(targetId),
    onSuccess: () => invalidateSafetyViews(queryClient, scope),
  })
}
