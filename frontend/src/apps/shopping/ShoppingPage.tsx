import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, Plus, ShoppingCart, Trash2 } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import type { ShoppingList } from './api'
import { useShoppingLists, useShoppingMutations } from './hooks'

function ListCard({ list }: { list: ShoppingList }) {
  const { t } = useTranslation()
  const { deleteList, addItem, toggleItem, deleteItem } = useShoppingMutations()
  const [text, setText] = useState('')
  const remaining = list.items.filter((i) => !i.done).length

  const add = () => {
    const v = text.trim()
    if (!v) return
    addItem.mutate({ listId: list.id, text: v }, { onSuccess: () => setText('') })
  }

  const removeList = async () => {
    if (await confirm({ title: t('shopping.deleteListTitle'), message: t('shopping.deleteListMsg', { name: list.name }), confirmLabel: t('common.delete'), danger: true }))
      deleteList.mutate(list.id)
  }

  return (
    <div className="card">
      <div className="card-h">
        <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-life)' }} /><h3>{list.name}</h3></div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span className="chip">{remaining}/{list.items.length}</span>
          <button className="btn ghost icon sm danger" type="button" onClick={() => void removeList()} aria-label={t('common.delete')}><Trash2 size={14} /></button>
        </div>
      </div>
      <div className="card-b flush">
        {list.items.map((it) => (
          <div className={`row-item shop-item${it.done ? ' done' : ''}`} key={it.id}>
            <label className="cb">
              <input type="checkbox" checked={it.done} onChange={() => toggleItem.mutate(it.id)} aria-label={it.text} />
              <span className="box"><Check size={12} /></span>
            </label>
            <div className="body"><div className="ttl">{it.text}</div></div>
            <button className="btn ghost icon sm danger" type="button" onClick={() => deleteItem.mutate(it.id)} aria-label={t('common.delete')}><Trash2 size={13} /></button>
          </div>
        ))}
        <div className="shop-add">
          <input className="inp sm" value={text} onChange={(e) => setText(e.target.value)} placeholder={t('shopping.addItem')}
            onKeyDown={(e) => { if (e.key === 'Enter') add() }} />
          <button className="btn sm primary" type="button" onClick={add} disabled={addItem.isPending}><Plus size={14} /></button>
        </div>
      </div>
    </div>
  )
}

export function ShoppingPage() {
  const { t } = useTranslation()
  const { data: lists, isLoading } = useShoppingLists()
  const { createList } = useShoppingMutations()
  const [name, setName] = useState('')

  const add = () => {
    const v = name.trim()
    if (!v) return
    createList.mutate(v, { onSuccess: () => setName('') })
  }

  return (
    <div className="wrap wide">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-life)' }}>{t('nav.shopping')}</div>
          <h1>{t('shopping.title')}</h1>
          <p className="sub">{t('shopping.sub')}</p>
        </div>
        <div className="actions shop-newlist">
          <input className="inp sm" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('shopping.newListPh')}
            onKeyDown={(e) => { if (e.key === 'Enter') add() }} />
          <button className="btn primary" type="button" onClick={add}><Plus size={15} />{t('shopping.newList')}</button>
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {!isLoading && (lists?.length ?? 0) === 0 && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-life)' }}><ShoppingCart size={20} /></span>
          <h4>{t('shopping.emptyTitle')}</h4>
          <p>{t('shopping.emptySub')}</p>
        </div></div>
      )}

      {!isLoading && (lists?.length ?? 0) > 0 && (
        <div className="notes-grid">
          {lists!.map((l) => <ListCard key={l.id} list={l} />)}
        </div>
      )}
    </div>
  )
}
