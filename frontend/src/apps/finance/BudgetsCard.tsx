import type { CSSProperties } from 'react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PiggyBank, Plus, Trash2 } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import { formatMoney } from '@/platform/money/api'
import { useBudgets, useDeleteBudget, useSaveBudget } from './hooks'

const mc = (hue: string) => ({ ['--mc' as string]: hue } as CSSProperties)

/** Per-category monthly budgets with this-month progress. Lives in the Finance right column. */
export function BudgetsCard() {
  const { t, i18n } = useTranslation()
  const { data: budgets } = useBudgets()
  const save = useSaveBudget()
  const del = useDeleteBudget()
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'

  const [category, setCategory] = useState('')
  const [amount, setAmount] = useState('')

  const add = () => {
    const limit = Number(amount)
    if (!category.trim() || !(limit > 0)) return
    save.mutate({ category: category.trim(), monthlyLimit: limit }, {
      onSuccess: () => { setCategory(''); setAmount('') },
    })
  }

  const remove = async (cat: string) => {
    if (await confirm({ title: t('finance.budget.deleteTitle'), message: t('finance.budget.deleteMsg', { category: cat }), confirmLabel: t('common.delete'), danger: true }))
      del.mutate(cat)
  }

  return (
    <div className="card">
      <div className="card-h"><div className="t"><i className="mdot" style={mc('var(--m-life)')} /><h3>{t('finance.budget.title')}</h3></div></div>
      <div className="card-b flush">
        {(budgets?.length ?? 0) === 0 && <div className="empty"><span className="empty-ico" style={mc('var(--m-life)')}><PiggyBank size={18} /></span><p className="hint">{t('finance.budget.empty')}</p></div>}

        {(budgets ?? []).map((b) => {
          const over = b.percent >= 100
          return (
            <div className="row-item budget-row" key={b.category}>
              <div className="body">
                <div className="ttl">{b.category}<span className="meta"> · {formatMoney(b.spent, b.currency, locale)} / {formatMoney(b.limit, b.currency, locale)}</span></div>
                <div className="bud-bar"><div className={`bud-fill${over ? ' over' : ''}`} style={{ width: `${Math.min(b.percent, 100)}%` }} /></div>
              </div>
              <div className="end">
                <span className={`chip${over ? ' danger' : b.percent >= 80 ? ' warn' : ''}`}>{b.percent}%</span>
                <button className="btn sm ghost icon danger" type="button" title={t('common.delete')} onClick={() => void remove(b.category)}><Trash2 size={14} /></button>
              </div>
            </div>
          )
        })}

        <div className="budget-add">
          <input className="inp sm" value={category} onChange={(e) => setCategory(e.target.value)} placeholder={t('finance.budget.category')} />
          <input className="inp sm" type="number" min="0" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder={t('finance.budget.limit')} />
          <button className="btn sm primary" type="button" onClick={add} disabled={save.isPending}><Plus size={14} /></button>
        </div>
      </div>
    </div>
  )
}
