import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ChevronRight, Download } from 'lucide-react'
import { ACCENTS, useUiStore } from '@/platform/ui/uiStore'
import { useMe } from '@/platform/auth/useAuth'
import { buildExport, downloadJson } from '@/platform/export/exportData'
import { toast } from '@/platform/ui/toastStore'

export function SettingsPage() {
  const { t, i18n } = useTranslation()
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)
  const density = useUiStore((s) => s.density)
  const setDensity = useUiStore((s) => s.setDensity)
  const accent = useUiStore((s) => s.accent)
  const setAccent = useUiStore((s) => s.setAccent)
  const { data: me } = useMe()
  const [exporting, setExporting] = useState(false)

  const onExport = async () => {
    setExporting(true)
    try {
      const bundle = await buildExport({ id: me?.householdId, name: me?.householdName })
      downloadJson(bundle)
      toast.success(t('settings.exportDone'))
    } catch {
      toast.error(t('common.error'))
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt"><div className="eyebrow">{t('nav.settings')}</div><h1>{t('settings.title')}</h1><p className="sub">{t('settings.sub')}</p></div>
      </div>

      <div className="grid g2" style={{ alignItems: 'start' }}>
        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('settings.appearance')}</h3></div></div>
          <div className="card-b" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="field">
              <label>{t('settings.theme')}</label>
              <div className="seg">
                {(['light', 'dark', 'system'] as const).map((x) => (
                  <button key={x} type="button" className={theme === x ? 'on' : undefined} onClick={() => setTheme(x)}>{t(`settings.${x}`)}</button>
                ))}
              </div>
            </div>
            <div className="field">
              <label>{t('settings.density')}</label>
              <div className="seg">
                {(['cozy', 'compact'] as const).map((x) => (
                  <button key={x} type="button" className={density === x ? 'on' : undefined} onClick={() => setDensity(x)}>{t(`settings.${x}`)}</button>
                ))}
              </div>
            </div>
            <div className="field">
              <label>{t('settings.accent')}</label>
              <div className="accent-row">
                {ACCENTS.map((a) => (
                  <button key={a.id} type="button" className={`accent-dot${accent === a.id ? ' on' : ''}`}
                    style={{ ['--sw' as string]: a.swatch }} onClick={() => setAccent(a.id)}
                    aria-label={t(`settings.accents.${a.id}`)} title={t(`settings.accents.${a.id}`)} />
                ))}
              </div>
            </div>
            <div className="field">
              <label>{t('settings.language')}</label>
              <select className="sel" value={i18n.resolvedLanguage} onChange={(e) => void i18n.changeLanguage(e.target.value)}>
                <option value="bs">Bosanski</option><option value="en">English</option>
              </select>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('settings.more')}</h3></div></div>
          <div className="card-b flush">
            <Link className="row-item" to="/profile"><div className="body"><div className="ttl">{t('nav.profile')}</div><div className="meta">{t('profile.sub')}</div></div><div className="end"><ChevronRight size={16} /></div></Link>
            <Link className="row-item" to="/household"><div className="body"><div className="ttl">{t('nav.household')}</div><div className="meta">{t('settings.householdSub')}</div></div><div className="end"><ChevronRight size={16} /></div></Link>
            <Link className="row-item" to="/notifications"><div className="body"><div className="ttl">{t('nav.notifications')}</div><div className="meta">{t('notifications.sub')}</div></div><div className="end"><ChevronRight size={16} /></div></Link>
          </div>
        </div>

        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('settings.data')}</h3></div></div>
          <div className="card-b" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <p className="hint" style={{ margin: 0 }}>{t('settings.exportSub')}</p>
            <button className="btn" type="button" onClick={() => void onExport()} disabled={exporting} style={{ alignSelf: 'flex-start' }}>
              <Download size={15} />{exporting ? t('common.loading') : t('settings.export')}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
