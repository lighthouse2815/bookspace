import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  Club,
  ClubInvitation,
  ClubInvitationStatus,
  ClubMember,
  ClubMemberRole,
  ClubPost,
  ClubPostComment,
} from '../types/domain'

export interface SaveClubInput {
  name: string
  description?: string
  coverImageUrl?: string
  isPrivate: boolean
}

export const clubService = {
  clubs: async (search?: string) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<Club>>>('/clubs', {
        params: { search, page: 1, pageSize: 50 },
      }),
    ),

  club: async (id: string) => {
    const [club, posts] = await Promise.all([
      api.get<ApiEnvelope<Club>>(`/clubs/${id}`).then(unwrap),
      api
        .get<ApiEnvelope<PageResult<ClubPost>>>(`/clubs/${id}/posts`, {
          params: { page: 1, pageSize: 50 },
        })
        .then(unwrap),
    ])
    return { ...club, posts: posts.items }
  },

  create: async (input: SaveClubInput) =>
    unwrap(await api.post<ApiEnvelope<Club>>('/clubs', input)),

  update: async (id: string, input: SaveClubInput) =>
    unwrap(await api.patch<ApiEnvelope<Club>>(`/clubs/${id}`, input)),

  join: async (id: string) => unwrap(await api.post<ApiEnvelope<Club>>(`/clubs/${id}/join`)),

  leave: async (id: string) => unwrap(await api.delete<ApiEnvelope<null>>(`/clubs/${id}/join`)),

  members: async (id: string) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ClubMember>>>(`/clubs/${id}/members`, {
        params: { page: 1, pageSize: 100 },
      }),
    ),

  updateMemberRole: async (id: string, userId: string, role: Exclude<ClubMemberRole, 'OWNER'>) =>
    unwrap(
      await api.patch<ApiEnvelope<ClubMember>>(`/clubs/${id}/members/${userId}/role`, { role }),
    ),

  removeMember: async (id: string, userId: string) =>
    unwrap(await api.delete<ApiEnvelope<unknown>>(`/clubs/${id}/members/${userId}`)),

  invitations: async (id: string, status?: ClubInvitationStatus) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ClubInvitation>>>(`/clubs/${id}/invitations`, {
        params: { status, page: 1, pageSize: 100 },
      }),
    ),

  invite: async (id: string, email: string) =>
    unwrap(
      await api.post<ApiEnvelope<ClubInvitation>>(`/clubs/${id}/invitations`, { email }),
    ),

  revokeInvitation: async (id: string, invitationId: string) =>
    unwrap(
      await api.delete<ApiEnvelope<ClubInvitation>>(
        `/clubs/${id}/invitations/${invitationId}`,
      ),
    ),

  myInvitations: async (status?: ClubInvitationStatus) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ClubInvitation>>>('/clubs/invitations', {
        params: { status, page: 1, pageSize: 100 },
      }),
    ),

  acceptInvitation: async (invitationId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ClubMember>>(`/clubs/invitations/${invitationId}/accept`),
    ),

  declineInvitation: async (invitationId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ClubInvitation>>(`/clubs/invitations/${invitationId}/decline`),
    ),

  setCurrentBook: async (id: string, bookId: string) =>
    unwrap(await api.put<ApiEnvelope<Club>>(`/clubs/${id}/current-book`, { bookId })),

  clearCurrentBook: async (id: string) =>
    unwrap(await api.delete<ApiEnvelope<Club>>(`/clubs/${id}/current-book`)),

  createPost: async (id: string, content: string) =>
    unwrap(await api.post<ApiEnvelope<ClubPost>>(`/clubs/${id}/posts`, { content })),

  postComments: async (postId: string) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ClubPostComment>>>(`/clubs/posts/${postId}/comments`, {
        params: { page: 1, pageSize: 50 },
      }),
    ),

  createPostComment: async (postId: string, content: string) =>
    unwrap(
      await api.post<ApiEnvelope<ClubPostComment>>(`/clubs/posts/${postId}/comments`, { content }),
    ),
}
