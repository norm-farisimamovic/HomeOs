import type { ComponentType } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Archive, Bell, CalendarDays, CheckSquare, Wallet } from 'lucide-react'
import type { FeedItem } from '@/apps/calendar/api'

const SOURCE: Record<string, { hue: string; icon: ComponentType<{ size?: number }>; route: string }> = {
  tasks: { hue: 'var(--m-tasks)', icon: CheckSquare, route: '/tasks' },
  calendar: { hue: 'var(--m-calendar)', icon: CalendarDays, route: '/calendar' },
  finance: { hue: 'var(--m-finance)', icon: Wallet, route: '/finance' },
  reminders: { hue: 'var(--m-reminders)', icon: Bell, route: '/reminders' },
  life: { hue: 'var(--m-life)', icon: Archive, route: '/life' },
}

/**
 * A modern "coming up" row: a coloured source icon, the title, and an urgency pill that counts down
 * (Today / Tomorrow / in N days / overdue) and shifts colour as the date approaches.
 */
export function UpcomingRow({ item, todayIso, locale }: { item: FeedItem; todayIso: string; locale: string }) {
  const { t } = useTranslation()
  const cfg = SOURCE[item.source] ?? { hue: 'var(--brand)', icon: CalendarDays, route: '/calendar' }
  const Icon = cfg.icon

  const days = Math.round((new Date(`${item.date}T00:00:00`).getTime() - new Date(`${todayIso}T00:00:00`).getTime()) / 86_400_000)
  const urgency = days < 0 ? 'over' : days === 0 ? 'today' : days <= 2 ? 'soon' : 'later'
  const rel = days < 0 ? t('dashboard.due.overdue', { count: -days })
    : days === 0 ? t('dashboard.due.today')
    : days === 1 ? t('dashboard.due.tomorrow')
    : t('dashboard.due.inDays', { count: days })
  const exact = new Date(`${item.date}T00:00:00`).toLocaleDateString(locale, { day: 'numeric', month: 'short' })

  return (
    <Link className="up-row" to={cfg.route} style={{ ['--mc' as string]: cfg.hue }}>
      <span className="up-ic"><Icon size={16} /></span>
      <div className="up-body">
        <div className="up-ttl">{item.title}</div>
        <div className="up-meta">{t(`calendar.legend.${item.source}`)}{item.time ? ` · ${item.time}` : ''} · {exact}</div>
      </div>
      <span className={`up-pill ${urgency}`}>{rel}</span>
    </Link>
  )
}
