import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  ReadingSprintCheckIn,
  ReadingSprintDetail,
  ReadingSprintMilestone,
  ReadingSprintMilestoneResponse,
  ReadingSprintParticipant,
  ReadingSprintStatus,
  ReadingSprintSummary,
  ReadingSprintTargetUnit,
} from '../types/domain'

export interface SaveReadingSprintInput {
  bookId: string
  title: string
  description: string | null
  startsAt: string
  endsAt: string
  targetUnit: ReadingSprintTargetUnit
  targetValue: number
}

export interface SaveReadingSprintMilestoneInput {
  title: string
  description: string | null
  targetValue: number
}

export interface ReadingSprintProgressInput {
  progressValue: number
  note: string | null
}

const sprintPath = (clubId: string, sprintId?: string) =>
  `/clubs/${clubId}/reading-sprints${sprintId ? `/${sprintId}` : ''}`

export const readingSprintService = {
  list: async (
    clubId: string,
    status: ReadingSprintStatus | undefined,
    page: number,
    pageSize: number,
  ) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingSprintSummary>>>(sprintPath(clubId), {
        params: { status, page, pageSize },
      }),
    ),

  detail: async (clubId: string, sprintId: string) =>
    unwrap(
      await api.get<ApiEnvelope<ReadingSprintDetail>>(sprintPath(clubId, sprintId)),
    ),

  create: async (clubId: string, input: SaveReadingSprintInput) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintDetail>>(sprintPath(clubId), input),
    ),

  update: async (clubId: string, sprintId: string, input: SaveReadingSprintInput) =>
    unwrap(
      await api.patch<ApiEnvelope<ReadingSprintDetail>>(
        sprintPath(clubId, sprintId),
        input,
      ),
    ),

  join: async (clubId: string, sprintId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintParticipant>>(
        `${sprintPath(clubId, sprintId)}/join`,
      ),
    ),

  leave: async (clubId: string, sprintId: string) =>
    unwrap(
      await api.delete<ApiEnvelope<ReadingSprintParticipant>>(
        `${sprintPath(clubId, sprintId)}/join`,
      ),
    ),

  checkIn: async (
    clubId: string,
    sprintId: string,
    input: ReadingSprintProgressInput,
  ) =>
    unwrap(
      await api.put<ApiEnvelope<ReadingSprintParticipant>>(
        `${sprintPath(clubId, sprintId)}/progress`,
        input,
      ),
    ),

  leaderboard: async (
    clubId: string,
    sprintId: string,
    page: number,
    pageSize: number,
  ) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingSprintParticipant>>>(
        `${sprintPath(clubId, sprintId)}/leaderboard`,
        { params: { page, pageSize } },
      ),
    ),

  timeline: async (
    clubId: string,
    sprintId: string,
    page: number,
    pageSize: number,
  ) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingSprintCheckIn>>>(
        `${sprintPath(clubId, sprintId)}/timeline`,
        { params: { page, pageSize } },
      ),
    ),

  createMilestone: async (
    clubId: string,
    sprintId: string,
    input: SaveReadingSprintMilestoneInput,
  ) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintMilestone>>(
        `${sprintPath(clubId, sprintId)}/milestones`,
        input,
      ),
    ),

  updateMilestone: async (
    clubId: string,
    sprintId: string,
    milestoneId: string,
    input: SaveReadingSprintMilestoneInput,
  ) =>
    unwrap(
      await api.patch<ApiEnvelope<ReadingSprintMilestone>>(
        `${sprintPath(clubId, sprintId)}/milestones/${milestoneId}`,
        input,
      ),
    ),

  deleteMilestone: async (clubId: string, sprintId: string, milestoneId: string) =>
    unwrap(
      await api.delete<ApiEnvelope<null>>(
        `${sprintPath(clubId, sprintId)}/milestones/${milestoneId}`,
      ),
    ),

  milestoneResponses: async (
    clubId: string,
    sprintId: string,
    milestoneId: string,
    page: number,
    pageSize: number,
  ) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ReadingSprintMilestoneResponse>>>(
        `${sprintPath(clubId, sprintId)}/milestones/${milestoneId}/responses`,
        { params: { page, pageSize } },
      ),
    ),

  createMilestoneResponse: async (
    clubId: string,
    sprintId: string,
    milestoneId: string,
    content: string,
  ) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintMilestoneResponse>>(
        `${sprintPath(clubId, sprintId)}/milestones/${milestoneId}/responses`,
        { content },
      ),
    ),

  deleteMilestoneResponse: async (
    clubId: string,
    sprintId: string,
    responseId: string,
  ) =>
    unwrap(
      await api.delete<ApiEnvelope<null>>(
        `${sprintPath(clubId, sprintId)}/milestone-responses/${responseId}`,
      ),
    ),

  sendReminder: async (clubId: string, sprintId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintDetail>>(
        `${sprintPath(clubId, sprintId)}/reminders`,
      ),
    ),

  complete: async (clubId: string, sprintId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintDetail>>(
        `${sprintPath(clubId, sprintId)}/complete`,
      ),
    ),

  cancel: async (clubId: string, sprintId: string) =>
    unwrap(
      await api.post<ApiEnvelope<ReadingSprintDetail>>(
        `${sprintPath(clubId, sprintId)}/cancel`,
      ),
    ),
}
