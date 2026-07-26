import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { FolderClock, Pencil, Plus, Trash2 } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import type { LifeRecord } from './api'
import { useDeleteLifeRecord, useLifeRecords } from './hooks'
import { LifeRecordModal } from './LifeRecordModal'

function RecordRow({ record, onEdit }: { record: LifeRecord; onEdit: (r: LifeRecord) => void }) {
  const { t, i18n } = useTranslation()
  const del = useDeleteLifeRecord()
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'

  const onDelete = async () => {
    if (await confirm({ title: t('life.confirmDelete.title'), message: t('life.confirmDelete.message', { title: record.title }), confirmLabel: t('common.delete'), danger: true })) del.mutate(record.id)
  }

  const d = record.daysToExpiry
  const expiryChip = record.expiresOn && (
    <span className={`chip due-chip${d !== null && d <= 7 ? ' danger' : d !== null && d <= 30 ? ' warn' : ''}`}>
      {new Date(`${record.expiresOn}T00:00:00`).toLocaleDateString(locale, { day: 'numeric', month: 'short', year: 'numeric' })}
      {d !== null && d >= 0 ? ` · ${t('life.inDays', { count: d })}` : d !== null ? ` · ${t('life.expired')}` : ''}
    </span>
  )

  return (
    <div className="row-item task-row">
      <div className="body">
        <div className="ttl">{record.title} <span className="chip">{t(`life.category.${record.category.toLowerCase()}`)}</span></div>
        <div className="meta">
          {expiryChip}
          {record.provider && <span className="chip">{record.provider}</span>}
          {record.notes && <span className="chip">{record.notes}</span>}
        </div>
      </div>
      {record.canEdit && (
        <div className="end"><div className="acts">
          <button className="btn ghost icon sm" type="button" onClick={() => onEdit(record)} aria-label={t('common.edit')}><Pencil size={14} /></button>
          <button className="btn ghost icon sm danger" type="button" onClick={() => void onDelete()} aria-label={t('common.delete')}><Trash2 size={14} /></button>
        </div></div>
      )}
    </div>
  )
}

export function LifeAdminPage() {
  const { t } = useTranslation()
  const { data: records, isLoading, isError, refetch } = useLifeRecords()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<LifeRecord | undefined>(undefined)

  const { soon, rest } = useMemo(() => {
    const soon: LifeRecord[] = []
    const rest: LifeRecord[] = []
    for (const r of records ?? []) {
      if (r.daysToExpiry !== null && r.daysToExpiry <= 30) soon.push(r)
      else rest.push(r)
    }
    return { soon, rest }
  }, [records])

  const openNew = () => { setEditing(undefined); setModalOpen(true) }
  const openEdit = (r: LifeRecord) => { setEditing(r); setModalOpen(true) }
  const hasAny = (records?.length ?? 0) > 0

  const section = (titleKey: string, list: LifeRecord[], danger = false) =>
    list.length > 0 ? (
      <div className="card" style={{ marginBottom: 14 }}>
        <div className="card-h">
          <div className="t"><h3>{t(titleKey)}</h3></div>
          <span className={`chip section-count${danger ? ' danger' : ''}`}>{list.length}</span>
        </div>
        <div className="card-b flush scroll-list">{list.map((r) => <RecordRow key={r.id} record={r} onEdit={openEdit} />)}</div>
      </div>
    ) : null

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-life)' }}>{t('nav.life')}</div>
          <h1>{t('life.title')}</h1>
          <p className="sub">{t('life.sub')}</p>
        </div>
        <div className="actions">
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('life.newRecord')}</button>
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}
      {isError && (
        <div className="card"><div className="card-b empty"><p>{t('common.error')}</p>
          <button className="btn" type="button" onClick={() => void refetch()}>{t('common.retry')}</button></div></div>
      )}

      {!isLoading && !isError && !hasAny && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-life)' }}><FolderClock size={20} /></span>
          <h4>{t('life.emptyTitle')}</h4>
          <p>{t('life.emptySub')}</p>
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('life.newRecord')}</button>
        </div></div>
      )}

      {!isLoading && !isError && hasAny && (
        <>
          {section('life.expiringSoon', soon, true)}
          {section('life.allRecords', rest)}
        </>
      )}

      {modalOpen && <LifeRecordModal record={editing} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
