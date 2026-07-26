import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { MailCheck } from 'lucide-react'
import { authApi } from '@/platform/auth/api'
import { AuthLayout } from '@/app/components/AuthLayout'

/** Ask for an email and send a password-reset link. Always shows the same "sent" state (no enumeration). */
export function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try { await authApi.forgotPassword(email.trim()) } catch { /* never reveal errors here */ }
    setSent(true)
    setBusy(false)
  }

  if (sent) {
    return (
      <AuthLayout active="in">
        <div className="auth-form" style={{ textAlign: 'center' }}>
          <div style={{ display: 'grid', placeItems: 'center', gap: 8 }}>
            <span className="empty-ico" style={{ ['--mc' as string]: 'var(--brand)' }}><MailCheck size={24} /></span>
            <h2>{t('auth.resetSentTitle')}</h2>
            <p className="hint">{t('auth.resetSentSub', { email: email.trim() })}</p>
          </div>
          <p className="auth-foot"><Link to="/login">{t('auth.signin')}</Link></p>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout active="in">
      <form className="auth-form" onSubmit={onSubmit} noValidate>
        <div>
          <h2>{t('auth.forgotTitle')}</h2>
          <p className="hint" style={{ marginTop: 4 }}>{t('auth.forgotSub')}</p>
        </div>
        <div className="field">
          <label>{t('auth.email')}</label>
          <input className="inp" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} autoFocus />
        </div>
        <button className="btn primary" type="submit" disabled={busy || !email.trim()}>{t('auth.sendReset')}</button>
        <div className="auth-foot"><Link to="/login">{t('auth.signin')}</Link></div>
      </form>
    </AuthLayout>
  )
}
