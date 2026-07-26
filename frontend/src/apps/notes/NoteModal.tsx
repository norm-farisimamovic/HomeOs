import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NotebookPen, Trash2 } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Req } from '@/shared/components/Req'
import { Avatar } from '@/shared/components/Avatar'
import { useMembers } from '@/platform/members/useMembers'
import { useMe } from '@/platform/auth/useAuth'
import { ApiError } from '@/platform/api/client'
import { confirm } from '@/platform/ui/confirmStore'
import { LinkedItems } from '@/shared/components/LinkedItems'
import type { Note } from './api'
import { useCreateNote, useDeleteNote, useUpdateNote } from './hooks'

/** Create or edit a note (or a dated journal entry when `journalDate` is set). */
export function NoteModal({ note, journalDate, onClose }: { note?: Note; journalDate?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const { data: members = [] } = useMembers()
  const create = useCreateNote()
  const update = useUpdateNote()
  const del = useDeleteNote()
  const editing = note !== undefined
  const entryDate = note?.entryDate ?? journalDate ?? undefined

  const [title, setTitle] = useState(note?.title ?? '')
  const [content, setContent] = useState(note?.content ?? '')
  const [tags, setTags] = useState(note?.tags.join(', ') ?? '')
  const [visibility, setVisibility] = useState<string>(note?.visibility ?? 'Household')
  const [sharedWith, setSharedWith] = useState<string[]>(note?.sharedWith ?? [])
  const [error, setError] = useState<string | null>(null)

  const busy = create.isPending || update.isPending
  const toggleShare = (id: string) =>
    setSharedWith((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]))

  const submit = async () => {
    if (!title.trim()) { setError(t('notes.required')); return }
    const input = {
      title: title.trim(),
      content: content.trim() || undefined,
      tags: tags.split(',').map((s) => s.trim()).filter(Boolean),
      visibility,
      sharedWith: visibility === 'Shared' ? sharedWith : [],
      entryDate: entryDate ?? null,
    }
    try {
      if (editing) await update.mutateAsync({ id: note.id, input })
      else await create.mutateAsync(input)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  const onDelete = async () => {
    if (await confirm({ title: t('notes.confirmDelete.title'), message: t('notes.confirmDelete.message', { title: note?.title }), confirmLabel: t('common.delete'), danger: true })) {
      del.mutate(note!.id)
      onClose()
    }
  }

  return (
    <Modal
      icon={NotebookPen} hue="var(--m-notes)"
      title={editing ? t('notes.editNote') : entryDate ? t('notes.newEntry') : t('notes.newNote')}
      subtitle={t('notes.modalSub')}
      onClose={onClose}
      footer={
        <>
          {editing && note.canEdit && (
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
        <label>{t('notes.fTitle')} <Req /></label>
        <input className="inp" value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('notes.fTitlePh')} autoFocus />
      </div>
      <div className="field">
        <label>{t('notes.fContent')}</label>
        <textarea className="ta" style={{ minHeight: 140 }} value={content} onChange={(e) => setContent(e.target.value)} placeholder={t('notes.fContentPh')} />
      </div>
      <div className="field">
        <label>{t('notes.fTags')}</label>
        <input className="inp" value={tags} onChange={(e) => setTags(e.target.value)} placeholder={t('notes.fTagsPh')} />
      </div>
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
      {editing && <LinkedItems fromType="note" fromId={note.id} />}
    </Modal>
  )
}
