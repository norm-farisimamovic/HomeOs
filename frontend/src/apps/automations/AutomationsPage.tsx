import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowRight, Pencil, Plus, Trash2, Zap } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import type { Automation } from './api'
import { useAutomations, useDeleteAutomation, useUpdateAutomation } from './hooks'
import { AutomationModal } from './AutomationModal'

const key = (v: string) => v.replaceAll('.', '_')

export function AutomationsPage() {
  const { t } = useTranslation()
  const { data: rules, isLoading } = useAutomations()
  const update = useUpdateAutomation()
  const del = useDeleteAutomation()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<Automation | undefined>(undefined)

  const openNew = () => { setEditing(undefined); setModalOpen(true) }
  const openEdit = (r: Automation) => { setEditing(r); setModalOpen(true) }

  const toggle = (r: Automation) =>
    update.mutate({ id: r.id, input: { name: r.name, trigger: r.trigger, action: r.action, message: r.message ?? undefined, enabled: !r.enabled } })

  const onDelete = async (r: Automation) => {
    if (await confirm({ title: t('automations.confirmDelete.title'), message: t('automations.confirmDelete.message', { name: r.name }), confirmLabel: t('common.delete'), danger: true })) del.mutate(r.id)
  }

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow">{t('nav.automations')}</div>
          <h1>{t('automations.title')}</h1>
          <p className="sub">{t('automations.sub')}</p>
        </div>
        <div className="actions"><button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('automations.newRule')}</button></div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {!isLoading && (rules?.length ?? 0) === 0 && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--brand)' }}><Zap size={20} /></span>
          <h4>{t('automations.emptyTitle')}</h4>
          <p>{t('automations.emptySub')}</p>
          <button className="btn primary" type="button" onClick={openNew}><Plus size={15} />{t('automations.newRule')}</button>
        </div></div>
      )}

      {!isLoading && (rules?.length ?? 0) > 0 && (
        <div className="card"><div className="card-b flush">
          {rules!.map((r) => (
            <div className="row-item" key={r.id}>
              <div className="body">
                <div className="ttl">{r.name}</div>
                <div className="meta">
                  <span className="chip" style={{ ['--mc' as string]: 'var(--m-tasks)' }} data-m>{t(`automations.triggers.${key(r.trigger)}`)}</span>
                  <ArrowRight size={12} style={{ color: 'var(--text-3)' }} />
                  <span className="chip">{t(`automations.actions.${r.action}`)}</span>
                  {r.message && <span className="meta">· {r.message}</span>}
                </div>
              </div>
              <div className="end">
                <label className="sw" title={r.enabled ? t('automations.on') : t('automations.off')}>
                  <input type="checkbox" checked={r.enabled} disabled={!r.canEdit} onChange={() => toggle(r)} />
                  <span className="track" />
                </label>
                {r.canEdit && <button className="btn ghost icon sm" type="button" onClick={() => openEdit(r)} aria-label={t('common.edit')}><Pencil size={14} /></button>}
                {r.canEdit && <button className="btn ghost icon sm danger" type="button" onClick={() => void onDelete(r)} aria-label={t('common.delete')}><Trash2 size={14} /></button>}
              </div>
            </div>
          ))}
        </div></div>
      )}

      {modalOpen && <AutomationModal rule={editing} onClose={() => setModalOpen(false)} />}
    </div>
  )
}
