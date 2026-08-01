import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  FeedItem,
  PublicLibraryEntry,
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

  userLibrary: async (id: string, shelf?: string, page = 1, pageSize = 12) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<PublicLibraryEntry>>>(`/users/${id}/library`, {
        params: { shelf, page, pageSize },
      }),
    ),

  userReviews: async (id: string, page = 1, pageSize = 10) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Review>>>(`/users/${id}/reviews`, {
        params: { page, pageSize },
      }),
    ),

  userActivity: async (id: string, page = 1, pageSize = 10) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<FeedItem>>>(`/users/${id}/activity`, {
        params: { page, pageSize },
      }),
    ),

  followers: async (id: string, page = 1, pageSize = 20) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<User>>>(`/users/${id}/followers`, {
        params: { page, pageSize },
      }),
    ),

  following: async (id: string, page = 1, pageSize = 20) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<User>>>(`/users/${id}/following`, {
        params: { page, pageSize },
      }),
    ),

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

  updateProfilePrivacy: async (input: {
    isReadingShelfPublic: boolean
    isReadingActivityPublic: boolean
  }) => unwrap(await api.patch<ApiEnvelope<User>>('/users/me/privacy', input)),
}
