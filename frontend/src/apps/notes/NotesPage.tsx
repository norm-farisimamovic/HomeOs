import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NotebookPen, Pencil, Pin, PinOff, Plus, Trash2 } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import type { Note } from './api'
import { useDeleteNote, useNotes, usePinNote } from './hooks'
import { NoteModal } from './NoteModal'

function NoteCard({ note, onEdit, locale }: { note: Note; onEdit: (n: Note) => void; locale: string }) {
  const { t } = useTranslation()
  const pin = usePinNote()
  const del = useDeleteNote()

  const onDelete = async () => {
    if (await confirm({ title: t('notes.confirmDelete.title'), message: t('notes.confirmDelete.message', { title: note.title }), confirmLabel: t('common.delete'), danger: true })) del.mutate(note.id)
  }

  return (
    <div className={`note-card${note.pinned ? ' pinned' : ''}`}>
      <div className="note-top">
        <h4>{note.entryDate ? new Date(`${note.entryDate}T00:00:00`).toLocaleDateString(locale, { weekday: 'short', day: 'numeric', month: 'short' }) : note.title}</h4>
        {note.canEdit && (
          <button className="btn ghost icon sm" type="button" title={note.pinned ? t('notes.unpin') : t('notes.pin')}
            onClick={() => pin.mutate({ id: note.id, pinned: !note.pinned })}>
            {note.pinned ? <Pin size={14} /> : <PinOff size={14} />}
          </button>
        )}
      </div>
      {note.content && <p className="note-body">{note.content}</p>}
      <div className="note-foot">
        <div className="note-tags">
          {note.visibility === 'Private' && <span className="chip">{t('tasks.visibility.private')}</span>}
          {note.visibility === 'Shared' && <span className="chip">{t('tasks.visibility.shared')}</span>}
          {note.tags.map((tag) => <span key={tag} className="chip">#{tag}</span>)}
        </div>
        {note.canEdit && (
          <div className="note-acts">
            <button className="btn ghost icon sm" type="button" onClick={() => onEdit(note)} aria-label={t('common.edit')}><Pencil size={14} /></button>
            <button className="btn ghost icon sm danger" type="button" onClick={() => void onDelete()} aria-label={t('common.delete')}><Trash2 size={14} /></button>
          </div>
        )}
      </div>
    </div>
  )
}

type Mode = 'all' | 'notes' | 'journal'

export function NotesPage() {
  const { t, i18n } = useTranslation()
  const { data: notes, isLoading, isError, refetch } = useNotes()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<Note | undefined>(undefined)
  const [journalDate, setJournalDate] = useState<string | undefined>(undefined)
  const [mode, setMode] = useState<Mode>('all')

  const openNew = () => { setEditing(undefined); setJournalDate(undefined); setModalOpen(true) }
  const openEdit = (n: Note) => { setEditing(n); setJournalDate(undefined); setModalOpen(true) }
  const openEntry = () => {
    const iso = new Date().toISOString().slice(0, 10)
    setEditing(undefined); setJournalDate(iso); setModalOpen(true)
  }

  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'
  const shown = (notes ?? [])
    .filter((n) => (mode === 'notes' ? !n.entryDate : mode === 'journal' ? n.entryDate : true))
    .slice()
    .sort((a, b) => (mode === 'journal' ? (b.entryDate ?? '').localeCompare(a.entryDate ?? '') : 0))

  return (
    <div className="wrap wide">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-notes)' }}>{t('nav.notes')}</div>
          <h1>{t('notes.title')}</h1>
          <p className="sub">{t('notes.sub')}</p>
        </div>
        <div className="actions">
          <button className="btn ghost" type="button" onClick={openEntry}><Plus size={15} />{t('notes.newEntry')}</button>
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('notes.newNote')}</button>
        </div>
      </div>

      <div className="toolbar" style={{ marginBottom: 14 }}>
        <div className="seg">
          {(['all', 'notes', 'journal'] as Mode[]).map((m) => (
            <button key={m} type="button" className={mode === m ? 'on' : undefined} onClick={() => setMode(m)}>{t(`notes.mode.${m}`)}</button>
          ))}
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}
      {isError && (
        <div className="card"><div className="card-b empty"><p>{t('common.error')}</p>
          <button className="btn" type="button" onClick={() => void refetch()}>{t('common.retry')}</button></div></div>
      )}

      {!isLoading && !isError && (notes?.length ?? 0) === 0 && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-notes)' }}><NotebookPen size={20} /></span>
          <h4>{t('notes.emptyTitle')}</h4>
          <p>{t('notes.emptySub')}</p>
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('notes.newNote')}</button>
        </div></div>
      )}

      {!isLoading && !isError && shown.length > 0 && (
        <div className="notes-grid">
          {shown.map((n) => <NoteCard key={n.id} note={n} onEdit={openEdit} locale={locale} />)}
        </div>
      )}

      {modalOpen && <NoteModal note={editing} journalDate={journalDate} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
