import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BellRing, Check, Pencil, Plus, Repeat, Trash2 } from 'lucide-react'
import { Avatar } from '@/shared/components/Avatar'
import { confirm } from '@/platform/ui/confirmStore'
import type { Reminder } from './api'
import { useDeleteReminder, useReminders, useToggleReminder } from './hooks'
import { ReminderModal } from './ReminderModal'

type Bucket = 'overdue' | 'today' | 'upcoming' | 'done'
const ORDER: Bucket[] = ['overdue', 'today', 'upcoming', 'done']

function todayIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function ReminderRow({ reminder, onEdit }: { reminder: Reminder; onEdit: (r: Reminder) => void }) {
  const { t, i18n } = useTranslation()
  const toggle = useToggleReminder()
  const del = useDeleteReminder()
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'

  const onToggle = async () => {
    if (!reminder.isDone) {
      const ok = await confirm({ title: t('reminders.confirmDone.title'), message: t('reminders.confirmDone.message', { title: reminder.title }), confirmLabel: t('common.confirm') })
      if (!ok) return
    }
    toggle.mutate(reminder.id)
  }

  const onDelete = async () => {
    if (await confirm({ title: t('reminders.confirmDelete.title'), message: t('reminders.confirmDelete.message', { title: reminder.title }), confirmLabel: t('common.delete'), danger: true })) del.mutate(reminder.id)
  }

  const dateLabel = new Date(`${reminder.remindOn}T00:00:00`).toLocaleDateString(locale, { day: 'numeric', month: 'short' })

  return (
    <div className={`row-item task-row${reminder.isDone ? ' done' : ''}`}>
      <label className="cb">
        <input type="checkbox" checked={reminder.isDone} onChange={() => void onToggle()} aria-label={t('reminders.markDone')} />
        <span className="box"><Check size={12} /></span>
      </label>
      <div className="body">
        <div className="ttl">{reminder.title}</div>
        <div className="meta">
          <span className={`chip due-chip${reminder.isOverdue ? ' danger' : ''}`}>{dateLabel}{reminder.remindAt ? ` · ${reminder.remindAt}` : ''}</span>
          {reminder.recurrence !== 'None' && <span className="chip repeat"><Repeat size={11} />{t(`recurrence.${reminder.recurrence}`)}</span>}
          {reminder.notes && <span className="chip">{reminder.notes}</span>}
        </div>
      </div>
      <div className="end">
        {reminder.forMemberName && <Avatar name={reminder.forMemberName} memberId={reminder.forMemberId} size="xs" color="var(--m-reminders)" />}
        <div className="acts">
          {reminder.canEdit && (
            <button className="btn ghost icon sm" type="button" onClick={() => onEdit(reminder)} aria-label={t('common.edit')}><Pencil size={14} /></button>
          )}
          {reminder.canEdit && (
            <button className="btn ghost icon sm danger" type="button" onClick={() => void onDelete()} aria-label={t('common.delete')}><Trash2 size={14} /></button>
          )}
        </div>
      </div>
    </div>
  )
}

export function RemindersPage() {
  const { t } = useTranslation()
  const { data: reminders, isLoading, isError, refetch } = useReminders()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<Reminder | undefined>(undefined)

  const groups = useMemo(() => {
    const today = todayIso()
    const map: Record<Bucket, Reminder[]> = { overdue: [], today: [], upcoming: [], done: [] }
    for (const r of reminders ?? []) {
      if (r.isDone) map.done.push(r)
      else if (r.remindOn < today) map.overdue.push(r)
      else if (r.remindOn === today) map.today.push(r)
      else map.upcoming.push(r)
    }
    return map
  }, [reminders])

  const openNew = () => { setEditing(undefined); setModalOpen(true) }
  const openEdit = (r: Reminder) => { setEditing(r); setModalOpen(true) }
  const hasAny = ORDER.some((b) => groups[b].length > 0)

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-reminders)' }}>{t('nav.reminders')}</div>
          <h1>{t('reminders.title')}</h1>
          <p className="sub">{t('reminders.sub')}</p>
        </div>
        <div className="actions">
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('reminders.newReminder')}</button>
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}
      {isError && (
        <div className="card"><div className="card-b empty"><p>{t('common.error')}</p>
          <button className="btn" type="button" onClick={() => void refetch()}>{t('common.retry')}</button></div></div>
      )}

      {!isLoading && !isError && !hasAny && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-reminders)' }}><BellRing size={20} /></span>
          <h4>{t('reminders.emptyTitle')}</h4>
          <p>{t('reminders.emptySub')}</p>
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('reminders.newReminder')}</button>
        </div></div>
      )}

      {!isLoading && !isError && ORDER.map((bucket) =>
        groups[bucket].length > 0 ? (
          <div className="card" key={bucket} style={{ marginBottom: 14 }}>
            <div className="card-h">
              <div className="t"><h3>{t(`reminders.bucket.${bucket}`)}</h3></div>
              <span className={`chip section-count${bucket === 'overdue' ? ' danger' : ''}`}>{groups[bucket].length}</span>
            </div>
            <div className="card-b flush scroll-list">
              {groups[bucket].map((r) => <ReminderRow key={r.id} reminder={r} onEdit={openEdit} />)}
            </div>
          </div>
        ) : null,
      )}

      {modalOpen && <ReminderModal reminder={editing} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
