import type { ComponentType, CSSProperties } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Archive, Bell, Blocks, Calendar, CheckSquare, Home, Kanban, Lock, Mail,
  ScrollText, Settings, StickyNote, Users, Wallet, Zap,
} from 'lucide-react'
import { useMe } from '@/platform/auth/useAuth'
import { toast } from '@/platform/ui/toastStore'
import { useApps, useSetAppCapabilities, useSetAppEnabled } from '@/platform/apps/useApps'
import type { AppInfo } from '@/platform/apps/api'

/** Maps a manifest icon name to its lucide component (matches the icons used in the nav rail). */
const ICONS: Record<string, ComponentType<{ size?: number }>> = {
  Home, Users, Mail, ScrollText, Blocks, Settings, CheckSquare, Kanban,
  Wallet, Calendar, Bell, StickyNote, Archive, Zap,
}

const mc = (hue: string) => ({ ['--mc' as string]: hue } as CSSProperties)

/**
 * The household's app catalogue. Owner/Admin can install/remove apps and review the abilities each one has;
 * everyone else sees the current state read-only. Core platform surfaces are always on.
 */
export function AppsPage() {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const { data: apps, isLoading } = useApps()
  const setEnabled = useSetAppEnabled()
  const setCaps = useSetAppCapabilities()
  const isManager = !!me?.roles.some((r) => r === 'Owner' || r === 'Admin')

  const toggleEnabled = (app: AppInfo) => {
    setEnabled.mutate({ id: app.id, enabled: !app.enabled }, {
      onSuccess: () => toast.success(t(app.enabled ? 'apps.toast.disabled' : 'apps.toast.enabled', { name: t(app.nameKey) })),
      onError: (e: Error) => toast.error(e.message),
    })
  }

  const toggleCap = (app: AppInfo, cap: string) => {
    const next = app.grantedCapabilities.includes(cap)
      ? app.grantedCapabilities.filter((c) => c !== cap)
      : [...app.grantedCapabilities, cap]
    setCaps.mutate({ id: app.id, capabilities: next }, {
      onSuccess: () => toast.success(t('apps.toast.capsSaved', { name: t(app.nameKey) })),
      onError: (e: Error) => toast.error(e.message),
    })
  }

  const installable = (apps ?? []).filter((a) => !a.isCore)
  const core = (apps ?? []).filter((a) => a.isCore)

  const renderCard = (app: AppInfo) => {
    const Icon = ICONS[app.icon] ?? Blocks
    return (
      <div className={`app-card${app.enabled ? '' : ' off'}`} key={app.id} style={mc(app.hue)}>
        <div className="app-head">
          <span className="app-ico"><Icon size={20} /></span>
          <div className="app-name">
            <div className="nm">{t(app.nameKey)}</div>
            <div className="rt">{app.route}</div>
          </div>
          {app.isCore ? (
            <span className="app-core" title={t('apps.coreHint')}><Lock size={12} /> {t('apps.core')}</span>
          ) : (
            <label className="sw" title={app.enabled ? t('apps.on') : t('apps.off')}>
              <input type="checkbox" checked={app.enabled} disabled={!isManager || setEnabled.isPending} onChange={() => toggleEnabled(app)} />
              <span className="track" />
            </label>
          )}
        </div>

        <p className="app-desc">{t(app.descriptionKey)}</p>

        {app.capabilities.length > 0 && (
          <div className="app-caps">
            <span className="lab">{t('apps.permissions')}</span>
            <div className="caps">
              {app.capabilities.map((cap) => {
                const on = app.grantedCapabilities.includes(cap)
                const [verb] = cap.split(':')
                return (
                  <button
                    key={cap}
                    type="button"
                    className={`cap${on ? ' on' : ''}`}
                    disabled={!isManager || !app.enabled || setCaps.isPending}
                    onClick={() => toggleCap(app, cap)}
                    title={cap}
                  >
                    {t(`apps.cap.${verb}`, { defaultValue: verb })}
                  </button>
                )
              })}
            </div>
          </div>
        )}
      </div>
    )
  }

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow">{t('nav.apps')}</div>
          <h1>{t('apps.title')}</h1>
          <p className="sub">{t('apps.sub')}</p>
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {!isLoading && (
        <>
          {!isManager && (
            <div className="banner-info">{t('apps.readOnly')}</div>
          )}

          <div className="section-lab">{t('apps.installed')}</div>
          <div className="apps-grid">{installable.map(renderCard)}</div>

          <div className="section-lab">{t('apps.coreSection')}</div>
          <div className="apps-grid">{core.map(renderCard)}</div>
        </>
      )}
    </div>
  )
}
