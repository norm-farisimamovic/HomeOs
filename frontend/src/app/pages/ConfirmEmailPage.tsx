import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Home } from 'lucide-react'
import { authApi } from '@/platform/auth/api'
import { meQueryKey } from '@/platform/auth/useAuth'
import '@/app/auth.css'

/** Public page opened from the confirmation email — verifies the email then signs the founder in. */
export function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const token = params.get('token') ?? ''
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [failed, setFailed] = useState(false)
  const ran = useRef(false)

  useEffect(() => {
    if (ran.current) return
    ran.current = true
    void (async () => {
      try {
        const me = await authApi.confirmEmail(userId, token)
        queryClient.setQueryData(meQueryKey, me)
        navigate('/')
      } catch {
        setFailed(true)
      }
    })()
  }, [userId, token, navigate, queryClient])

  return (
    <div className="auth-solo">
      <div className="card card-pad">
        <div className="brand-row"><span className="brand-mark"><Home size={17} /></span> {t('app.name')}</div>
        {failed ? (
          <>
            <h2>{t('auth.confirmFailed')}</h2>
            <p className="hint" style={{ marginTop: 8 }}>{t('auth.confirmFailedSub')}</p>
            <p style={{ marginTop: 12 }}><Link className="linkbtn" to="/login">{t('auth.signin')}</Link></p>
          </>
        ) : (
          <p className="hint">{t('auth.confirming')}</p>
        )}
      </div>
    </div>
  )
}
