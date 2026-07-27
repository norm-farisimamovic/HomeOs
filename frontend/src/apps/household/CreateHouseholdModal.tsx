import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Home } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { toast } from '@/platform/ui/toastStore'
import { useCreateHousehold, useSwitchHousehold } from '@/platform/households/api'

/**
 * Create an additional household you own (e.g. "Home" and "Work"). This makes a linked account for you — it
 * never asks for another email, so it doesn't touch the member-uniqueness checks used for inviting people.
 * On success we switch straight into the new household.
 */
export function CreateHouseholdModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const create = useCreateHousehold()
  const switchTo = useSwitchHousehold()
  const [name, setName] = useState('')

  const submit = () => {
    const v = name.trim()
    if (!v || create.isPending || switchTo.isPending) return
    create.mutate(v, {
      onSuccess: (r) => {
        toast.success(t('households.created'))
        switchTo.mutate(r.householdId, { onSuccess: () => window.location.assign('/') })
      },
      onError: () => toast.error(t('common.error')),
    })
  }

  const busy = create.isPending || switchTo.isPending

  return (
    <Modal
      title={t('households.create')}
      subtitle={t('households.createHint')}
      icon={Home}
      hue="var(--brand)"
      onClose={onClose}
      footer={
        <>
          <button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary" type="button" onClick={submit} disabled={busy || !name.trim()}>{t('households.create')}</button>
        </>
      }
    >
      <div className="field">
        <label>{t('households.name')}</label>
        <input className="inp" autoFocus value={name} maxLength={60} placeholder={t('households.namePh')}
          onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') submit() }} />
      </div>
    </Modal>
  )
}
