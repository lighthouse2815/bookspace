import type {
  ReadingSprintStatus,
  ReadingSprintTargetUnit,
} from '../types/domain'

export const readingSprintStatusLabels: Record<ReadingSprintStatus, string> = {
  PLANNED: 'Sắp diễn ra',
  ACTIVE: 'Đang đọc',
  ENDED: 'Đã kết thúc',
  COMPLETED: 'Đã tổng kết',
  CANCELLED: 'Đã hủy',
}

export const readingSprintUnitLabels: Record<ReadingSprintTargetUnit, string> = {
  PAGES: 'Trang',
  CHAPTERS: 'Chương',
}

export const readingSprintUnitNames: Record<ReadingSprintTargetUnit, string> = {
  PAGES: 'trang',
  CHAPTERS: 'chương',
}

export function readingSprintStatusClass(status: ReadingSprintStatus) {
  if (status === 'ACTIVE') return 'bg-accent text-white'
  if (status === 'COMPLETED') return 'bg-accent-soft text-accent-strong'
  if (status === 'CANCELLED') {
    return 'bg-red-50 text-red-700 dark:bg-red-950/35 dark:text-red-300'
  }
  if (status === 'ENDED') return 'bg-surface-muted text-heading'
  return 'bg-heading text-page'
}

const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function formatReadingSprintDateTime(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Chưa cập nhật' : dateTimeFormatter.format(date)
}

export function toDateTimeLocal(value: string | Date) {
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return ''
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

export function createDefaultSprintRange() {
  const startsAt = new Date()
  startsAt.setMinutes(Math.ceil(startsAt.getMinutes() / 15) * 15, 0, 0)
  const endsAt = new Date(startsAt)
  endsAt.setDate(endsAt.getDate() + 14)
  return {
    startsAt: toDateTimeLocal(startsAt),
    endsAt: toDateTimeLocal(endsAt),
  }
}
