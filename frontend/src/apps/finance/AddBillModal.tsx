import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CalendarClock } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { useMembers } from '@/platform/members/useMembers'
import { ApiError } from '@/platform/api/client'
import { useAddBill } from './hooks'

const cadences = ['Monthly', 'Quarterly', 'Yearly', 'OneOff']

export function AddBillModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const { data: members = [] } = useMembers()
  const add = useAddBill()
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('')
  const [cadence, setCadence] = useState('Monthly')
  const [nextDue, setNextDue] = useState('')
  const [category, setCategory] = useState('')
  const [whoPaysId, setWhoPaysId] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    const value = Number(amount.replace(',', '.'))
    if (!name.trim() || !(value > 0) || !nextDue) { setError(t('finance.billRequired')); return }
    try {
      await add.mutateAsync({
        name: name.trim(), amount: value, cadence, nextDue,
        category: category.trim() || 'Bills', whoPaysId: whoPaysId || null,
      })
      onClose()
    } catch (e) { setError(e instanceof ApiError ? e.message : t('common.error')) }
  }

  return (
    <Modal
      icon={CalendarClock} hue="var(--m-finance)" title={t('finance.addBill')} subtitle={t('finance.addBillSub')} onClose={onClose}
      footer={<><div className="spacer" /><button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
        <button className="btn primary" type="button" onClick={() => void submit()} disabled={add.isPending}>{t('common.save')}</button></>}
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
      <div className="field"><label>{t('finance.billName')} <Req /></label><input className="inp" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('finance.billNamePh')} autoFocus /></div>
      <div className="form-grid">
        <div className="field"><label>{t('finance.amount')} <Req /></label><input className="inp mono" inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="0.00" /></div>
        <div className="field"><label>{t('finance.nextDue')} <Req /></label><input className="inp" type="date" value={nextDue} onChange={(e) => setNextDue(e.target.value)} /></div>
        <div className="field"><label>{t('finance.cadence')}</label>
          <select className="sel" value={cadence} onChange={(e) => setCadence(e.target.value)}>
            {cadences.map((c) => <option key={c} value={c}>{t(`finance.cadences.${c.toLowerCase()}`)}</option>)}
          </select>
        </div>
        <div className="field"><label>{t('finance.category')}</label><input className="inp" value={category} onChange={(e) => setCategory(e.target.value)} placeholder={t('finance.categoryPh')} /></div>
      </div>
      <div className="field"><label>{t('finance.whoPays')}</label>
        <select className="sel" value={whoPaysId} onChange={(e) => setWhoPaysId(e.target.value)}>
          <option value="">{t('finance.me')}</option>
          {members.map((m) => <option key={m.id} value={m.id}>{m.displayName}</option>)}
        </select>
      </div>
    </Modal>
  )
}
