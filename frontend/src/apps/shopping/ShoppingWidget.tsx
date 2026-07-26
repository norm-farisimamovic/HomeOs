import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { ShoppingCart } from 'lucide-react'
import { useShoppingLists } from './hooks'

/** A compact dashboard card for the Shopping app — contributed via the dashboard-widget registry. */
export function ShoppingWidget() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data: lists } = useShoppingLists()
  const active = (lists ?? []).filter((l) => l.items.some((i) => !i.done))
  if ((lists?.length ?? 0) === 0) return null

  return (
    <div className="card" style={{ cursor: 'pointer' }} onClick={() => navigate('/shopping')} role="button" tabIndex={0}>
      <div className="card-h">
        <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-life)' }} /><h3>{t('shopping.title')}</h3></div>
        <span className="empty-ico sm" style={{ ['--mc' as string]: 'var(--m-life)' }}><ShoppingCart size={15} /></span>
      </div>
      <div className="card-b flush">
        {(lists ?? []).slice(0, 4).map((l) => {
          const remaining = l.items.filter((i) => !i.done).length
          return (
            <div className="row-item" key={l.id}>
              <div className="body"><div className="ttl">{l.name}</div></div>
              <span className={`chip${remaining === 0 ? ' ok' : ''}`}>{remaining === 0 ? t('shopping.allDone') : t('shopping.remaining', { count: remaining })}</span>
            </div>
          )
        })}
        {active.length === 0 && <div className="row-item"><div className="body"><div className="meta">{t('shopping.allDone')}</div></div></div>}
      </div>
    </div>
  )
}
