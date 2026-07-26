import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, ChevronDown, ChevronUp, Eye, EyeOff, GripVertical, SlidersHorizontal } from 'lucide-react'
import { dashboardWidgets } from '@/platform/surfaces/dashboardWidgets'

const ORDER_KEY = 'homeos.dashboardOrder'
const HIDDEN_KEY = 'homeos.dashboardHidden'

function load(key: string): string[] {
  try { return JSON.parse(localStorage.getItem(key) ?? '[]') as string[] } catch { return [] }
}

/**
 * The dashboard widget column. Normally the widgets render clean, in the member's saved order. An "Arrange"
 * toggle reveals a per-widget control strip (drag / up / down / show-hide) shown *above* each card, so it
 * never overlaps the card's own header — and hidden or empty widgets stay reorderable while editing.
 * Order + hidden set persist in localStorage.
 */
export function DashboardWidgets({ enabledAppIds }: { enabledAppIds: Set<string> }) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState(false)
  const [order, setOrder] = useState<string[]>(() => load(ORDER_KEY))
  const [hidden, setHidden] = useState<string[]>(() => load(HIDDEN_KEY))
  const [dragId, setDragId] = useState<string | null>(null)

  const available = dashboardWidgets.filter((w) => !w.appId || enabledAppIds.has(w.appId))
  const ordered = [
    ...order.map((id) => available.find((w) => w.id === id)).filter((w): w is typeof available[number] => !!w),
    ...available.filter((w) => !order.includes(w.id)),
  ]

  const persistOrder = (ids: string[]) => { setOrder(ids); localStorage.setItem(ORDER_KEY, JSON.stringify(ids)) }
  const toggleHidden = (id: string) => {
    const next = hidden.includes(id) ? hidden.filter((x) => x !== id) : [...hidden, id]
    setHidden(next); localStorage.setItem(HIDDEN_KEY, JSON.stringify(next))
  }

  const reorder = (ids: string[], from: number, to: number) => {
    const [moved] = ids.splice(from, 1)
    if (moved === undefined) return
    ids.splice(to, 0, moved)
  }
  const move = (id: string, delta: number) => {
    const ids = ordered.map((w) => w.id)
    const from = ids.indexOf(id); const to = from + delta
    if (from < 0 || to < 0 || to >= ids.length) return
    reorder(ids, from, to); persistOrder(ids)
  }
  const onDrop = (targetId: string) => {
    if (!dragId || dragId === targetId) return
    const ids = ordered.map((w) => w.id)
    const from = ids.indexOf(dragId); const to = ids.indexOf(targetId)
    if (from < 0 || to < 0) return
    reorder(ids, from, to); persistOrder(ids); setDragId(null)
  }

  return (
    <>
      <div className="widget-bar">
        <button type="button" className={`btn sm ${editing ? 'primary' : 'ghost'}`} onClick={() => setEditing((v) => !v)}>
          {editing ? <><Check size={14} />{t('common.done')}</> : <><SlidersHorizontal size={14} />{t('dashboard.arrange')}</>}
        </button>
      </div>

      {editing
        ? ordered.map((w, i) => {
          const isHidden = hidden.includes(w.id)
          return (
            <div key={w.id} className={`widget-edit${dragId === w.id ? ' dragging' : ''}`}
              onDragOver={(e) => { if (dragId) e.preventDefault() }} onDrop={() => onDrop(w.id)}>
              <div className="widget-edit-h">
                <span className="grip" title={t('dashboard.drag')} draggable
                  onDragStart={() => setDragId(w.id)} onDragEnd={() => setDragId(null)}><GripVertical size={15} /></span>
                <span className="nm">{t(w.nameKey)}</span>
                <button type="button" className="wbtn" title={t('dashboard.moveUp')} disabled={i === 0} onClick={() => move(w.id, -1)}><ChevronUp size={15} /></button>
                <button type="button" className="wbtn" title={t('dashboard.moveDown')} disabled={i === ordered.length - 1} onClick={() => move(w.id, 1)}><ChevronDown size={15} /></button>
                <button type="button" className="wbtn" title={isHidden ? t('dashboard.show') : t('dashboard.hide')} onClick={() => toggleHidden(w.id)}>
                  {isHidden ? <EyeOff size={15} /> : <Eye size={15} />}
                </button>
              </div>
              <div className={`widget-preview${isHidden ? ' off' : ''}`}><w.Component /></div>
            </div>
          )
        })
        : ordered.filter((w) => !hidden.includes(w.id)).map((w) => <w.Component key={w.id} />)}
    </>
  )
}
