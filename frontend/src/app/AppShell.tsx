import type { ComponentType } from 'react'
import { useEffect, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Archive, Bell, Blocks, Calendar, CheckSquare, Home, Kanban, LogOut, Mail, Menu, MessageCircle,
  Moon, ScrollText, Search, Settings, ShoppingCart, Sparkles, StickyNote, Sun, User, Users, Wallet, Zap,
} from 'lucide-react'
import { useUiStore } from '@/platform/ui/uiStore'
import { useLogout, useMe } from '@/platform/auth/useAuth'
import { useApps } from '@/platform/apps/useApps'
import { useNotifications } from '@/platform/notifications/useNotifications'
import { useNotificationsRealtime } from '@/platform/notifications/useRealtime'
import { Avatar } from '@/shared/components/Avatar'
import { ErrorBoundary } from '@/shared/components/ErrorBoundary'
import { GlobalLoadingBar } from '@/shared/components/GlobalLoadingBar'
import { TaskModal } from '@/apps/tasks/TaskModal'
import { NoteModal } from '@/apps/notes/NoteModal'
import { ReminderModal } from '@/apps/reminders/ReminderModal'
import { CommandPalette } from '@/app/components/CommandPalette'
import { HouseholdMenu } from '@/app/components/HouseholdMenu'
import './app.css'

interface NavEntry {
  to: string
  labelKey: string
  icon: ComponentType<{ size?: number; className?: string }>
  hue: string
  end?: boolean
  group?: 'apps' | 'household'
  managerOnly?: boolean
  /** When set, the entry is hidden if the household has disabled this app. */
  appId?: string
}

const NAV: NavEntry[] = [
  { to: '/', labelKey: 'nav.dashboard', icon: Home, hue: 'var(--brand)', end: true },
  { to: '/tasks', labelKey: 'nav.tasks', icon: CheckSquare, hue: 'var(--m-tasks)', group: 'apps', appId: 'tasks' },
  { to: '/boards', labelKey: 'nav.boards', icon: Kanban, hue: 'var(--m-boards)', appId: 'kanban' },
  { to: '/calendar', labelKey: 'nav.calendar', icon: Calendar, hue: 'var(--m-calendar)', appId: 'calendar' },
  { to: '/reminders', labelKey: 'nav.reminders', icon: Bell, hue: 'var(--m-reminders)', appId: 'reminders' },
  { to: '/notes', labelKey: 'nav.notes', icon: StickyNote, hue: 'var(--m-notes)', appId: 'notes' },
  { to: '/finance', labelKey: 'nav.finance', icon: Wallet, hue: 'var(--m-finance)', appId: 'finance' },
  { to: '/life', labelKey: 'nav.life', icon: Archive, hue: 'var(--m-life)', appId: 'life' },
  { to: '/shopping', labelKey: 'nav.shopping', icon: ShoppingCart, hue: 'var(--m-life)', appId: 'shopping' },
  { to: '/chat', labelKey: 'nav.chat', icon: MessageCircle, hue: 'var(--m-boards)', appId: 'chat', group: 'household' },
  { to: '/assistant', labelKey: 'nav.assistant', icon: Sparkles, hue: 'var(--brand)' },
  { to: '/household', labelKey: 'nav.household', icon: Users, hue: 'var(--text-3)' },
  { to: '/notifications', labelKey: 'nav.notifications', icon: Mail, hue: 'var(--text-3)' },
  { to: '/automations', labelKey: 'nav.automations', icon: Zap, hue: 'var(--text-3)', appId: 'automations' },
  { to: '/audit', labelKey: 'nav.audit', icon: ScrollText, hue: 'var(--text-3)', managerOnly: true },
  { to: '/apps', labelKey: 'nav.apps', icon: Blocks, hue: 'var(--text-3)' },
  { to: '/settings', labelKey: 'nav.settings', icon: Settings, hue: 'var(--text-3)' },
]

export function AppShell() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)
  const railCollapsed = useUiStore((s) => s.railCollapsed)
  const toggleRail = useUiStore((s) => s.toggleRail)
  const { data: me } = useMe()
  const { data: apps } = useApps()
  const logout = useLogout()
  useNotificationsRealtime()
  const { data: notifications } = useNotifications()
  const unread = notifications?.unread ?? 0
  // Hide app entries the household has disabled; until apps load, show everything (no flicker of a full rail).
  const enabledAppIds = apps ? new Set(apps.filter((a) => a.enabled).map((a) => a.id)) : null

  const [menuOpen, setMenuOpen] = useState(false)
  const [quickMenu, setQuickMenu] = useState(false)
  const [quick, setQuick] = useState<'task' | 'note' | 'reminder' | null>(null)
  const [paletteOpen, setPaletteOpen] = useState(false)

  const isDark = theme === 'dark' ||
    (theme === 'system' && typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches)
  const initials = me?.displayName?.trim().charAt(0).toUpperCase() || '?'
  const nextLang = i18n.resolvedLanguage === 'bs' ? 'en' : 'bs'
  const isManager = !!me?.roles.some((r) => r === 'Owner' || r === 'Admin')

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); setPaletteOpen(true) }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const onLogout = async () => { await logout.mutateAsync(); navigate('/login') }
  const closeRail = () => document.body.classList.remove('rail-open')
  // Mobile: the hamburger opens the off-canvas rail; desktop: it collapses the rail to icons.
  const onHamburger = () => {
    if (window.matchMedia('(max-width: 760px)').matches) document.body.classList.toggle('rail-open')
    else toggleRail()
  }

  return (
    <div className={`shell${railCollapsed ? ' rail-collapsed' : ''}`}>
      <GlobalLoadingBar />
      <aside className="rail">
        <div className="brand">
          <span className="mark"><Home size={18} /></span>
          <span className="nm">Home<span>OS</span></span>
        </div>

        <nav className="nav">
          {NAV
            .filter((item) => !item.managerOnly || isManager)
            .filter((item) => !item.appId || !enabledAppIds || enabledAppIds.has(item.appId))
            .map((item) => (
            <div key={item.to} style={{ display: 'contents' }}>
              {item.group && <div className="nav-grp">{t(`nav.group.${item.group}`)}</div>}
              <NavLink to={item.to} end={item.end} onClick={closeRail}
                style={{ ['--mc' as string]: item.hue }}
                className={({ isActive }) => (isActive ? 'on' : undefined)}>
                <item.icon size={19} className="ic" />
                <span className="label">{t(item.labelKey)}</span>
              </NavLink>
            </div>
          ))}
        </nav>

        <div className="rail-foot">
          <Avatar name={me?.displayName} memberId={me?.id} />
          <div className="who">
            <div className="n">{me?.displayName}</div>
            <div className="h">{me?.householdName} · {me?.roles.join(', ')}</div>
          </div>
        </div>
      </aside>

      <div className="main">
        <header className="top">
          <button className="btn ghost icon" type="button" onClick={onHamburger} aria-label={t('nav.group.apps')} title={t('nav.group.apps')}>
            <Menu size={18} />
          </button>
          <button className="searchbox" type="button" onClick={() => setPaletteOpen(true)}>
            <Search size={15} />
            <span>{t('common.searchPlaceholder')}</span>
            <span className="kbd">Ctrl+K</span>
          </button>
          <div className="spacer" />
          <div className="pop">
            <button className="btn primary" type="button" onClick={() => setQuickMenu((v) => !v)}>
              <Zap size={15} /> <span className="hide-sm">{t('dashboard.quickAdd')}</span>
            </button>
            {quickMenu && (
              <>
                <div style={{ position: 'fixed', inset: 0, zIndex: 50 }} onClick={() => setQuickMenu(false)} />
                <div className="menu">
                  <button type="button" onClick={() => { setQuickMenu(false); setQuick('task') }}><CheckSquare size={15} />{t('tasks.newTask')}</button>
                  <button type="button" onClick={() => { setQuickMenu(false); setQuick('note') }}><StickyNote size={15} />{t('notes.newNote')}</button>
                  <button type="button" onClick={() => { setQuickMenu(false); setQuick('reminder') }}><Bell size={15} />{t('reminders.newReminder')}</button>
                </div>
              </>
            )}
          </div>
          <button className="btn ghost sm langbtn hide-sm" type="button" onClick={() => void i18n.changeLanguage(nextLang)} aria-label={t('common.language')}>
            <span className="code">{i18n.resolvedLanguage?.toUpperCase()}</span>
          </button>
          <button className="btn ghost icon" type="button" onClick={() => setTheme(isDark ? 'light' : 'dark')} aria-label={t('common.theme')}>
            {isDark ? <Sun size={17} /> : <Moon size={17} />}
          </button>
          <button className="btn ghost icon bell" type="button" onClick={() => navigate('/notifications')} aria-label={t('nav.notifications')} title={t('nav.notifications')}>
            <Bell size={17} />
            {unread > 0 && <span className="bell-badge">{unread > 9 ? '9+' : unread}</span>}
          </button>
          <div className="pop">
            <button className="av" type="button" onClick={() => setMenuOpen((v) => !v)} title={me?.displayName}>{initials}</button>
            {menuOpen && (
              <>
                <div style={{ position: 'fixed', inset: 0, zIndex: 50 }} onClick={() => setMenuOpen(false)} />
                <div className="menu">
                  <div className="lab">{me?.email}</div>
                  <button type="button" onClick={() => { setMenuOpen(false); navigate('/profile') }}><User size={15} />{t('nav.profile')}</button>
                  <button type="button" onClick={() => { setMenuOpen(false); navigate('/settings') }}><Settings size={15} />{t('nav.settings')}</button>
                  <button type="button" onClick={() => { setMenuOpen(false); navigate('/household') }}><Users size={15} />{t('nav.household')}</button>
                  <div className="sep" />
                  <HouseholdMenu />
                  <div className="sep" />
                  <button type="button" className="danger" onClick={() => void onLogout()}><LogOut size={15} />{t('auth.logout')}</button>
                </div>
              </>
            )}
          </div>
        </header>

        <div className="scroll">
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </div>
      </div>

      {quick === 'task' && <TaskModal onClose={() => setQuick(null)} />}
      {quick === 'note' && <NoteModal onClose={() => setQuick(null)} />}
      {quick === 'reminder' && <ReminderModal onClose={() => setQuick(null)} />}
      {paletteOpen && <CommandPalette onClose={() => setPaletteOpen(false)} onNewTask={() => setQuick('task')} />}
    </div>
  )
}
