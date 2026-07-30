import { api, unwrap } from '../lib/api'
import type { ApiEnvelope } from '../types/api'
import type { AuthSession, AuthTokens, User } from '../types/domain'

export const authService = {
  login: async (input: { email: string; password: string }) =>
    unwrap(await api.post<ApiEnvelope<AuthSession>>('/auth/login', input)),

  register: async (input: { displayName: string; email: string; password: string }) =>
    unwrap(await api.post<ApiEnvelope<AuthSession>>('/auth/register', input)),

  me: async () => unwrap(await api.get<ApiEnvelope<User>>('/auth/me')),

  refresh: async (refreshToken: string) =>
    unwrap(await api.post<ApiEnvelope<AuthTokens>>('/auth/refresh', { refreshToken })),

  logout: async (refreshToken?: string) =>
    unwrap(await api.post<ApiEnvelope<null>>('/auth/logout', { refreshToken })),
}
