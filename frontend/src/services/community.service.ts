import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  FeedItem,
  Review,
  ReviewComment,
  User,
  UserDiscoveryItem,
} from '../types/domain'

export const communityService = {
  feed: async (page = 1) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<FeedItem>>>('/feed', {
        params: { page, pageSize: 20 },
      }),
    ),

  reviews: async (bookId: string, page = 1) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Review>>>('/reviews', {
        params: { bookId, page, pageSize: 20 },
      }),
    ),

  createReview: async (input: {
    bookId: string
    rating: number
    content: string
    containsSpoilers: boolean
  }) => unwrap(await api.post<ApiEnvelope<Review>>('/reviews', input)),

  updateReview: async (
    reviewId: string,
    input: { rating: number; content: string; containsSpoilers: boolean },
  ) => unwrap(await api.put<ApiEnvelope<Review>>(`/reviews/${reviewId}`, input)),

  deleteReview: async (reviewId: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/reviews/${reviewId}`)),

  toggleReviewLike: async (reviewId: string, liked: boolean) =>
    unwrap(
      liked
        ? await api.delete<ApiEnvelope<Review>>(`/reviews/${reviewId}/like`)
        : await api.post<ApiEnvelope<Review>>(`/reviews/${reviewId}/like`),
    ),

  comment: async (reviewId: string, content: string) =>
    unwrap(
      await api.post<ApiEnvelope<ReviewComment>>(`/reviews/${reviewId}/comments`, { content }),
    ),

  comments: async (reviewId: string) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReviewComment>>>(`/reviews/${reviewId}/comments`, {
        params: { page: 1, pageSize: 50 },
      }),
    ),

  deleteComment: async (commentId: string) =>
    unwrap(await api.delete<ApiEnvelope<null>>(`/review-comments/${commentId}`)),

  user: async (id: string) => unwrap(await api.get<ApiEnvelope<User>>(`/users/${id}`)),

  people: async (search: string, page = 1, pageSize = 20) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<UserDiscoveryItem>>>('/users', {
        params: { search: search || undefined, page, pageSize },
      }),
    ),

  suggestions: async (page = 1, pageSize = 20) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<UserDiscoveryItem>>>('/users/suggestions', {
        params: { page, pageSize },
      }),
    ),

  follow: async (id: string) =>
    unwrap(await api.post<ApiEnvelope<User>>(`/users/${id}/follow`)),

  unfollow: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<User>>(`/users/${id}/follow`)),

  updateProfile: async (input: { displayName: string; bio?: string; avatarUrl?: string }) =>
    unwrap(await api.patch<ApiEnvelope<User>>('/users/me', input)),
}
