import { useEffect, useState } from 'react'
import type { ComponentType } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AtSign, Bell, BellRing, CalendarClock, CheckCheck, CheckSquare, Mail, Share2, Sparkles, Wallet } from 'lucide-react'
import { toast } from '@/platform/ui/toastStore'
import {
  useMarkAllRead, useMarkNotificationRead, useNotificationPrefs, useNotifications, useSaveNotificationPrefs,
} from '@/platform/notifications/useNotifications'
import type { NotificationPref } from '@/platform/notifications/api'

const catIcon: Record<string, ComponentType<{ size?: number }>> = {
  taskAssigned: CheckSquare, reminder: BellRing, billDue: Wallet, shared: Share2, invite: Mail, renewal: CalendarClock, mention: AtSign, assistant: Sparkles,
}
const catHue: Record<string, string> = {
  taskAssigned: 'var(--m-tasks)', reminder: 'var(--m-reminders)', billDue: 'var(--m-finance)',
  shared: 'var(--m-calendar)', invite: 'var(--brand)', renewal: 'var(--m-life)', mention: 'var(--m-boards)', assistant: 'var(--brand)',
}

export function NotificationsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { data } = useNotifications()
  const markRead = useMarkNotificationRead()
  const markAll = useMarkAllRead()

  const { data: serverPrefs } = useNotificationPrefs()
  const savePrefs = useSaveNotificationPrefs()
  const [prefs, setPrefs] = useState<NotificationPref[]>([])
  useEffect(() => { if (serverPrefs) setPrefs(serverPrefs) }, [serverPrefs])

  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  const fmt = (iso: string) => new Date(iso).toLocaleString(locale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

  const togglePref = (category: string) =>
    setPrefs((cur) => cur.map((p) => (p.category === category ? { ...p, email: !p.email } : p)))

  const onSavePrefs = async () => { await savePrefs.mutateAsync(prefs); toast.success(t('common.saved')) }

  const openNotification = (id: string, link: string | null, isRead: boolean) => {
    if (!isRead) markRead.mutate(id)
    if (link) navigate(link)
  }

  const items = data?.items ?? []

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt"><div className="eyebrow">{t('nav.notifications')}</div><h1>{t('notifications.title')}</h1><p className="sub">{t('notifications.sub')}</p></div>
        {(data?.unread ?? 0) > 0 && (
          <div className="actions">
            <button className="btn" type="button" onClick={() => markAll.mutate()}><CheckCheck size={15} />{t('notifications.markAllRead')}</button>
          </div>
        )}
      </div>

      <div className="grid g2" style={{ alignItems: 'start' }}>
        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('notifications.recent')}</h3></div>{(data?.unread ?? 0) > 0 && <span className="chip solid">{data?.unread}</span>}</div>
          <div className="card-b flush">
            {items.length === 0 && (
              <div className="empty">
                <span className="empty-ico"><Bell size={24} /></span>
                <h4>{t('notifications.emptyTitle')}</h4>
                <p>{t('notifications.emptySub')}</p>
              </div>
            )}
            {items.map((n) => {
              const Icon = catIcon[n.category] ?? Bell
              return (
                <div key={n.id} className={`notif-row${n.isRead ? '' : ' unread'}`} onClick={() => openNotification(n.id, n.link, n.isRead)}>
                  <span className="notif-ico" style={{ ['--mc' as string]: catHue[n.category] ?? 'var(--brand)' }}><Icon size={15} /></span>
                  <div className="body">
                    <div className="ttl">{n.title}</div>
                    {n.body && <div className="meta">{n.body}</div>}
                    <div className="meta">{fmt(n.createdAt)}</div>
                  </div>
                  {!n.isRead && <span className="notif-dot" />}
                </div>
              )
            })}
          </div>
        </div>

        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('notifications.email')}</h3></div></div>
          <div className="card-b" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <p className="hint">{t('notifications.emailSub')}</p>
            {prefs.map((p) => (
              <label className="sw" style={{ justifyContent: 'space-between' }} key={p.category}>
                <span className="txt">{t(`notifications.cat.${p.category}`)}</span>
                <input type="checkbox" checked={p.email} onChange={() => togglePref(p.category)} />
                <span className="track" />
              </label>
            ))}
            <div className="btn-row"><button className="btn primary" type="button" onClick={() => void onSavePrefs()} disabled={savePrefs.isPending}>{t('common.save')}</button></div>
          </div>
        </div>
      </div>
    </div>
  )
}
