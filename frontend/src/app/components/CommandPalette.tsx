import type { CSSProperties } from 'react'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Plus, Search, User, X } from 'lucide-react'
import { searchApi, type SearchHit } from '@/platform/search/api'
import { useApps } from '@/platform/apps/useApps'
import { appIcon } from '@/platform/apps/icons'

/**
 * Global search + command palette (⌘/Ctrl-K). It is **registry-driven**: search-result styling and the
 * "go to" list come from the app registry (`useApps`), so a newly installed app surfaces here automatically —
 * no edit to this component.
 */
export function CommandPalette({ onClose, onNewTask }: { onClose: () => void; onNewTask: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data: apps } = useApps()
  const [q, setQ] = useState('')
  const ql = q.trim()

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); onClose() } }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const { data: hits } = useQuery({
    queryKey: ['search', ql.toLowerCase()],
    queryFn: () => searchApi.query(ql),
    enabled: ql.length >= 2,
  })

  const enabled = (apps ?? []).filter((a) => a.enabled)
  const appById = new Map(enabled.map((a) => [a.id, a]))
  // "Go to" destinations = every enabled app (from the registry) + the profile.
  const gotoList = [...enabled.map((a) => ({ to: a.route, label: t(a.nameKey), icon: appIcon(a.icon) })),
    { to: '/profile', label: t('nav.profile'), icon: User }]
    .filter((n) => !ql || n.label.toLowerCase().includes(ql.toLowerCase()))

  const grouped = (hits ?? []).reduce<Record<string, SearchHit[]>>((acc, h) => {
    (acc[h.source] ??= []).push(h)
    return acc
  }, {})

  const go = (to: string) => { onClose(); navigate(to) }
  const mcTasks = { ['--mc' as string]: 'var(--m-tasks)' } as CSSProperties

  return (
    <div className="veil top" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}>
      <div className="palette" role="dialog" aria-label={t('common.search')}>
        <div className="pin">
          <Search size={16} />
          <input autoFocus placeholder={t('common.searchPlaceholder')} value={q} onChange={(e) => setQ(e.target.value)} />
          <button className="palette-x" type="button" onClick={onClose} aria-label={t('common.cancel')}><X size={15} /></button>
        </div>
        <div className="plist">
          <div className="pgroup">{t('palette.actions')}</div>
          <div className="pitem" style={mcTasks} onClick={() => { onClose(); onNewTask() }}>
            <Plus size={16} className="ic" /><span>{t('tasks.newTask')}</span>
          </div>

          {Object.entries(grouped).map(([source, items]) => {
            const app = appById.get(source)
            const Icon = appIcon(app?.icon ?? '')
            const hue = app?.hue ?? 'var(--brand)'
            return (
              <div key={source}>
                <div className="pgroup">{app ? t(app.nameKey) : source}</div>
                {items.map((h) => (
                  <div key={`${source}-${h.id}`} className="pitem" style={{ ['--mc' as string]: hue }} onClick={() => go(h.link)}>
                    <Icon size={16} className="ic" />
                    <span>{h.title}</span>
                    {h.subtitle && <span className="sub">{h.subtitle}</span>}
                  </div>
                ))}
              </div>
            )
          })}

          {ql.length >= 2 && (hits?.length ?? 0) === 0 && <div className="pgroup">{t('common.noResults')}</div>}

          <div className="pgroup">{t('palette.goto')}</div>
          {gotoList.map((n) => (
            <div key={n.to} className="pitem" onClick={() => go(n.to)}>
              <n.icon size={16} className="ic" /><span>{n.label}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
