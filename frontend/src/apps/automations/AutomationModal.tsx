import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Zap } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { ApiError } from '@/platform/api/client'
import { ACTIONS, type Automation, TRIGGERS } from './api'
import { useCreateAutomation, useUpdateAutomation } from './hooks'

const key = (v: string) => v.replaceAll('.', '_')

/** Create or edit an automation rule. */
export function AutomationModal({ rule, onClose }: { rule?: Automation; onClose: () => void }) {
  const { t } = useTranslation()
  const create = useCreateAutomation()
  const update = useUpdateAutomation()
  const editing = rule !== undefined

  const [name, setName] = useState(rule?.name ?? '')
  const [trigger, setTrigger] = useState(rule?.trigger ?? TRIGGERS[0])
  const [action, setAction] = useState(rule?.action ?? ACTIONS[0])
  const [message, setMessage] = useState(rule?.message ?? '')
  const [error, setError] = useState<string | null>(null)

  const busy = create.isPending || update.isPending

  const submit = async () => {
    if (!name.trim()) { setError(t('automations.required')); return }
    const input = { name: name.trim(), trigger, action, message: message.trim() || undefined, enabled: rule?.enabled ?? true }
    try {
      if (editing) await update.mutateAsync({ id: rule.id, input })
      else await create.mutateAsync(input)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  return (
    <Modal
      icon={Zap} hue="var(--brand)"
      title={editing ? t('automations.editRule') : t('automations.newRule')}
      subtitle={t('automations.modalSub')}
      onClose={onClose}
      footer={
        <>
          <div className="spacer" />
          <button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary" type="button" onClick={() => void submit()} disabled={busy}>{t('common.save')}</button>
        </>
      }
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
      <div className="field">
        <label>{t('automations.fName')} <Req /></label>
        <input className="inp" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('automations.fNamePh')} autoFocus />
      </div>
      <div className="field">
        <label>{t('automations.fWhen')}</label>
        <select className="sel" value={trigger} onChange={(e) => setTrigger(e.target.value)}>
          {TRIGGERS.map((tr) => <option key={tr} value={tr}>{t(`automations.triggers.${key(tr)}`)}</option>)}
        </select>
      </div>
      <div className="field">
        <label>{t('automations.fThen')}</label>
        <select className="sel" value={action} onChange={(e) => setAction(e.target.value)}>
          {ACTIONS.map((ac) => <option key={ac} value={ac}>{t(`automations.actions.${ac}`)}</option>)}
        </select>
      </div>
      <div className="field">
        <label>{t('automations.fMessage')}</label>
        <input className="inp" value={message} onChange={(e) => setMessage(e.target.value)} placeholder={t('automations.fMessagePh')} />
      </div>
    </Modal>
  )
}
