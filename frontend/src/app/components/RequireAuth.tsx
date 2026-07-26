import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useMe } from '@/platform/auth/useAuth'

/** Route guard: renders children only for an authenticated member; otherwise redirects to /login. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: me, isLoading } = useMe()
  const location = useLocation()

  if (isLoading) {
    return (
      <div style={{ display: 'grid', placeItems: 'center', minHeight: '100dvh' }}>
        <div className="eyebrow">Home OS</div>
      </div>
    )
  }
  if (!me) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  return <>{children}</>
}
