import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { communityService } from '../services/community.service'

export function useFeed() {
  return useQuery({ queryKey: ['feed'], queryFn: () => communityService.feed(1) })
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
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
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
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
    },
  })
}

export function useDeleteReview(bookId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reviewId: string) => communityService.deleteReview(reviewId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['reviews', bookId] })
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
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
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
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
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
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
      void queryClient.invalidateQueries({ queryKey: ['feed'] })
    },
  })
}

export function useUser(id?: string) {
  return useQuery({
    queryKey: ['users', id],
    queryFn: () => communityService.user(id!),
    enabled: Boolean(id),
  })
}
