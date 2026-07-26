import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CalendarClock, ChevronLeft, ChevronRight, Pencil, Plus } from 'lucide-react'
import type { CalendarEvent, FeedItem } from './api'
import { useMonthFeed, useUpcomingEvents } from './hooks'
import { EventModal } from './EventModal'

type View = 'month' | 'week' | 'day'

const sourceHue: Record<FeedItem['source'], string> = {
  calendar: 'var(--m-calendar)',
  tasks: 'var(--m-tasks)',
  finance: 'var(--m-finance)',
  reminders: 'var(--m-reminders)',
  life: 'var(--m-life)',
}

/** Local YYYY-MM-DD (avoids UTC off-by-one from toISOString). */
function isoLocal(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** Monday of the week containing d. */
function mondayOf(d: Date): Date {
  const offset = (d.getDay() + 6) % 7
  return new Date(d.getFullYear(), d.getMonth(), d.getDate() - offset)
}

export function CalendarPage() {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  const today = new Date()
  const todayIso = isoLocal(today)

  const [view, setView] = useState<View>('month')
  const [focus, setFocus] = useState(today)

  // The days to render, by view.
  const days = useMemo(() => {
    if (view === 'day') return [new Date(focus.getFullYear(), focus.getMonth(), focus.getDate())]
    if (view === 'week') {
      const start = mondayOf(focus)
      return Array.from({ length: 7 }, (_, i) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + i))
    }
    const first = new Date(focus.getFullYear(), focus.getMonth(), 1)
    const offset = (first.getDay() + 6) % 7
    const start = new Date(focus.getFullYear(), focus.getMonth(), 1 - offset)
    return Array.from({ length: 42 }, (_, i) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + i))
  }, [view, focus])

  // Fetch every month the visible range touches. A month grid spans up to 3 months (prev tail, the focused
  // month, next head), so fetch the first day's, the focused, and the last day's month (React Query dedups
  // when they coincide). The focused month is the critical one — omitting it left the grid empty.
  const first = days[0] ?? focus
  const last = days[days.length - 1] ?? focus
  const feedA = useMonthFeed(first.getFullYear(), first.getMonth() + 1)
  const feedB = useMonthFeed(focus.getFullYear(), focus.getMonth() + 1)
  const feedC = useMonthFeed(last.getFullYear(), last.getMonth() + 1)
  const { data: events } = useUpcomingEvents()

  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<CalendarEvent | undefined>(undefined)
  const [defaultDate, setDefaultDate] = useState<string | undefined>(undefined)

  const byDate = useMemo(() => {
    const seen = new Set<string>()
    const map = new Map<string, FeedItem[]>()
    for (const item of [...(feedA.data?.items ?? []), ...(feedB.data?.items ?? []), ...(feedC.data?.items ?? [])]) {
      const key = `${item.source}-${item.id}-${item.date}`
      if (seen.has(key)) continue
      seen.add(key)
      const list = map.get(item.date) ?? []
      list.push(item)
      map.set(item.date, list)
    }
    return map
  }, [feedA.data, feedB.data, feedC.data])

  const weekdays = useMemo(
    () => Array.from({ length: 7 }, (_, i) => new Date(2024, 0, 1 + i).toLocaleDateString(locale, { weekday: 'short' })),
    [locale],
  )

  const label = useMemo(() => {
    if (view === 'day') return focus.toLocaleDateString(locale, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
    if (view === 'week') {
      const s = mondayOf(focus); const e = new Date(s.getFullYear(), s.getMonth(), s.getDate() + 6)
      return `${s.toLocaleDateString(locale, { day: 'numeric', month: 'short' })} – ${e.toLocaleDateString(locale, { day: 'numeric', month: 'short' })}`
    }
    return focus.toLocaleDateString(locale, { month: 'long', year: 'numeric' })
  }, [view, focus, locale])

  const shift = (delta: number) => setFocus((f) => {
    if (view === 'day') return new Date(f.getFullYear(), f.getMonth(), f.getDate() + delta)
    if (view === 'week') return new Date(f.getFullYear(), f.getMonth(), f.getDate() + delta * 7)
    return new Date(f.getFullYear(), f.getMonth() + delta, 1)
  })

  const openNew = (date?: string) => { setEditing(undefined); setDefaultDate(date); setModalOpen(true) }
  const openEdit = (ev: CalendarEvent) => { setEditing(ev); setDefaultDate(undefined); setModalOpen(true) }

  const dayItems = (d: Date) => (byDate.get(isoLocal(d)) ?? []).slice().sort((a, b) => (a.time ?? '99').localeCompare(b.time ?? '99'))

  return (
    <div className="wrap wide">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-calendar)' }}>{t('nav.calendar')}</div>
          <h1>{t('calendar.title')}</h1>
          <p className="sub">{t('calendar.sub')}</p>
        </div>
        <div className="actions">
          <button className="btn primary" type="button" onClick={() => openNew()}><Plus size={15} />{t('calendar.newEvent')}</button>
        </div>
      </div>

      <div className="cal-bar">
        <div className="cal-nav">
          <button className="btn ghost icon sm" type="button" onClick={() => shift(-1)} aria-label={t('calendar.prev')}><ChevronLeft size={16} /></button>
          <span className="cal-month">{label}</span>
          <button className="btn ghost icon sm" type="button" onClick={() => shift(1)} aria-label={t('calendar.next')}><ChevronRight size={16} /></button>
          <button className="btn sm" type="button" onClick={() => setFocus(new Date())}>{t('calendar.today')}</button>
        </div>
        <div className="seg">
          {(['month', 'week', 'day'] as View[]).map((v) => (
            <button key={v} type="button" className={view === v ? 'on' : undefined} onClick={() => setView(v)}>{t(`calendar.view.${v}`)}</button>
          ))}
        </div>
      </div>

      {(feedA.isLoading || feedB.isLoading || feedC.isLoading) && byDate.size === 0 && (
        <div className="card" style={{ marginBottom: 14 }}><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>
      )}

      {view !== 'day' && (
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cal-scroll">
          <div className={`cal-grid${view === 'week' ? ' week' : ''}`}>
            {weekdays.map((w) => <div key={w} className="cal-wd">{w}</div>)}
            {days.map((d) => {
              const iso = isoLocal(d)
              const inMonth = view === 'week' || d.getMonth() === focus.getMonth()
              const items = byDate.get(iso) ?? []
              return (
                <button key={iso} type="button" className={`cal-cell${inMonth ? '' : ' out'}${iso === todayIso ? ' today' : ''}`} onClick={() => openNew(iso)}>
                  <span className="cal-day">{d.getDate()}</span>
                  <span className="cal-items">
                    {items.slice(0, view === 'week' ? 8 : 4).map((it) => (
                      <span key={`${it.source}-${it.id}`} className={`cal-pill${it.isDone ? ' done' : ''}`} style={{ ['--mc' as string]: sourceHue[it.source] }} title={it.title}>
                        {it.time && <b>{it.time}</b>} {it.title}
                      </span>
                    ))}
                    {items.length > (view === 'week' ? 8 : 4) && <span className="cal-more">+{items.length - (view === 'week' ? 8 : 4)}</span>}
                  </span>
                </button>
              )
            })}
          </div>
          </div>
        </div>
      )}

      {view === 'day' && (
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="card-b flush">
            {dayItems(focus).length === 0 && (
              <button type="button" className="empty" style={{ width: '100%', border: 0, background: 'none', cursor: 'pointer' }} onClick={() => openNew(isoLocal(focus))}>
                <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-calendar)' }}><CalendarClock size={20} /></span>
                <h4>{t('calendar.dayEmpty')}</h4>
                <p>{t('calendar.dayEmptySub')}</p>
              </button>
            )}
            {dayItems(focus).map((it) => (
              <div className="row-item" key={`${it.source}-${it.id}`}>
                <span className="notif-ico" style={{ ['--mc' as string]: sourceHue[it.source] }}><i className="mdot" style={{ ['--mc' as string]: sourceHue[it.source] }} /></span>
                <div className="body">
                  <div className={`ttl${it.isDone ? ' done' : ''}`}>{it.title}</div>
                  <div className="meta">{it.time ? <span className="chip">{it.time}</span> : null}<span className="chip" style={{ ['--mc' as string]: sourceHue[it.source] }} data-m>{t(`calendar.legend.${it.source}`)}</span></div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="card">
        <div className="card-h"><div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-calendar)' }} /><h3>{t('calendar.upcoming')}</h3></div></div>
        <div className="card-b flush">
          {(events?.length ?? 0) === 0 && (
            <div className="empty">
              <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-calendar)' }}><CalendarClock size={20} /></span>
              <h4>{t('calendar.emptyTitle')}</h4>
              <p>{t('calendar.emptySub')}</p>
            </div>
          )}
          {(events ?? []).map((ev) => (
            <div className="row-item" key={ev.id}>
              <div className="body">
                <div className="ttl">{ev.title}</div>
                <div className="meta">
                  <span className="chip due-chip">{new Date(`${ev.startsOn}T00:00:00`).toLocaleDateString(locale, { day: 'numeric', month: 'short' })}{ev.startTime ? ` · ${ev.startTime}` : ''}</span>
                  {ev.location && <span className="chip">{ev.location}</span>}
                </div>
              </div>
              <div className="end">
                {ev.canEdit && (
                  <button className="btn ghost icon sm" type="button" onClick={() => openEdit(ev)} aria-label={t('common.edit')}><Pencil size={14} /></button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>

      {modalOpen && <EventModal event={editing} defaultDate={defaultDate} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
