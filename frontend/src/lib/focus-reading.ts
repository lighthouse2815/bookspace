export function formatFocusDuration(totalSeconds: number) {
  const normalized = Math.max(0, Math.floor(totalSeconds))
  const hours = Math.floor(normalized / 3600)
  const minutes = Math.floor((normalized % 3600) / 60)
  const seconds = normalized % 60
  return [hours, minutes, seconds].map((value) => String(value).padStart(2, '0')).join(':')
}
