import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  readingSprintService,
  type ReadingSprintProgressInput,
  type SaveReadingSprintInput,
  type SaveReadingSprintMilestoneInput,
} from '../services/reading-sprint.service'
import type { ReadingSprintStatus } from '../types/domain'

export const readingSprintKeys = {
  all: ['reading-sprints'] as const,
  club: (clubId: string) => [...readingSprintKeys.all, 'club', clubId] as const,
  lists: (clubId: string) => [...readingSprintKeys.club(clubId), 'list'] as const,
  list: (
    clubId: string,
    status: ReadingSprintStatus | undefined,
    page: number,
    pageSize: number,
  ) =>
    [...readingSprintKeys.lists(clubId), status ?? 'ALL', page, pageSize] as const,
  detail: (clubId: string, sprintId: string) =>
    [...readingSprintKeys.club(clubId), 'detail', sprintId] as const,
  leaderboards: (clubId: string, sprintId: string) =>
    [...readingSprintKeys.detail(clubId, sprintId), 'leaderboard'] as const,
  leaderboard: (clubId: string, sprintId: string, page: number, pageSize: number) =>
    [...readingSprintKeys.leaderboards(clubId, sprintId), page, pageSize] as const,
  timelines: (clubId: string, sprintId: string) =>
    [...readingSprintKeys.detail(clubId, sprintId), 'timeline'] as const,
  timeline: (clubId: string, sprintId: string, page: number, pageSize: number) =>
    [...readingSprintKeys.timelines(clubId, sprintId), page, pageSize] as const,
  responseLists: (clubId: string, sprintId: string, milestoneId: string) =>
    [
      ...readingSprintKeys.detail(clubId, sprintId),
      'milestones',
      milestoneId,
      'responses',
    ] as const,
  responses: (
    clubId: string,
    sprintId: string,
    milestoneId: string,
    page: number,
    pageSize: number,
  ) =>
    [
      ...readingSprintKeys.responseLists(clubId, sprintId, milestoneId),
      page,
      pageSize,
    ] as const,
}

export function useReadingSprints(
  clubId: string,
  status: ReadingSprintStatus | undefined,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: readingSprintKeys.list(clubId, status, page, pageSize),
    queryFn: () => readingSprintService.list(clubId, status, page, pageSize),
    enabled: Boolean(clubId),
  })
}

export function useReadingSprint(clubId: string, sprintId: string) {
  return useQuery({
    queryKey: readingSprintKeys.detail(clubId, sprintId),
    queryFn: () => readingSprintService.detail(clubId, sprintId),
    enabled: Boolean(clubId && sprintId),
  })
}

export function useCreateReadingSprint(clubId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveReadingSprintInput) => readingSprintService.create(clubId, input),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: readingSprintKeys.club(clubId) }),
  })
}

export function useUpdateReadingSprint(clubId: string, sprintId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveReadingSprintInput) =>
      readingSprintService.update(clubId, sprintId, input),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.detail(clubId, sprintId),
        }),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.lists(clubId),
        }),
      ])
    },
  })
}

function useInvalidateSprint(clubId: string, sprintId: string) {
  const queryClient = useQueryClient()
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: readingSprintKeys.detail(clubId, sprintId),
      }),
      queryClient.invalidateQueries({
        queryKey: readingSprintKeys.lists(clubId),
      }),
    ])
  }
}

export function useJoinReadingSprint(clubId: string, sprintId: string) {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: () => readingSprintService.join(clubId, sprintId),
    onSuccess: async () => {
      await Promise.all([
        invalidate(),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.leaderboards(clubId, sprintId),
        }),
      ])
    },
  })
}

export function useLeaveReadingSprint(clubId: string, sprintId: string) {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: () => readingSprintService.leave(clubId, sprintId),
    onSuccess: async () => {
      await Promise.all([
        invalidate(),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.leaderboards(clubId, sprintId),
        }),
      ])
    },
  })
}

export function useCheckInReadingSprint(clubId: string, sprintId: string) {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: (input: ReadingSprintProgressInput) =>
      readingSprintService.checkIn(clubId, sprintId, input),
    onSuccess: async () => {
      await Promise.all([
        invalidate(),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.leaderboards(clubId, sprintId),
        }),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.timelines(clubId, sprintId),
        }),
      ])
    },
  })
}

export function useReadingSprintLeaderboard(
  clubId: string,
  sprintId: string,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: readingSprintKeys.leaderboard(clubId, sprintId, page, pageSize),
    queryFn: () => readingSprintService.leaderboard(clubId, sprintId, page, pageSize),
    enabled: Boolean(clubId && sprintId),
  })
}

export function useReadingSprintTimeline(
  clubId: string,
  sprintId: string,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: readingSprintKeys.timeline(clubId, sprintId, page, pageSize),
    queryFn: () => readingSprintService.timeline(clubId, sprintId, page, pageSize),
    enabled: Boolean(clubId && sprintId),
  })
}

export function useCreateReadingSprintMilestone(clubId: string, sprintId: string) {
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: (input: SaveReadingSprintMilestoneInput) =>
      readingSprintService.createMilestone(clubId, sprintId, input),
    onSuccess: invalidate,
  })
}

export function useUpdateReadingSprintMilestone(clubId: string, sprintId: string) {
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: ({
      milestoneId,
      input,
    }: {
      milestoneId: string
      input: SaveReadingSprintMilestoneInput
    }) => readingSprintService.updateMilestone(clubId, sprintId, milestoneId, input),
    onSuccess: invalidate,
  })
}

export function useDeleteReadingSprintMilestone(clubId: string, sprintId: string) {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: (milestoneId: string) =>
      readingSprintService.deleteMilestone(clubId, sprintId, milestoneId),
    onSuccess: async (_data, milestoneId) => {
      queryClient.removeQueries({
        queryKey: readingSprintKeys.responseLists(clubId, sprintId, milestoneId),
      })
      await invalidate()
    },
  })
}

export function useReadingSprintMilestoneResponses(
  clubId: string,
  sprintId: string,
  milestoneId: string,
  enabled: boolean,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: readingSprintKeys.responses(
      clubId,
      sprintId,
      milestoneId,
      page,
      pageSize,
    ),
    queryFn: () =>
      readingSprintService.milestoneResponses(
        clubId,
        sprintId,
        milestoneId,
        page,
        pageSize,
      ),
    enabled: Boolean(clubId && sprintId && milestoneId && enabled),
  })
}

export function useCreateReadingSprintMilestoneResponse(
  clubId: string,
  sprintId: string,
  milestoneId: string,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (content: string) =>
      readingSprintService.createMilestoneResponse(
        clubId,
        sprintId,
        milestoneId,
        content,
      ),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.responseLists(clubId, sprintId, milestoneId),
        }),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.detail(clubId, sprintId),
        }),
      ])
    },
  })
}

export function useDeleteReadingSprintMilestoneResponse(
  clubId: string,
  sprintId: string,
  milestoneId: string,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (responseId: string) =>
      readingSprintService.deleteMilestoneResponse(clubId, sprintId, responseId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.responseLists(clubId, sprintId, milestoneId),
        }),
        queryClient.invalidateQueries({
          queryKey: readingSprintKeys.detail(clubId, sprintId),
        }),
      ])
    },
  })
}

export function useSendReadingSprintReminder(clubId: string, sprintId: string) {
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: () => readingSprintService.sendReminder(clubId, sprintId),
    onSuccess: invalidate,
  })
}

export function useCompleteReadingSprint(clubId: string, sprintId: string) {
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: () => readingSprintService.complete(clubId, sprintId),
    onSuccess: invalidate,
  })
}

export function useCancelReadingSprint(clubId: string, sprintId: string) {
  const invalidate = useInvalidateSprint(clubId, sprintId)
  return useMutation({
    mutationFn: () => readingSprintService.cancel(clubId, sprintId),
    onSuccess: invalidate,
  })
}
