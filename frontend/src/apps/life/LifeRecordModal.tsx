import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { FolderClock, Trash2 } from 'lucide-react'
import { Modal } from '@/shared/components/Modal'
import { Attachments } from '@/shared/components/Attachments'
import { Req } from '@/shared/components/Req'
import { ApiError } from '@/platform/api/client'
import { confirm } from '@/platform/ui/confirmStore'
import { LIFE_CATEGORIES, type LifeRecord } from './api'
import { useCreateLifeRecord, useDeleteLifeRecord, useUpdateLifeRecord } from './hooks'

/** Create or edit a life-admin record. Setting an expiry date auto-schedules a reminder (server-side). */
export function LifeRecordModal({ record, onClose }: { record?: LifeRecord; onClose: () => void }) {
  const { t } = useTranslation()
  const create = useCreateLifeRecord()
  const update = useUpdateLifeRecord()
  const del = useDeleteLifeRecord()
  const editing = record !== undefined

  const [title, setTitle] = useState(record?.title ?? '')
  const [category, setCategory] = useState<string>(record?.category ?? 'Document')
  const [expiresOn, setExpiresOn] = useState(record?.expiresOn ?? '')
  const [provider, setProvider] = useState(record?.provider ?? '')
  const [notes, setNotes] = useState(record?.notes ?? '')
  const [visibility, setVisibility] = useState<string>(record?.visibility ?? 'Household')
  const [error, setError] = useState<string | null>(null)

  const busy = create.isPending || update.isPending

  const submit = async () => {
    if (!title.trim()) { setError(t('life.required')); return }
    const input = {
      title: title.trim(), category, expiresOn: expiresOn || null,
      provider: provider.trim() || undefined, notes: notes.trim() || undefined, visibility,
    }
    try {
      if (editing) await update.mutateAsync({ id: record.id, input })
      else await create.mutateAsync(input)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.error'))
    }
  }

  const onDelete = async () => {
    if (await confirm({ title: t('life.confirmDelete.title'), message: t('life.confirmDelete.message', { title: record?.title }), confirmLabel: t('common.delete'), danger: true })) {
      del.mutate(record!.id)
      onClose()
    }
  }

  return (
    <Modal
      icon={FolderClock} hue="var(--m-life)"
      title={editing ? t('life.editRecord') : t('life.newRecord')}
      subtitle={t('life.modalSub')}
      onClose={onClose}
      footer={
        <>
          {editing && record.canEdit && (
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
        <label>{t('life.fTitle')} <Req /></label>
        <input className="inp" value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('life.fTitlePh')} autoFocus />
      </div>
      <div className="form-grid">
        <div className="field">
          <label>{t('life.fCategory')}</label>
          <select className="sel" value={category} onChange={(e) => setCategory(e.target.value)}>
            {LIFE_CATEGORIES.map((c) => <option key={c} value={c}>{t(`life.category.${c.toLowerCase()}`)}</option>)}
          </select>
        </div>
        <div className="field">
          <label>{t('life.fExpires')}</label>
          <input className="inp" type="date" value={expiresOn} onChange={(e) => setExpiresOn(e.target.value)} />
        </div>
      </div>
      <p className="hint" style={{ marginTop: -4 }}>{t('life.expiryHint')}</p>
      <div className="field">
        <label>{t('life.fProvider')}</label>
        <input className="inp" value={provider} onChange={(e) => setProvider(e.target.value)} placeholder={t('life.fProviderPh')} />
      </div>
      <div className="field"><label>{t('life.fNotes')}</label><textarea className="ta" value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
      <div className="field">
        <label>{t('tasks.fVisibility')}</label>
        <select className="sel" value={visibility} onChange={(e) => setVisibility(e.target.value)}>
          <option value="Private">{t('tasks.visibility.private')}</option>
          <option value="Household">{t('tasks.visibility.household')}</option>
        </select>
      </div>

      {editing && <Attachments ownerType="life" ownerId={record.id} />}
    </Modal>
  )
}
