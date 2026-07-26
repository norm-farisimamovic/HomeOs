import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Eye, EyeOff } from 'lucide-react'
import { useLogin } from '@/platform/auth/useAuth'
import { authApi } from '@/platform/auth/api'
import { ApiError } from '@/platform/api/client'
import { AuthLayout } from '@/app/components/AuthLayout'

const schema = z.object({ email: z.email(), password: z.string().min(1) })
type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const login = useLogin()
  const [showPw, setShowPw] = useState(false)
  const [resent, setResent] = useState(false)
  const [remember, setRemember] = useState(true)
  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = handleSubmit(async (values) => {
    await login.mutateAsync({ ...values, rememberMe: remember })
    navigate('/')
  })

  const errorMessage = login.isError
    ? login.error instanceof ApiError ? login.error.message : t('auth.genericError')
    : null
  // A 403 means the account exists but the email isn't confirmed → offer to resend the link.
  const unconfirmed = login.error instanceof ApiError && login.error.status === 403

  return (
    <AuthLayout active="in">
      <form className="auth-form" onSubmit={onSubmit} noValidate>
        <div>
          <h2>{t('auth.welcome')}</h2>
          <p className="hint" style={{ marginTop: 4 }}>{t('auth.welcomeSub')}</p>
        </div>

        {errorMessage && <div className="err-msg" role="alert"><span>{errorMessage}</span></div>}
        {unconfirmed && (
          <button className="btn sm" type="button" disabled={resent}
            onClick={() => { void authApi.resendConfirmation(getValues('email')); setResent(true) }}>
            {resent ? t('auth.resent') : t('auth.resendConfirmation')}
          </button>
        )}

        <div className="field">
          <label>{t('auth.email')}</label>
          <input className="inp" type="email" autoComplete="email" {...register('email')} />
          {errors.email && <span className="err-msg">{t('auth.invalidEmail')}</span>}
        </div>

        <div className="field">
          <div className="row-between">
            <label>{t('auth.password')}</label>
            <Link className="mini-link" to="/forgot-password">{t('auth.forgot')}</Link>
          </div>
          <div className="pw-wrap">
            <input className="inp" type={showPw ? 'text' : 'password'} autoComplete="current-password" {...register('password')} />
            <button className="peek" type="button" onClick={() => setShowPw((v) => !v)} aria-label={t('auth.password')}>
              {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
          {errors.password && <span className="err-msg">{t('auth.required')}</span>}
        </div>

        <label className="remember">
          <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
          <span>{t('auth.rememberMe')}</span>
        </label>

        <button className="btn primary" type="submit" disabled={isSubmitting}>{t('auth.signin')}</button>

        <div className="auth-foot">
          {t('auth.noAccount')} <Link to="/register">{t('auth.createOne')}</Link>
        </div>
      </form>
    </AuthLayout>
  )
}
