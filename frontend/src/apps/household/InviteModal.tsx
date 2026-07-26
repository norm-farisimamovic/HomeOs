import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { UserPlus } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { ApiError } from '@/platform/api/client'
import { ROLES } from './api'
import { useInviteMember } from './hooks'

/** Invite a new household member by email. */
export function InviteModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const invite = useInviteMember()
  const [email, setEmail] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [role, setRole] = useState('Adult')
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    if (!email.trim() || !firstName.trim() || !lastName.trim()) { setError(t('household.inviteRequired')); return }
    try {
      await invite.mutateAsync({ email: email.trim(), firstName: firstName.trim(), lastName: lastName.trim(), role })
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  return (
    <Modal
      icon={UserPlus}
      size="sm"
      title={t('household.invite')}
      subtitle={t('household.inviteSub')}
      onClose={onClose}
      footer={
        <>
          <div className="spacer" />
          <button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary" type="button" onClick={() => void submit()} disabled={invite.isPending}>{t('household.sendInvite')}</button>
        </>
      }
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
      <div className="form-grid">
        <div className="field">
          <label>{t('household.fFirstName')} <Req /></label>
          <input className="inp" value={firstName} onChange={(e) => setFirstName(e.target.value)} placeholder="Lejla" autoFocus />
        </div>
        <div className="field">
          <label>{t('household.fLastName')} <Req /></label>
          <input className="inp" value={lastName} onChange={(e) => setLastName(e.target.value)} placeholder="Hadžić" />
        </div>
      </div>
      <div className="field">
        <label>{t('auth.email')} <Req /></label>
        <input className="inp" type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="lejla@dom.ba" />
      </div>
      <div className="field">
        <label>{t('household.fRole')}</label>
        <select className="sel" value={role} onChange={(e) => setRole(e.target.value)}>
          {ROLES.filter((r) => r !== 'Owner').map((r) => (
            <option key={r} value={r}>{t(`household.role.${r.toLowerCase()}`)}</option>
          ))}
        </select>
      </div>
    </Modal>
  )
}
