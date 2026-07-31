import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { communityService } from '../services/community.service'
import type { PageResult } from '../types/api'
import type { UserDiscoveryItem } from '../types/domain'

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
  following: (scope: string, id: string) =>
    [...userKeys.scope(scope), 'following', id] as const,
}

export const feedKeys = {
  all: ['feed'] as const,
  scoped: (scope: string) => [...feedKeys.all, scope] as const,
}

export function useFeed() {
  const { user, isLoading } = useAuth()
  const scope = viewerScope(user?.id)
  return useQuery({
    queryKey: feedKeys.scoped(scope),
    queryFn: () => communityService.feed(1),
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
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useUpdateReview(bookId?: string) {
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
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useDeleteReview(bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reviewId: string) => communityService.deleteReview(reviewId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useLikeReview(bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reviewId, liked }: { reviewId: string; liked: boolean }) =>
      communityService.toggleReviewLike(reviewId, liked),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
      void queryClient.invalidateQueries({ queryKey: feedKeys.all })
    },
  })
}

export function useCommentReview(bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reviewId, content }: { reviewId: string; content: string }) =>
      communityService.comment(reviewId, content),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
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

export function useDeleteReviewComment(bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ commentId }: { reviewId: string; commentId: string }) =>
      communityService.deleteComment(commentId),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
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

  return useMutation({
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
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ])
    },
  })
}
