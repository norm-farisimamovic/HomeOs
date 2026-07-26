import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Home } from 'lucide-react'
import { authApi } from '@/platform/auth/api'
import { meQueryKey } from '@/platform/auth/useAuth'
import { ApiError } from '@/platform/api/client'
import '@/app/auth.css'

/** Public page an invitee opens from their email to set a password and join the household. */
export function AcceptInvitePage() {
  const { token = '' } = useParams()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: invite, isLoading, isError } = useQuery({
    queryKey: ['invite', token], queryFn: () => authApi.getInvite(token), retry: false,
  })
  const accept = useMutation({ mutationFn: (password: string) => authApi.acceptInvite(token, password) })
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    if (password.length < 8) { setError(t('auth.passwordMin')); return }
    try {
      await accept.mutateAsync(password)
      await qc.invalidateQueries({ queryKey: meQueryKey })
      navigate('/')
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  return (
    <div className="auth-solo">
      <div className="card card-pad">
        <div className="brand-row"><span className="brand-mark"><Home size={17} /></span> {t('app.name')}</div>

        {isLoading && <p className="hint">{t('common.loading')}</p>}

        {isError && (
          <>
            <h2>{t('household.inviteInvalid')}</h2>
            <p className="hint" style={{ marginTop: 8 }}>{t('household.inviteInvalidSub')}</p>
            <p style={{ marginTop: 12 }}><Link className="linkbtn" to="/login">{t('auth.signin')}</Link></p>
          </>
        )}

        {invite && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <h2>{t('household.joinTitle', { household: invite.householdName })}</h2>
              <p className="hint" style={{ marginTop: 6 }}>{t('household.joinSub', { email: invite.email })}</p>
            </div>
            {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
            <div className="field">
              <label>{t('auth.password')}</label>
              <input className="inp" type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>
            <button className="btn primary" type="button" onClick={() => void submit()} disabled={accept.isPending}>
              {t('household.acceptJoin')}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
