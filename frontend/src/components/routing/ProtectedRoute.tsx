import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

export function ProtectedRoute() {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="grid min-h-[60dvh] place-items-center" aria-label="Đang kiểm tra phiên đăng nhập">
        <div className="w-52 animate-pulse space-y-3">
          <div className="h-4 rounded bg-surface-muted" />
          <div className="mx-auto h-4 w-2/3 rounded bg-surface-muted" />
        </div>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

export function AdminRoute() {
  const { user, isLoading } = useAuth()
  if (isLoading) return null
  return user?.role === 'ADMIN' ? <Outlet /> : <Navigate to="/dashboard" replace />
}
