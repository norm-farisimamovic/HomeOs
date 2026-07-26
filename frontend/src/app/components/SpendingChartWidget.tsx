import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { PieChart } from 'lucide-react'
import { useFinanceSummary } from '@/apps/finance/hooks'
import { formatMoney } from '@/platform/money/api'

// A small rotating palette so each category bar gets a distinct, on-brand colour.
const PALETTE = ['var(--m-finance)', 'var(--m-tasks)', 'var(--m-boards)', 'var(--m-calendar)', 'var(--m-reminders)', 'var(--m-notes)']

/** Dashboard chart card: this month's spending broken down by category as horizontal bars. */
export function SpendingChartWidget() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { data: summary } = useFinanceSummary()
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'

  const cats = [...(summary?.byCategory ?? [])].sort((a, b) => b.amount - a.amount).slice(0, 6)
  if (cats.length === 0) return null
  const max = Math.max(...cats.map((c) => c.amount), 1)
  const cur = summary?.currency ?? 'KM'
  const money = (n: number) => formatMoney(n, cur, locale)

  return (
    <div className="card" style={{ cursor: 'pointer' }} onClick={() => navigate('/finance')} role="button" tabIndex={0}>
      <div className="card-h">
        <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-finance)' }} /><h3>{t('dashboard.widgets.spending')}</h3></div>
        <span className="empty-ico sm" style={{ ['--mc' as string]: 'var(--m-finance)' }}><PieChart size={15} /></span>
      </div>
      <div className="card-b">
        <div className="chart-bars">
          {cats.map((c, i) => (
            <div className="chart-row" key={c.category}>
              <div className="chart-lab"><span className="nm">{c.category}</span><span className="amt">{money(c.amount)}</span></div>
              <div className="chart-track">
                <div className="chart-fill" style={{ width: `${Math.round((c.amount / max) * 100)}%`, background: PALETTE[i % PALETTE.length] }} />
              </div>
            </div>
          ))}
        </div>
        <div className="chart-total">{t('dashboard.widgets.thisMonth')}<b>{money(summary?.spent ?? 0)}</b></div>
      </div>
    </div>
  )
}
