import type { CSSProperties } from 'react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CalendarClock, Paperclip, Plus, Trash2, Wallet } from 'lucide-react'
import { Avatar } from '@/shared/components/Avatar'
import { Attachments } from '@/shared/components/Attachments'
import { Modal } from '@/shared/components/Modal'
import { confirm } from '@/platform/ui/confirmStore'
import { formatMoney } from '@/platform/money/api'
import { useBills, useDeleteBill, useDeleteTransaction, useFinanceSummary, useTransactions } from './hooks'
import { AddTransactionModal } from './AddTransactionModal'
import { AddBillModal } from './AddBillModal'
import { BudgetsCard } from './BudgetsCard'
import { CurrencyPicker } from './CurrencyPicker'

const mc = (hue: string) => ({ ['--mc' as string]: hue } as CSSProperties)

/** Household finance overview — this-month totals, transactions, upcoming bills, and who paid what. */
export function FinancePage() {
  const { t, i18n } = useTranslation()
  const { data: summary } = useFinanceSummary()
  const { data: transactions, isLoading, isError, refetch } = useTransactions()
  const { data: bills } = useBills()
  const delTx = useDeleteTransaction()
  const delBill = useDeleteBill()
  const [txOpen, setTxOpen] = useState(false)
  const [billOpen, setBillOpen] = useState(false)
  const [billFiles, setBillFiles] = useState<{ id: string; name: string } | null>(null)

  const cur = summary?.currency ?? 'KM'
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  const money = (n: number, c = cur) => formatMoney(n, c, locale)
  const fmtDate = (iso: string) => new Date(iso).toLocaleDateString(locale, { day: 'numeric', month: 'short' })

  const askDeleteTx = async (id: string) => {
    if (await confirm({ title: t('finance.confirmDeleteTx.title'), message: t('finance.confirmDeleteTx.message'), confirmLabel: t('common.delete'), danger: true })) delTx.mutate(id)
  }
  const askDeleteBill = async (id: string, name: string) => {
    if (await confirm({ title: t('finance.confirmDeleteBill.title'), message: t('finance.confirmDeleteBill.message', { name }), confirmLabel: t('common.delete'), danger: true })) delBill.mutate(id)
  }

  return (
    <div className="wrap wide">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-finance)' }}>{t('nav.finance')}</div>
          <h1>{t('finance.title')}</h1>
          <p className="sub">{t('finance.sub')}</p>
        </div>
        <div className="actions">
          <CurrencyPicker />
          <button className="btn primary" type="button" onClick={() => setTxOpen(true)}><Plus size={15} />{t('finance.addTx')}</button>
          <button className="btn ghost" type="button" onClick={() => setBillOpen(true)}><CalendarClock size={15} />{t('finance.addBill')}</button>
        </div>
      </div>

      <div className="fin-stats">
        <div className="fin-stat" style={mc('var(--m-finance)')}><div className="n">{money(summary?.income ?? 0, cur)}</div><div className="l">{t('finance.income')}</div></div>
        <div className="fin-stat" style={mc('var(--danger)')}><div className="n">{money(summary?.spent ?? 0, cur)}</div><div className="l">{t('finance.spent')}</div></div>
        <div className="fin-stat" style={mc('var(--brand)')}><div className="n">{money(summary?.balance ?? 0, cur)}</div><div className="l">{t('finance.balance')}</div></div>
        <div className="fin-stat" style={mc('var(--m-reminders)')}><div className="n">{money(summary?.dueSoonAmount ?? 0, cur)}</div><div className="l">{t('finance.dueSoon', { n: summary?.dueSoonCount ?? 0 })}</div></div>
      </div>

      <div className="grid g3" style={{ alignItems: 'start' }}>
        <div style={{ gridColumn: 'span 2', display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div className="card">
            <div className="card-h">
              <div className="t"><i className="mdot" style={mc('var(--m-finance)')} /><h3>{t('finance.transactions')}</h3></div>
              <button className="btn sm" type="button" onClick={() => setTxOpen(true)}><Plus size={14} />{t('finance.addTx')}</button>
            </div>
            <div className="card-b flush scroll-list">
              {isLoading && <div className="empty"><p className="hint">{t('common.loading')}</p></div>}
              {isError && (
                <div className="empty"><p>{t('common.error')}</p>
                  <button className="btn" type="button" onClick={() => void refetch()}>{t('common.retry')}</button></div>
              )}
              {!isLoading && !isError && (transactions?.length ?? 0) === 0 && (
                <div className="empty">
                  <span className="empty-ico" style={mc('var(--m-finance)')}><Wallet size={20} /></span>
                  <h4>{t('finance.emptyTitle')}</h4>
                  <p>{t('finance.emptySub')}</p>
                  <button className="btn primary" type="button" onClick={() => setTxOpen(true)}><Plus size={15} />{t('finance.addTx')}</button>
                </div>
              )}
              {(transactions ?? []).map((tx) => {
                const income = tx.kind === 'Income'
                return (
                  <div className="row-item" key={tx.id}>
                    <Avatar name={tx.paidByName} memberId={tx.paidById} color="var(--m-finance)" />
                    <div className="body">
                      <div className="ttl">{tx.category}{tx.description ? <span className="meta"> · {tx.description}</span> : null}</div>
                      <div className="meta">{tx.paidByName ?? '—'} · {fmtDate(tx.occurredOn)}</div>
                    </div>
                    <div className="end">
                      <span className="mono" style={{ color: income ? 'var(--m-finance)' : 'var(--text-1)', fontWeight: 600 }}>
                        {income ? '+' : '−'}{money(tx.amount, tx.currency)}
                      </span>
                      <button className="btn sm ghost icon danger" type="button" title={t('common.delete')} onClick={() => void askDeleteTx(tx.id)}><Trash2 size={14} /></button>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div className="card">
            <div className="card-h">
              <div className="t"><i className="mdot" style={mc('var(--m-reminders)')} /><h3>{t('finance.upcomingBills')}</h3></div>
              <button className="btn sm" type="button" onClick={() => setBillOpen(true)}><Plus size={14} /></button>
            </div>
            <div className="card-b flush">
              {(bills?.length ?? 0) === 0 && <div className="empty"><p className="hint">{t('finance.noBills')}</p></div>}
              {(bills ?? []).map((b) => (
                <div className="row-item" key={b.id}>
                  <div className="body">
                    <div className="ttl">{b.name}</div>
                    <div className="meta">{t(`finance.cadences.${b.cadence.toLowerCase()}`)} · {fmtDate(b.nextDue)}</div>
                  </div>
                  <div className="end">
                    <span className={`chip${b.dueInDays <= 3 ? ' danger' : ''}`}>{t('finance.inDays', { count: b.dueInDays })}</span>
                    <span className="mono" style={{ fontWeight: 600 }}>{money(b.amount, b.currency)}</span>
                    <button className="btn sm ghost icon" type="button" title={t('attachments.title')} onClick={() => setBillFiles({ id: b.id, name: b.name })}><Paperclip size={14} /></button>
                    <button className="btn sm ghost icon danger" type="button" title={t('common.delete')} onClick={() => void askDeleteBill(b.id, b.name)}><Trash2 size={14} /></button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <BudgetsCard />

          {(summary?.members.length ?? 0) > 0 && (
            <div className="card">
              <div className="card-h"><div className="t"><i className="mdot" style={mc('var(--brand)')} /><h3>{t('finance.whoPaid')}</h3></div></div>
              <div className="card-b flush">
                {(summary?.members ?? []).map((m) => (
                  <div className="row-item" key={m.memberId}>
                    <Avatar name={m.name} memberId={m.memberId} />
                    <div className="body">
                      <div className="ttl">{m.name}</div>
                      <div className="meta">{t('finance.paidTotal')}: {money(m.paid, cur)}</div>
                    </div>
                    <div className="end">
                      <span className={`chip${m.net >= 0 ? ' solid' : ' danger'}`}>
                        {m.net >= 0 ? t('finance.owed') : t('finance.owes')} {money(Math.abs(m.net), cur)}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {txOpen && <AddTransactionModal onClose={() => setTxOpen(false)} />}
      {billOpen && <AddBillModal onClose={() => setBillOpen(false)} />}
      {billFiles && (
        <Modal onClose={() => setBillFiles(null)} icon={Paperclip} title={billFiles.name} hue="var(--m-finance)">
          <Attachments ownerType="bill" ownerId={billFiles.id} />
        </Modal>
      )}
    </div>
  )
}
