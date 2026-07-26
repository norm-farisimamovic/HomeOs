import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Eye, EyeOff, MailCheck } from 'lucide-react'
import { useRegister } from '@/platform/auth/useAuth'
import { authApi } from '@/platform/auth/api'
import { ApiError } from '@/platform/api/client'
import { AuthLayout } from '@/app/components/AuthLayout'
import { Req } from '@/shared/components/Req'

const schema = z.object({
  firstName: z.string().min(1),
  lastName: z.string().min(1),
  householdName: z.string().min(1),
  email: z.email(),
  password: z.string().min(8),
})
type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const { t, i18n } = useTranslation()
  const registerMember = useRegister()
  const [showPw, setShowPw] = useState(false)
  const [sentTo, setSentTo] = useState<string | null>(null)
  const [resent, setResent] = useState(false)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = handleSubmit(async (values) => {
    const result = await registerMember.mutateAsync({ ...values, preferredCulture: i18n.resolvedLanguage })
    setSentTo(result.email)
  })

  const errorMessage = registerMember.isError
    ? registerMember.error instanceof ApiError ? registerMember.error.message : t('auth.genericError')
    : null

  if (sentTo) {
    return (
      <AuthLayout active="up">
        <div className="auth-form" style={{ textAlign: 'center' }}>
          <div style={{ display: 'grid', placeItems: 'center', gap: 8 }}>
            <span className="empty-ico" style={{ ['--mc' as string]: 'var(--brand)' }}><MailCheck size={24} /></span>
            <h2>{t('auth.checkEmailTitle')}</h2>
            <p className="hint">{t('auth.checkEmailSub', { email: sentTo })}</p>
          </div>
          <button className="btn" type="button" disabled={resent}
            onClick={() => { void authApi.resendConfirmation(sentTo); setResent(true) }}>
            {resent ? t('auth.resent') : t('auth.resend')}
          </button>
          <p className="auth-foot"><Link to="/login">{t('auth.signin')}</Link></p>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout active="up">
      <form className="auth-form" onSubmit={onSubmit} noValidate>
        <div>
          <h2>{t('auth.startHome')}</h2>
          <p className="hint" style={{ marginTop: 4 }}>{t('auth.startSub')}</p>
        </div>

        {errorMessage && <div className="err-msg" role="alert"><span>{errorMessage}</span></div>}

        <div className="form-grid">
          <div className="field">
            <label>{t('auth.firstName')} <Req /></label>
            <input className="inp" type="text" autoComplete="given-name" {...register('firstName')} />
            {errors.firstName && <span className="err-msg">{t('auth.required')}</span>}
          </div>
          <div className="field">
            <label>{t('auth.lastName')} <Req /></label>
            <input className="inp" type="text" autoComplete="family-name" {...register('lastName')} />
            {errors.lastName && <span className="err-msg">{t('auth.required')}</span>}
          </div>
        </div>

        <div className="field">
          <label>{t('auth.householdName')} <Req /></label>
          <input className="inp" type="text" {...register('householdName')} />
          {errors.householdName && <span className="err-msg">{t('auth.required')}</span>}
        </div>

        <div className="field">
          <label>{t('auth.email')} <Req /></label>
          <input className="inp" type="email" autoComplete="email" {...register('email')} />
          {errors.email && <span className="err-msg">{t('auth.invalidEmail')}</span>}
        </div>

        <div className="field">
          <label>{t('auth.password')} <Req /></label>
          <div className="pw-wrap">
            <input className="inp" type={showPw ? 'text' : 'password'} autoComplete="new-password" {...register('password')} />
            <button className="peek" type="button" onClick={() => setShowPw((v) => !v)} aria-label={t('auth.password')}>
              {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
          {errors.password && <span className="err-msg">{t('auth.passwordMin')}</span>}
        </div>

        <button className="btn primary" type="submit" disabled={isSubmitting}>{t('auth.createHome')}</button>

        <div className="auth-foot">{t('auth.haveAccount')} <Link to="/login">{t('auth.signin')}</Link></div>
      </form>
    </AuthLayout>
  )
}
