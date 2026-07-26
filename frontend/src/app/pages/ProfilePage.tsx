import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Camera, Trash2 } from 'lucide-react'
import { useChangePassword, useMe, useUpdateProfile, meQueryKey } from '@/platform/auth/useAuth'
import { Avatar } from '@/shared/components/Avatar'
import { Req } from '@/shared/components/Req'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { currenciesApi } from '@/platform/money/api'
import { authApi } from '@/platform/auth/api'

export function ProfilePage() {
  const { t, i18n } = useTranslation()
  const { data: me } = useMe()
  const updateProfile = useUpdateProfile()
  const changePassword = useChangePassword()

  const [firstName, setFirstName] = useState(me?.firstName ?? '')
  const [lastName, setLastName] = useState(me?.lastName ?? '')
  const [culture, setCulture] = useState(me?.preferredCulture ?? 'bs')
  const [currency, setCurrency] = useState(me?.preferredCurrency ?? 'BAM')
  const [digest, setDigest] = useState(me?.digestFrequency ?? 'Off')
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [previewing, setPreviewing] = useState(false)
  const { data: currencies = [] } = useQuery({ queryKey: ['currencies'], queryFn: currenciesApi.list })
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [bust, setBust] = useState(0)

  const onPickAvatar = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    try {
      await authApi.uploadAvatar(file)
      await qc.invalidateQueries({ queryKey: meQueryKey })
      setBust(Date.now())
      toast.success(t('profile.avatarSaved'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error'))
    }
  }

  const removeAvatar = async () => {
    try {
      await authApi.deleteAvatar()
      await qc.invalidateQueries({ queryKey: meQueryKey })
      setBust(Date.now())
      toast.success(t('profile.avatarRemoved'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.error'))
    }
  }

  const saveProfile = async () => {
    if (!firstName.trim() || !lastName.trim()) { toast.error(t('error.profile.nameRequired', { defaultValue: t('auth.required') })); return }
    try {
      await updateProfile.mutateAsync({ firstName: firstName.trim(), lastName: lastName.trim(), preferredCulture: culture, preferredCurrency: currency, digestFrequency: digest })
      await i18n.changeLanguage(culture)
      toast.success(t('profile.saved'))
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  const sendPreview = async () => {
    setPreviewing(true)
    try {
      const { sent } = await authApi.sendDigestPreview()
      toast[sent ? 'success' : 'info'](t(sent ? 'profile.digestSent' : 'profile.digestEmpty'))
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t('common.error'))
    } finally {
      setPreviewing(false)
    }
  }

  const savePassword = async () => {
    if (next.length < 8) { toast.error(t('auth.passwordMin')); return }
    try {
      await changePassword.mutateAsync({ current, next })
      toast.success(t('profile.pwChanged')); setCurrent(''); setNext('')
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt"><div className="eyebrow">{t('nav.profile')}</div><h1>{t('profile.title')}</h1><p className="sub">{t('profile.sub')}</p></div>
      </div>

      <div className="grid g2" style={{ alignItems: 'start' }}>
        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('profile.details')}</h3></div></div>
          <div className="card-b" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ display: 'flex', gap: 13, alignItems: 'center' }}>
              <button type="button" className="avatar-edit" onClick={() => fileRef.current?.click()} title={t('profile.changePhoto')}>
                <Avatar name={me?.displayName} memberId={me?.id} bust={bust} size="lg" />
                <span className="avatar-edit-badge"><Camera size={12} /></span>
              </button>
              <input ref={fileRef} type="file" accept="image/*" hidden onChange={(e) => void onPickAvatar(e)} />
              <div>
                <div className="ttl">{me?.displayName}</div>
                <div className="hint">{me?.email} · {me?.roles.join(', ')}</div>
                {me?.hasAvatar && <button type="button" className="mini-link" onClick={() => void removeAvatar()}><Trash2 size={11} /> {t('profile.removePhoto')}</button>}
              </div>
            </div>
            <div className="form-grid">
              <div className="field"><label>{t('profile.firstName')} <Req /></label><input className="inp" autoComplete="given-name" value={firstName} onChange={(e) => setFirstName(e.target.value)} /></div>
              <div className="field"><label>{t('profile.lastName')} <Req /></label><input className="inp" autoComplete="family-name" value={lastName} onChange={(e) => setLastName(e.target.value)} /></div>
            </div>
            <div className="form-grid">
              <div className="field"><label>{t('settings.language')}</label>
                <select className="sel" value={culture} onChange={(e) => setCulture(e.target.value)}>
                  <option value="bs">Bosanski</option><option value="en">English</option>
                </select>
              </div>
              <div className="field"><label>{t('profile.currency')}</label>
                <select className="sel" value={currency} onChange={(e) => setCurrency(e.target.value)}>
                  {currencies.map((c) => <option key={c.code} value={c.code}>{c.code} · {c.symbol} — {c.name}</option>)}
                </select>
              </div>
            </div>
            <div className="field">
              <label>{t('profile.digest')}</label>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <select className="sel" value={digest} onChange={(e) => setDigest(e.target.value as typeof digest)} style={{ flex: 1 }}>
                  <option value="Off">{t('profile.digestOff')}</option>
                  <option value="Daily">{t('profile.digestDaily')}</option>
                  <option value="Weekly">{t('profile.digestWeekly')}</option>
                </select>
                <button className="btn sm" type="button" onClick={() => void sendPreview()} disabled={previewing}>{t('profile.digestPreview')}</button>
              </div>
              <div className="hint">{t('profile.digestHint')}</div>
            </div>
            <div className="btn-row"><button className="btn primary" type="button" onClick={() => void saveProfile()} disabled={updateProfile.isPending}>{t('common.save')}</button></div>
          </div>
        </div>

        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('profile.password')}</h3></div></div>
          <div className="card-b" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="field"><label>{t('profile.current')} <Req /></label><input className="inp" type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} /></div>
            <div className="field"><label>{t('profile.new')} <Req /></label><input className="inp" type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} /></div>
            <div className="btn-row"><button className="btn" type="button" onClick={() => void savePassword()} disabled={changePassword.isPending}>{t('profile.changePassword')}</button></div>
          </div>
        </div>
      </div>
    </div>
  )
}
