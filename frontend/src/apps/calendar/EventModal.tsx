import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CalendarDays, Trash2 } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { Avatar } from '@/shared/components/Avatar'
import { ApiError } from '@/platform/api/client'
import { useMembers } from '@/platform/members/useMembers'
import { useMe } from '@/platform/auth/useAuth'
import { confirm } from '@/platform/ui/confirmStore'
import type { CalendarEvent } from './api'
import { useCreateEvent, useDeleteEvent, useUpdateEvent } from './hooks'

/** Create or edit a calendar event. `defaultDate` pre-fills the day when adding from a grid cell. */
export function EventModal({ event, defaultDate, onClose }: { event?: CalendarEvent; defaultDate?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const { data: members = [] } = useMembers()
  const create = useCreateEvent()
  const update = useUpdateEvent()
  const del = useDeleteEvent()
  const editing = event !== undefined

  const [title, setTitle] = useState(event?.title ?? '')
  const [startsOn, setStartsOn] = useState(event?.startsOn ?? defaultDate ?? '')
  const [startTime, setStartTime] = useState(event?.startTime ?? '')
  const [location, setLocation] = useState(event?.location ?? '')
  const [notes, setNotes] = useState(event?.notes ?? '')
  const [visibility, setVisibility] = useState<string>(event?.visibility ?? 'Household')
  const [sharedWith, setSharedWith] = useState<string[]>(event?.sharedWith ?? [])
  const [error, setError] = useState<string | null>(null)

  const busy = create.isPending || update.isPending
  const toggleShare = (id: string) =>
    setSharedWith((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]))

  const submit = async () => {
    if (!title.trim() || !startsOn) { setError(t('calendar.required')); return }
    const input = {
      title: title.trim(), startsOn, startTime: startTime || null,
      location: location.trim() || undefined, notes: notes.trim() || undefined, visibility,
      sharedWith: visibility === 'Shared' ? sharedWith : [],
    }
    try {
      if (editing) await update.mutateAsync({ id: event.id, input })
      else await create.mutateAsync(input)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  const onDelete = async () => {
    if (await confirm({ title: t('calendar.confirmDelete.title'), message: t('calendar.confirmDelete.message', { title: event?.title }), confirmLabel: t('common.delete'), danger: true })) {
      del.mutate(event!.id)
      onClose()
    }
  }

  return (
    <Modal
      icon={CalendarDays} hue="var(--m-calendar)"
      title={editing ? t('calendar.editEvent') : t('calendar.newEvent')}
      subtitle={t('calendar.modalSub')}
      onClose={onClose}
      footer={
        <>
          {editing && event.canEdit && (
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
        <label>{t('calendar.fTitle')} <Req /></label>
        <input className="inp" value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('calendar.fTitlePh')} autoFocus />
      </div>
      <div className="form-grid">
        <div className="field"><label>{t('calendar.fDate')} <Req /></label><input className="inp" type="date" value={startsOn} onChange={(e) => setStartsOn(e.target.value)} /></div>
        <div className="field"><label>{t('calendar.fTime')}</label><input className="inp" type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} /></div>
      </div>
      <div className="field"><label>{t('calendar.fLocation')}</label><input className="inp" value={location} onChange={(e) => setLocation(e.target.value)} placeholder={t('calendar.fLocationPh')} /></div>
      <div className="field"><label>{t('calendar.fNotes')}</label><textarea className="ta" value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
      <div className="field">
        <label>{t('tasks.fVisibility')}</label>
        <select className="sel" value={visibility} onChange={(e) => setVisibility(e.target.value)}>
          <option value="Private">{t('tasks.visibility.private')}</option>
          <option value="Household">{t('tasks.visibility.household')}</option>
          <option value="Shared">{t('tasks.visibility.shared')}</option>
        </select>
      </div>

      {visibility === 'Shared' && (
        <div className="field">
          <label>{t('calendar.shareWith')} <Req /></label>
          <div className="share-list">
            {members.filter((m) => m.id !== me?.id).map((m) => (
              <label key={m.id} className={`share-chip${sharedWith.includes(m.id) ? ' on' : ''}`}>
                <input type="checkbox" checked={sharedWith.includes(m.id)} onChange={() => toggleShare(m.id)} />
                <Avatar name={m.displayName} size="xs" />
                <span>{m.displayName}</span>
              </label>
            ))}
            {members.filter((m) => m.id !== me?.id).length === 0 && <span className="hint">{t('calendar.noOneToShare')}</span>}
          </div>
        </div>
      )}
    </Modal>
  )
}
