import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { accountService } from '../services/account.service'
import { adminService } from '../services/admin.service'
import { challengeService } from '../services/challenge.service'
import { clubService, type SaveClubInput } from '../services/club.service'
import type { ClubInvitationStatus, ClubMemberRole } from '../types/domain'
import { challengeKeys, challengeViewerScope } from './challengeKeys'
import { readingSprintKeys } from './useReadingSprints'

export const clubKeys = {
  all: ['clubs'] as const,
  lists: ['clubs', 'list'] as const,
  list: (search: string) => [...clubKeys.all, 'list', search] as const,
  detail: (id: string) => [...clubKeys.all, 'detail', id] as const,
  members: (id: string) => [...clubKeys.all, 'members', id] as const,
  invitations: (id: string, status?: ClubInvitationStatus) =>
    [...clubKeys.all, 'invitations', id, status ?? 'ALL'] as const,
  myInvitations: (status?: ClubInvitationStatus) =>
    status
      ? ([...clubKeys.all, 'my-invitations', status] as const)
      : ([...clubKeys.all, 'my-invitations'] as const),
}

export function useClubs(search?: string) {
  return useQuery({
    queryKey: clubKeys.list(search ?? ''),
    queryFn: () => clubService.clubs(search),
  })
}

export function useClub(id?: string) {
  return useQuery({
    queryKey: clubKeys.detail(id ?? ''),
    queryFn: () => clubService.club(id!),
    enabled: Boolean(id),
  })
}

export function useCreateClub() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveClubInput) => clubService.create(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clubKeys.all }),
  })
}

export function useUpdateClub(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveClubInput) => clubService.update(id, input),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.lists }),
      ])
    },
  })
}

export function useClubMembership(id: string, joined: boolean) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      if (joined) return await clubService.leave(id)
      return await clubService.join(id)
    },
    onSuccess: async () => {
      if (joined) {
        await queryClient.invalidateQueries({ queryKey: clubKeys.lists })
        queryClient.removeQueries({ queryKey: clubKeys.detail(id) })
        queryClient.removeQueries({ queryKey: readingSprintKeys.club(id) })
        return
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.all }),
        queryClient.invalidateQueries({ queryKey: clubKeys.myInvitations() }),
        queryClient.invalidateQueries({ queryKey: readingSprintKeys.club(id) }),
      ])
    },
  })
}

export function useClubMembers(id: string, enabled = true) {
  return useQuery({
    queryKey: clubKeys.members(id),
    queryFn: () => clubService.members(id),
    enabled: Boolean(id) && enabled,
  })
}

export function useUpdateClubMemberRole(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      userId,
      role,
    }: {
      userId: string
      role: Exclude<ClubMemberRole, 'OWNER'>
    }) => clubService.updateMemberRole(id, userId, role),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.members(id) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: readingSprintKeys.club(id) }),
      ])
    },
  })
}

export function useRemoveClubMember(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => clubService.removeMember(id, userId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.members(id) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.lists }),
        queryClient.invalidateQueries({ queryKey: readingSprintKeys.club(id) }),
      ])
    },
  })
}

export function useClubInvitations(id: string, enabled = true) {
  return useQuery({
    queryKey: clubKeys.invitations(id, 'PENDING'),
    queryFn: () => clubService.invitations(id, 'PENDING'),
    enabled: Boolean(id) && enabled,
  })
}

export function useInviteToClub(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (email: string) => clubService.invite(id, email),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: clubKeys.invitations(id, 'PENDING') }),
  })
}

export function useRevokeClubInvitation(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (invitationId: string) => clubService.revokeInvitation(id, invitationId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: clubKeys.invitations(id, 'PENDING') }),
  })
}

export function useMyClubInvitations(status: ClubInvitationStatus = 'PENDING') {
  return useQuery({
    queryKey: clubKeys.myInvitations(status),
    queryFn: () => clubService.myInvitations(status),
  })
}

export function useRespondToClubInvitation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({
      invitationId,
      action,
    }: {
      invitationId: string
      clubId: string
      action: 'accept' | 'decline'
    }) => {
      if (action === 'accept') return await clubService.acceptInvitation(invitationId)
      return await clubService.declineInvitation(invitationId)
    },
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.myInvitations() }),
        queryClient.invalidateQueries({ queryKey: clubKeys.detail(variables.clubId) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.members(variables.clubId) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.lists }),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.club(variables.clubId),
        }),
      ])
    },
  })
}

export function useSetClubCurrentBook(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (bookId: string) => clubService.setCurrentBook(id, bookId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.lists }),
      ])
    },
  })
}

export function useClearClubCurrentBook(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => clubService.clearCurrentBook(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: clubKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: clubKeys.lists }),
      ])
    },
  })
}

export function useCreateClubPost(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (content: string) => clubService.createPost(id, content),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clubKeys.detail(id) }),
  })
}

export function useClubPostComments(postId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['club-post-comments', postId],
    queryFn: () => clubService.postComments(postId),
    enabled,
  })
}

export function useCreateClubPostComment(clubId: string, postId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (content: string) => clubService.createPostComment(postId, content),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['club-post-comments', postId] })
      void queryClient.invalidateQueries({ queryKey: clubKeys.detail(clubId) })
    },
  })
}

export function useChallenges() {
  const { user, isLoading } = useAuth()
  const scope = challengeViewerScope(user?.id)

  return useQuery({
    queryKey: challengeKeys.lists(scope),
    queryFn: challengeService.challenges,
    enabled: !isLoading,
  })
}

export function useChallenge(id: string) {
  const { user, isLoading } = useAuth()
  const scope = challengeViewerScope(user?.id)

  return useQuery({
    queryKey: challengeKeys.detail(scope, id),
    queryFn: () => challengeService.detail(id),
    enabled: Boolean(id) && !isLoading,
  })
}

export function useAdminChallenges() {
  return useQuery({ queryKey: ['admin', 'challenges'], queryFn: adminService.challenges })
}

export function useChallengeMembership(id: string, joined: boolean) {
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const scope = challengeViewerScope(user?.id)

  return useMutation({
    mutationFn: () => (joined ? challengeService.leave(id) : challengeService.join(id)),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: challengeKeys.lists(scope) }),
        queryClient.invalidateQueries({ queryKey: challengeKeys.detail(scope, id) }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ])
    },
  })
}

export function useDashboard() {
  return useQuery({ queryKey: ['dashboard'], queryFn: accountService.dashboard })
}
