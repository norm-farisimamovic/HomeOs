import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { ScrollText } from 'lucide-react'
import { useMe } from '@/platform/auth/useAuth'
import { auditApi } from '@/platform/audit/api'

const key = (v: string) => v.replaceAll('.', '_')

/** Owner/Admin-only activity log. Non-managers see an access notice. */
export function AuditPage() {
  const { t, i18n } = useTranslation()
  const { data: me } = useMe()
  const isManager = !!me?.roles.some((r) => r === 'Owner' || r === 'Admin')
  const { data: entries } = useQuery({ queryKey: ['audit'], queryFn: auditApi.list, enabled: isManager })

  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  const fmt = (iso: string) => new Date(iso).toLocaleString(locale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt"><div className="eyebrow">{t('nav.audit')}</div><h1>{t('audit.title')}</h1><p className="sub">{t('audit.sub')}</p></div>
      </div>

      {!isManager && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico"><ScrollText size={22} /></span>
          <h4>{t('audit.noAccess')}</h4><p>{t('audit.noAccessSub')}</p>
        </div></div>
      )}

      {isManager && (
        <div className="card"><div className="card-b flush scroll-list">
          {(entries?.length ?? 0) === 0 && (
            <div className="empty"><span className="empty-ico"><ScrollText size={22} /></span><h4>{t('audit.empty')}</h4></div>
          )}
          {(entries ?? []).map((e) => (
            <div className="row-item" key={e.id}>
              <div className="body">
                <div className="ttl">{t(`audit.actions.${key(e.action)}`, { defaultValue: e.action })}<span className="meta"> · {e.detail}</span></div>
                <div className="meta">{e.actorName ?? t('audit.system')} · {fmt(e.createdAt)}</div>
              </div>
            </div>
          ))}
        </div></div>
      )}
    </div>
  )
}
