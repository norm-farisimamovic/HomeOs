import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Link2, Plus, X } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { searchApi } from '@/platform/search/api'
import { useCreateLink, useDeleteLink, useLinks } from '@/platform/links/api'

/**
 * The cross-app "connected web" surface: shows and edits the links from one object (a note, a task…) to any
 * other app object, chosen via global search. Drop it into any app's detail view with the object's type + id.
 */
export function LinkedItems({ fromType, fromId }: { fromType: string; fromId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data: links = [] } = useLinks(fromType, fromId)
  const create = useCreateLink(fromType, fromId)
  const del = useDeleteLink(fromType, fromId)

  const [adding, setAdding] = useState(false)
  const [q, setQ] = useState('')
  const { data: hits = [] } = useQuery({
    queryKey: ['search', q],
    queryFn: () => searchApi.query(q),
    enabled: adding && q.trim().length >= 2,
  })

  const add = (hit: { source: string; id: string; title: string; link: string }) => {
    if (hit.source === fromType && hit.id === fromId) return
    create.mutate(
      { fromType, fromId, toType: hit.source, toId: hit.id, toTitle: hit.title, toLink: hit.link },
      { onSuccess: () => { setQ(''); setAdding(false) } },
    )
  }

  return (
    <div className="linked">
      <div className="linked-h"><Link2 size={13} /><span>{t('links.title')}</span></div>
      <div className="linked-list">
        {links.map((l) => (
          <span className="link-chip" key={l.id}>
            <button type="button" onClick={() => navigate(l.toLink)} title={l.toTitle}>{l.toTitle || l.toType}</button>
            <button type="button" className="link-x" onClick={() => del.mutate(l.id)} aria-label={t('common.delete')}><X size={11} /></button>
          </span>
        ))}
        {!adding && (
          <button type="button" className="link-chip add" onClick={() => setAdding(true)}><Plus size={12} />{t('links.add')}</button>
        )}
      </div>

      {adding && (
        <div className="link-picker">
          <input className="inp sm" autoFocus value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('links.searchPh')}
            onKeyDown={(e) => { if (e.key === 'Escape') { setAdding(false); setQ('') } }} />
          {q.trim().length >= 2 && (
            <div className="link-results">
              {hits.length === 0 && <div className="link-none">{t('common.noResults')}</div>}
              {hits.slice(0, 6).map((h) => (
                <button type="button" key={`${h.source}-${h.id}`} onClick={() => add(h)}>
                  <span className="src" data-m style={{ ['--mc' as string]: `var(--m-${h.source})` }}>{h.source}</span>
                  <span className="ttl">{h.title}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
