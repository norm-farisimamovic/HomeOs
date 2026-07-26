import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Wallet } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { useMembers } from '@/platform/members/useMembers'
import { ApiError } from '@/platform/api/client'
import { useAddTransaction } from './hooks'

export function AddTransactionModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const { data: members = [] } = useMembers()
  const add = useAddTransaction()
  const [kind, setKind] = useState('Expense')
  const [amount, setAmount] = useState('')
  const [category, setCategory] = useState('')
  const [date, setDate] = useState('')
  const [paidById, setPaidById] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    const value = Number(amount.replace(',', '.'))
    if (!(value > 0) || !category.trim()) { setError(t('finance.txRequired')); return }
    try {
      await add.mutateAsync({
        kind, amount: value, category: category.trim(),
        occurredOn: date || null, description: description.trim() || undefined, paidById: paidById || null,
      })
      onClose()
    } catch (e) { setError(e instanceof ApiError ? e.message : t('common.error')) }
  }

  return (
    <Modal
      icon={Wallet} hue="var(--m-finance)" title={t('finance.addTx')} subtitle={t('finance.addTxSub')} onClose={onClose}
      footer={<><div className="spacer" /><button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
        <button className="btn primary" type="button" onClick={() => void submit()} disabled={add.isPending}>{t('common.save')}</button></>}
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
      <div className="field">
        <label>{t('finance.kind')}</label>
        <div className="seg">
          {['Expense', 'Income'].map((k) => (
            <button key={k} type="button" className={kind === k ? 'on' : undefined} onClick={() => setKind(k)}>{t(`finance.${k.toLowerCase()}`)}</button>
          ))}
        </div>
      </div>
      <div className="form-grid">
        <div className="field"><label>{t('finance.amount')} <Req /></label><input className="inp mono" inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="0.00" autoFocus /></div>
        <div className="field"><label>{t('finance.category')} <Req /></label><input className="inp" value={category} onChange={(e) => setCategory(e.target.value)} placeholder={t('finance.categoryPh')} /></div>
        <div className="field"><label>{t('finance.date')}</label><input className="inp" type="date" value={date} onChange={(e) => setDate(e.target.value)} /></div>
        <div className="field"><label>{t('finance.paidBy')}</label>
          <select className="sel" value={paidById} onChange={(e) => setPaidById(e.target.value)}>
            <option value="">{t('finance.me')}</option>
            {members.map((m) => <option key={m.id} value={m.id}>{m.displayName}</option>)}
          </select>
        </div>
      </div>
      <div className="field"><label>{t('finance.note')}</label><input className="inp" value={description} onChange={(e) => setDescription(e.target.value)} placeholder={t('finance.notePh')} /></div>
    </Modal>
  )
}
