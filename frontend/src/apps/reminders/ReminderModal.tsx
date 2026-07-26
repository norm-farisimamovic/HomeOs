import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BellRing, Trash2 } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { useMembers } from '@/platform/members/useMembers'
import { ApiError } from '@/platform/api/client'
import { confirm } from '@/platform/ui/confirmStore'
import type { Reminder } from './api'
import { useCreateReminder, useDeleteReminder, useUpdateReminder } from './hooks'

/** Create or edit a reminder. */
export function ReminderModal({ reminder, onClose }: { reminder?: Reminder; onClose: () => void }) {
  const { t } = useTranslation()
  const { data: members = [] } = useMembers()
  const create = useCreateReminder()
  const update = useUpdateReminder()
  const del = useDeleteReminder()
  const editing = reminder !== undefined

  const [title, setTitle] = useState(reminder?.title ?? '')
  const [remindOn, setRemindOn] = useState(reminder?.remindOn ?? '')
  const [remindAt, setRemindAt] = useState(reminder?.remindAt ?? '')
  const [forMemberId, setForMemberId] = useState(reminder?.forMemberId ?? '')
  const [notes, setNotes] = useState(reminder?.notes ?? '')
  const [visibility, setVisibility] = useState<string>(reminder?.visibility ?? 'Private')
  const [recurrence, setRecurrence] = useState<string>(reminder?.recurrence ?? 'None')
  const [error, setError] = useState<string | null>(null)

  const busy = create.isPending || update.isPending

  const submit = async () => {
    if (!title.trim() || !remindOn) { setError(t('reminders.required')); return }
    const input = {
      title: title.trim(), remindOn, remindAt: remindAt || null,
      notes: notes.trim() || undefined, forMemberId: forMemberId || null, visibility, recurrence,
    }
    try {
      if (editing) await update.mutateAsync({ id: reminder.id, input })
      else await create.mutateAsync(input)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  const onDelete = async () => {
    if (await confirm({ title: t('reminders.confirmDelete.title'), message: t('reminders.confirmDelete.message', { title: reminder?.title }), confirmLabel: t('common.delete'), danger: true })) {
      del.mutate(reminder!.id)
      onClose()
    }
  }

  return (
    <Modal
      icon={BellRing} hue="var(--m-reminders)"
      title={editing ? t('reminders.editReminder') : t('reminders.newReminder')}
      subtitle={t('reminders.modalSub')}
      onClose={onClose}
      footer={
        <>
          {editing && reminder.canEdit && (
            <button className="btn danger" type="button" onClick={() => void onDelete()}><Trash2 size={14} />{t('common.delete')}</button>
          )}
          <div className="spacer" />
          <button className="btn" type="button" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary" type="button" onClick={() => void submit()} disabled={busy}>{t('common.save')}</button>
        </>
      }
    >
      {error && <div className="err-msg" role="alert"><span>{error}</span></div>}
      <div className="field">
        <label>{t('reminders.fTitle')} <Req /></label>
        <input className="inp" value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('reminders.fTitlePh')} autoFocus />
      </div>
      <div className="form-grid">
        <div className="field"><label>{t('reminders.fDate')} <Req /></label><input className="inp" type="date" value={remindOn} onChange={(e) => setRemindOn(e.target.value)} /></div>
        <div className="field"><label>{t('reminders.fTime')}</label><input className="inp" type="time" value={remindAt} onChange={(e) => setRemindAt(e.target.value)} /></div>
      </div>
      <div className="field">
        <label>{t('reminders.fFor')}</label>
        <select className="sel" value={forMemberId} onChange={(e) => setForMemberId(e.target.value)}>
          <option value="">{t('reminders.forMe')}</option>
          {members.map((m) => <option key={m.id} value={m.id}>{m.displayName}</option>)}
        </select>
      </div>
      <div className="field"><label>{t('reminders.fNotes')}</label><textarea className="ta" value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
      <div className="form-grid">
        <div className="field">
          <label>{t('common.repeat')}</label>
          <select className="sel" value={recurrence} onChange={(e) => setRecurrence(e.target.value)}>
            <option value="None">{t('recurrence.None')}</option>
            <option value="Daily">{t('recurrence.Daily')}</option>
            <option value="Weekly">{t('recurrence.Weekly')}</option>
            <option value="Monthly">{t('recurrence.Monthly')}</option>
            <option value="Yearly">{t('recurrence.Yearly')}</option>
          </select>
        </div>
        <div className="field">
          <label>{t('tasks.fVisibility')}</label>
          <select className="sel" value={visibility} onChange={(e) => setVisibility(e.target.value)}>
            <option value="Private">{t('reminders.visPrivate')}</option>
            <option value="Household">{t('tasks.visibility.household')}</option>
          </select>
        </div>
      </div>
    </Modal>
  )
}
