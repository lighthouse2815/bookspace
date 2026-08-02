import { api, unwrap } from '../lib/api'
import type { ApiEnvelope, PageResult } from '../types/api'
import type {
  ContentReport,
  ContentReportReason,
  ContentReportStatus,
  ContentReportTargetType,
  ModerationAction,
} from '../types/domain'

export interface CreateContentReportInput {
  targetType: ContentReportTargetType
  targetId: string
  reason: ContentReportReason
  details?: string
}

export interface ContentReportFilters {
  status?: ContentReportStatus
  targetType?: ContentReportTargetType
  reason?: ContentReportReason
  page?: number
  pageSize?: number
}

export interface ResolveContentReportInput {
  status: Exclude<ContentReportStatus, 'PENDING'>
  action: ModerationAction
  resolutionNote?: string
}

export const moderationService = {
  create: async (input: CreateContentReportInput) =>
    unwrap(await api.post<ApiEnvelope<ContentReport>>('/reports', input)),

  reports: async (filters: ContentReportFilters) =>
    unwrap(
      await api.get<ApiEnvelope<PageResult<ContentReport>>>('/admin/reports', {
        params: { pageSize: 20, ...filters },
      }),
    ),

  resolve: async (id: string, input: ResolveContentReportInput) =>
    unwrap(
      await api.patch<ApiEnvelope<ContentReport>>(`/admin/reports/${id}/resolution`, input),
    ),
}
