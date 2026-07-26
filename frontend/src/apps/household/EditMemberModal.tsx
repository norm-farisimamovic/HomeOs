import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { UserCog } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { ApiError } from '@/platform/api/client'
import type { HouseholdMember } from './api'
import { useUpdateMember } from './hooks'

/** Owner/Admin edits a member's name and (optionally) login email. */
export function EditMemberModal({ member, onClose }: { member: HouseholdMember; onClose: () => void }) {
  const { t } = useTranslation()
  const update = useUpdateMember()
  const [firstName, setFirstName] = useState(member.firstName)
  const [lastName, setLastName] = useState(member.lastName)
  const [email, setEmail] = useState(member.email)
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    if (!firstName.trim() || !lastName.trim()) { setError(t('auth.required')); return }
    try {
      await update.mutateAsync({ id: member.id, input: { firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim() || undefined } })
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  return (
    <Modal
      icon={UserCog} hue="var(--brand)"
      title={t('household.editMember')}
      subtitle={member.displayName}
      onClose={onClose}
      footer={
        <>
          <div className="spacer" />
          <button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary" type="button" onClick={() => void submit()} disabled={update.isPending}>{t('common.save')}</button>
        </>
      }
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
      <div className="form-grid">
        <div className="field"><label>{t('profile.firstName')} <Req /></label><input className="inp" value={firstName} onChange={(e) => setFirstName(e.target.value)} /></div>
        <div className="field"><label>{t('profile.lastName')} <Req /></label><input className="inp" value={lastName} onChange={(e) => setLastName(e.target.value)} /></div>
      </div>
      <div className="field">
        <label>{t('auth.email')}</label>
        <input className="inp" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        <div className="hint">{t('household.emailHint')}</div>
      </div>
    </Modal>
  )
}
