import { api, unwrap } from '../lib/api'
import type { ApiEnvelope } from '../types/api'
import type {
  ReadingInsightsCalendar,
  ReadingInsightsMonthly,
  ReadingInsightsOverview,
  ReadingInsightsWeekly,
} from '../types/domain'

export const insightsService = {
  overview: async (days: number, utcOffsetMinutes: number) =>
    unwrap(
      await api.get<ApiEnvelope<ReadingInsightsOverview>>('/insights/overview', {
        params: { days, utcOffsetMinutes },
      }),
    ),

  calendar: async (
    period: { days: 30 | 90 | 365 } | { year: number },
    utcOffsetMinutes: number,
  ) =>
    unwrap(
      await api.get<ApiEnvelope<ReadingInsightsCalendar>>('/insights/calendar', {
        params: { ...period, utcOffsetMinutes },
      }),
    ),

  weekly: async (weeks: number, utcOffsetMinutes: number) =>
    unwrap(
      await api.get<ApiEnvelope<ReadingInsightsWeekly>>('/insights/weekly', {
        params: { weeks, utcOffsetMinutes },
      }),
    ),

  monthly: async (months: 6 | 12 | 24, utcOffsetMinutes: number) =>
    unwrap(
      await api.get<ApiEnvelope<ReadingInsightsMonthly>>('/insights/monthly', {
        params: { months, utcOffsetMinutes },
      }),
    ),
}
