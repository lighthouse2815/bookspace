import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { queryClient } from '../app/queryClient'
import { getStoredTokens, storeTokens } from '../lib/api'
import { authService } from '../services/auth.service'
import type { User } from '../types/domain'

interface AuthContextValue {
  user: User | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (input: { email: string; password: string }) => Promise<User>
  register: (input: { displayName: string; email: string; password: string }) => Promise<User>
  logout: () => Promise<void>
  refreshUser: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(Boolean(getStoredTokens()))

  const clearSession = useCallback(() => {
    storeTokens(null)
    setUser(null)
    queryClient.clear()
  }, [])

  const refreshUser = useCallback(async () => {
    if (!getStoredTokens()) {
      setUser(null)
      setIsLoading(false)
      return
    }
    try {
      setUser(await authService.me())
    } catch {
      clearSession()
    } finally {
      setIsLoading(false)
    }
  }, [clearSession])

  useEffect(() => {
    void refreshUser()
  }, [refreshUser])

  useEffect(() => {
    const expire = () => clearSession()
    window.addEventListener('bookspace:session-expired', expire)
    return () => window.removeEventListener('bookspace:session-expired', expire)
  }, [clearSession])

  const login = useCallback(async (input: { email: string; password: string }) => {
    const session = await authService.login(input)
    storeTokens({ accessToken: session.accessToken, refreshToken: session.refreshToken })
    setUser(session.user)
    return session.user
  }, [])

  const register = useCallback(
    async (input: { displayName: string; email: string; password: string }) => {
      const session = await authService.register(input)
      storeTokens({ accessToken: session.accessToken, refreshToken: session.refreshToken })
      setUser(session.user)
      return session.user
    },
    [],
  )

  const logout = useCallback(async () => {
    const refreshToken = getStoredTokens()?.refreshToken
    try {
      await authService.logout(refreshToken)
    } finally {
      clearSession()
    }
  }, [clearSession])

  const value = useMemo(
    () => ({
      user,
      isAuthenticated: Boolean(user),
      isLoading,
      login,
      register,
      logout,
      refreshUser,
    }),
    [user, isLoading, login, register, logout, refreshUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth phải được dùng trong AuthProvider')
  return context
}
