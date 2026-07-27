import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Download, FileText, Wallet } from 'lucide-react'
import { useMe } from '@/platform/auth/useAuth'
import { toast } from '@/platform/ui/toastStore'
import { downloadFinancePdf, downloadTasksPdf } from '@/platform/reports/pdf'

/** Downloadable PDF reports (generated client-side from the same authorized data the app shows). */
export function ReportsPage() {
  const { t, i18n } = useTranslation()
  const { data: me } = useMe()
  const [busy, setBusy] = useState<string | null>(null)
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  const household = me?.householdName ?? 'Home OS'

  const run = async (kind: string, fn: () => Promise<void>) => {
    setBusy(kind)
    try { await fn() } catch { toast.error(t('common.error')) } finally { setBusy(null) }
  }

  const reports = [
    { id: 'finance', icon: Wallet, hue: 'var(--m-finance)', fn: () => downloadFinancePdf({ t, locale, household }) },
    { id: 'tasks', icon: FileText, hue: 'var(--m-tasks)', fn: () => downloadTasksPdf({ t, locale, household }) },
  ]

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow">{t('nav.reports')}</div>
          <h1>{t('reports.title')}</h1>
          <p className="sub">{t('reports.sub')}</p>
        </div>
      </div>

      <div className="grid g2" style={{ alignItems: 'start' }}>
        {reports.map((r) => (
          <div className="card" key={r.id}>
            <div className="card-b" style={{ display: 'flex', gap: 14, alignItems: 'flex-start' }}>
              <span className="empty-ico sm" style={{ ['--mc' as string]: r.hue }}><r.icon size={16} /></span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <h3 style={{ margin: '0 0 2px' }}>{t(`reports.${r.id}.title`)}</h3>
                <p className="hint" style={{ margin: '0 0 12px' }}>{t(`reports.${r.id}.desc`)}</p>
                <button className="btn primary" type="button" disabled={busy === r.id} onClick={() => void run(r.id, r.fn)}>
                  <Download size={15} />{busy === r.id ? t('common.loading') : t('reports.download')}
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
