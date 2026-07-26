import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Eye, EyeOff } from 'lucide-react'
import { authApi } from '@/platform/auth/api'
import { ApiError } from '@/platform/api/client'
import { AuthLayout } from '@/app/components/AuthLayout'

/** Opened from the reset email — sets a new password for the account in the link. */
export function ResetPasswordPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const token = params.get('token') ?? ''
  const [pw, setPw] = useState('')
  const [showPw, setShowPw] = useState(false)
  const [done, setDone] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (pw.length < 8) { setError(t('auth.passwordMin')); return }
    setBusy(true)
    try {
      await authApi.resetPassword(userId, token, pw)
      setDone(true)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('auth.resetFailed'))
    }
    setBusy(false)
  }

  if (done) {
    return (
      <AuthLayout active="in">
        <div className="auth-form" style={{ textAlign: 'center' }}>
          <h2>{t('auth.resetTitle')}</h2>
          <p className="hint">{t('auth.resetDone')}</p>
          <p className="auth-foot"><Link className="btn primary" to="/login">{t('auth.signin')}</Link></p>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout active="in">
      <form className="auth-form" onSubmit={onSubmit} noValidate>
        <div>
          <h2>{t('auth.resetTitle')}</h2>
          <p className="hint" style={{ marginTop: 4 }}>{t('auth.resetSub')}</p>
        </div>
        {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
        <div className="field">
          <label>{t('auth.newPassword')}</label>
          <div className="pw-wrap">
            <input className="inp" type={showPw ? 'text' : 'password'} autoComplete="new-password" value={pw} onChange={(e) => setPw(e.target.value)} autoFocus />
            <button className="peek" type="button" onClick={() => setShowPw((v) => !v)} aria-label={t('auth.newPassword')}>
              {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
        </div>
        <button className="btn primary" type="submit" disabled={busy || !pw}>{t('common.save')}</button>
        <div className="auth-foot"><Link to="/login">{t('auth.signin')}</Link></div>
      </form>
    </AuthLayout>
  )
}
