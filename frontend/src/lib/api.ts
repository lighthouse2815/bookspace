import axios, { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import type { ApiEnvelope } from '../types/api'
import type { AuthTokens } from '../types/domain'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080/api'
const TOKEN_KEY = 'bookspace.tokens'

export const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})

const refreshClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})

export function getStoredTokens(): AuthTokens | null {
  try {
    const value = localStorage.getItem(TOKEN_KEY)
    return value ? (JSON.parse(value) as AuthTokens) : null
  } catch {
    localStorage.removeItem(TOKEN_KEY)
    return null
  }
}

export function storeTokens(tokens: AuthTokens | null) {
  if (tokens) localStorage.setItem(TOKEN_KEY, JSON.stringify(tokens))
  else localStorage.removeItem(TOKEN_KEY)
}

export function unwrap<T>(response: AxiosResponse<ApiEnvelope<T>>) {
  if (!response.data.success) {
    throw new Error(response.data.message || 'Yêu cầu không thành công')
  }
  return response.data.data
}

export function errorMessage(error: unknown, fallback = 'Đã có lỗi xảy ra') {
  if (axios.isAxiosError(error)) {
    const payload = error.response?.data as Partial<ApiEnvelope<unknown>> | undefined
    return payload?.message || error.message || fallback
  }
  return error instanceof Error ? error.message : fallback
}

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const tokens = getStoredTokens()
  if (tokens?.accessToken) config.headers.Authorization = `Bearer ${tokens.accessToken}`
  return config
})

let refreshPromise: Promise<AuthTokens> | null = null

async function refreshTokens(refreshToken: string) {
  if (!refreshPromise) {
    refreshPromise = refreshClient
      .post<ApiEnvelope<AuthTokens>>('/auth/refresh', { refreshToken })
      .then(unwrap)
      .then((tokens) => {
        storeTokens(tokens)
        return tokens
      })
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined
    const tokens = getStoredTokens()
    if (error.response?.status === 401 && original && !original._retried && tokens?.refreshToken) {
      original._retried = true
      try {
        const refreshed = await refreshTokens(tokens.refreshToken)
        original.headers.Authorization = `Bearer ${refreshed.accessToken}`
        return api(original)
      } catch {
        storeTokens(null)
        window.dispatchEvent(new Event('bookspace:session-expired'))
      }
    }
    return Promise.reject(error)
  },
)
