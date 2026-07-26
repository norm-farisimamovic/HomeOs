import type { CSSProperties } from 'react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ChevronRight, Plus } from 'lucide-react'
import { useMe } from '@/platform/auth/useAuth'
import { useApps } from '@/platform/apps/useApps'
import { ApiStatus } from '@/app/components/ApiStatus'
import { AssistantWidget } from '@/app/components/AssistantWidget'
import { OnboardingCard } from '@/app/components/OnboardingCard'
import { DashboardWidgets } from '@/app/components/DashboardWidgets'
import { UpcomingRow } from '@/app/components/UpcomingRow'
import { useTasks, useTasksSummary } from '@/apps/tasks/hooks'
import { TaskRow } from '@/apps/tasks/TaskRow'
import { TaskModal } from '@/apps/tasks/TaskModal'
import { useFinanceSummary } from '@/apps/finance/hooks'
import { useMonthFeed } from '@/apps/calendar/hooks'
import type { FeedItem } from '@/apps/calendar/api'

const mc = (hue: string) => ({ ['--mc' as string]: hue } as CSSProperties)
function isoLocal(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** "Today" dashboard — greeting, live stats pulled from every app, a unified "coming up" feed, tasks-due, household. */
export function DashboardPage() {
  const { t, i18n } = useTranslation()
  const { data: me } = useMe()
  const { data: summary } = useTasksSummary()
  const { data: tasks } = useTasks()
  const { data: finance } = useFinanceSummary()
  const { data: apps } = useApps()
  const enabledAppIds = new Set((apps ?? []).filter((a) => a.enabled).map((a) => a.id))
  const [modalOpen, setModalOpen] = useState(false)

  const now = new Date()
  const todayIso = isoLocal(now)
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  // Pull this month + next so "coming up" spans month boundaries.
  const { data: feedA } = useMonthFeed(now.getFullYear(), now.getMonth() + 1)
  const nextM = new Date(now.getFullYear(), now.getMonth() + 1, 1)
  const { data: feedB } = useMonthFeed(nextM.getFullYear(), nextM.getMonth() + 1)

  const allFeed: FeedItem[] = [...(feedA?.items ?? []), ...(feedB?.items ?? [])]
  const upcoming = allFeed
    .filter((i) => i.date >= todayIso && !i.isDone)
    .sort((a, b) => a.date.localeCompare(b.date) || (a.time ?? '').localeCompare(b.time ?? ''))
    .slice(0, 7)
  const eventsToday = allFeed.filter((i) => i.date === todayIso && i.source === 'calendar').length

  const greetKey = now.getHours() < 12 ? 'dashboard.morning' : now.getHours() < 18 ? 'dashboard.afternoon' : 'dashboard.evening'
  const dateStr = now.toLocaleDateString(locale, { weekday: 'long', day: 'numeric', month: 'long' })
  const open = (tasks ?? []).filter((task) => !task.isDone).slice(0, 6)
  const money = (n: number) => `${n.toLocaleString(locale, { maximumFractionDigits: 0 })} ${finance?.currency ?? 'KM'}`
  // Brand-new household: everything loaded and still empty → show the onboarding card.
  const isNewHousehold = tasks !== undefined && tasks.length === 0 && feedA !== undefined && upcoming.length === 0

  return (
    <div className="wrap wide">
      <div className="hero">
        <div className="row">
          <div className="greet">
            <div className="eyebrow">{dateStr}</div>
            <h1>{t(greetKey, { name: me?.firstName || me?.displayName || '' })}</h1>
            <p style={{ color: 'var(--text-2)', maxWidth: '56ch' }}>{t('dashboard.subtitle')}</p>
            <div className="stats">
              <Link className="stat" to="/tasks" data-m style={mc('var(--m-tasks)')}><div className="n">{summary?.dueToday ?? 0}</div><div className="l">{t('today.dueToday')}</div></Link>
              <Link className="stat" to="/tasks" data-m style={mc('var(--danger)')}><div className="n">{summary?.overdue ?? 0}</div><div className="l">{t('today.overdue')}</div></Link>
              <Link className="stat" to="/calendar" data-m style={mc('var(--m-calendar)')}><div className="n">{eventsToday}</div><div className="l">{t('today.events')}</div></Link>
              <Link className="stat" to="/finance" data-m style={mc('var(--m-finance)')}><div className="n">{money(finance?.dueSoonAmount ?? 0)}</div><div className="l">{t('today.billsWeek')}</div></Link>
            </div>
          </div>

          <div className="web">
            <div className="eyebrow" style={{ marginBottom: 8 }}>{t('today.web')}</div>
            <svg viewBox="0 0 200 150" role="img" aria-label="Connection map">
              <g>
                <path className="lnk live" d="M40 30 C 90 20 120 40 158 32" />
                <path className="lnk live" d="M158 32 C 178 70 140 88 120 100" />
                <path className="lnk" d="M120 100 C 80 118 60 96 38 92" />
                <path className="lnk" d="M38 92 C 30 40 34 40 40 30" />
                <path className="lnk live" d="M120 100 C 110 128 90 132 74 134" />
              </g>
              <g className="nd">
                <circle cx="40" cy="30" r="7" fill="var(--m-finance)" /><text x="40" y="16">bill</text>
                <circle cx="158" cy="32" r="7" fill="var(--m-tasks)" /><text x="158" y="18">task</text>
                <circle cx="120" cy="100" r="7" fill="var(--m-calendar)" /><text x="120" y="119">calendar</text>
                <circle cx="38" cy="92" r="7" fill="var(--m-reminders)" /><text x="38" y="111">reminder</text>
                <circle cx="74" cy="134" r="6" fill="var(--m-life)" /><text x="74" y="148">renewal</text>
              </g>
            </svg>
          </div>
        </div>
      </div>

      <div className="grid g3" style={{ alignItems: 'start' }}>
        <div style={{ gridColumn: 'span 2', display: 'flex', flexDirection: 'column', gap: 14 }}>
          {isNewHousehold && <OnboardingCard />}
          <AssistantWidget />
          <div className="card">
            <div className="card-h">
              <div className="t"><i className="mdot" style={mc('var(--brand)')} /><h3>{t('today.comingUp')}</h3></div>
              <Link className="btn sm ghost" to="/calendar">{t('common.viewAll')} <ChevronRight size={14} /></Link>
            </div>
            <div className="card-b flush">
              {upcoming.length > 0 ? (
                <div className="up-list">
                  {upcoming.map((i) => <UpcomingRow key={`${i.source}-${i.id}`} item={i} todayIso={todayIso} locale={locale} />)}
                </div>
              ) : (
                <div className="empty"><span className="empty-ico" style={mc('var(--brand)')}>✳</span><h4>{t('today.allClear')}</h4><p>{t('today.allClearSub')}</p></div>
              )}
            </div>
          </div>

          <div className="card">
            <div className="card-h">
              <div className="t"><i className="mdot" style={mc('var(--m-tasks)')} /><h3>{t('today.tasks')}</h3></div>
              <div className="btn-row">
                <button className="btn sm" type="button" onClick={() => setModalOpen(true)}><Plus size={14} />{t('tasks.newTask')}</button>
                <Link className="btn sm ghost" to="/tasks">{t('common.viewAll')} <ChevronRight size={14} /></Link>
              </div>
            </div>
            <div className="card-b flush">
              {open.length > 0
                ? open.map((task) => <TaskRow key={task.id} task={task} />)
                : (
                  <div className="empty">
                    <span className="empty-ico" style={mc('var(--m-tasks)')}>✳</span>
                    <h4>{t('tasks.emptyTitle')}</h4>
                    <p>{t('tasks.emptySub')}</p>
                  </div>
                )}
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Registry-driven, member-reorderable widget column (weather, scoreboard, household, apps…). */}
          <DashboardWidgets enabledAppIds={enabledAppIds} />
          <ApiStatus />
        </div>
      </div>

      {modalOpen && <TaskModal onClose={() => setModalOpen(false)} />}
    </div>
  )
}
