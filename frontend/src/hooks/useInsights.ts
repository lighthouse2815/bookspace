import { useQuery } from '@tanstack/react-query'
import { insightsService } from '../services/insights.service'

export const insightKeys = {
  all: ['reading-insights'] as const,
  overview: (days: number, utcOffsetMinutes: number) =>
    [...insightKeys.all, 'overview', days, utcOffsetMinutes] as const,
  calendar: (period: { days: 30 | 90 | 365 } | { year: number }, utcOffsetMinutes: number) =>
    [...insightKeys.all, 'calendar', period, utcOffsetMinutes] as const,
  weekly: (weeks: number, utcOffsetMinutes: number) =>
    [...insightKeys.all, 'weekly', weeks, utcOffsetMinutes] as const,
  monthly: (months: 6 | 12 | 24, utcOffsetMinutes: number) =>
    [...insightKeys.all, 'monthly', months, utcOffsetMinutes] as const,
}

export function useInsightsOverview(days: number) {
  const utcOffsetMinutes = -new Date().getTimezoneOffset()
  return useQuery({
    queryKey: insightKeys.overview(days, utcOffsetMinutes),
    queryFn: () => insightsService.overview(days, utcOffsetMinutes),
  })
}

export function useInsightsCalendar(period: { days: 30 | 90 | 365 } | { year: number }) {
  const utcOffsetMinutes = -new Date().getTimezoneOffset()
  return useQuery({
    queryKey: insightKeys.calendar(period, utcOffsetMinutes),
    queryFn: () => insightsService.calendar(period, utcOffsetMinutes),
  })
}

export function useInsightsWeekly(weeks: number) {
  const utcOffsetMinutes = -new Date().getTimezoneOffset()
  return useQuery({
    queryKey: insightKeys.weekly(weeks, utcOffsetMinutes),
    queryFn: () => insightsService.weekly(weeks, utcOffsetMinutes),
  })
}

export function useInsightsMonthly(months: 6 | 12 | 24) {
  const utcOffsetMinutes = -new Date().getTimezoneOffset()
  return useQuery({
    queryKey: insightKeys.monthly(months, utcOffsetMinutes),
    queryFn: () => insightsService.monthly(months, utcOffsetMinutes),
  })
}
