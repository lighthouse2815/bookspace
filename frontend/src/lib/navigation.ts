const DEFAULT_RETURN_PATH = '/dashboard'

export function safeReturnPath(value: unknown, fallback = DEFAULT_RETURN_PATH) {
  if (
    typeof value !== 'string' ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    Array.from(value).some((character) => character.charCodeAt(0) < 32)
  ) {
    return fallback
  }

  return value
}

export function returnPathFromState(state: unknown, fallback = DEFAULT_RETURN_PATH) {
  return safeReturnPath((state as { from?: unknown } | null)?.from, fallback)
}
