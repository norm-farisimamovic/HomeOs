import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, Home, Pencil, Plus, Trash2, UserPlus, X } from 'lucide-react'
import { useMe } from '@/platform/auth/useAuth'
import { Avatar } from '@/shared/components/Avatar'
import { confirm } from '@/platform/ui/confirmStore'
import { toast } from '@/platform/ui/toastStore'
import { useSwitchHousehold, useSwitchableHouseholds } from '@/platform/households/api'
import { ROLES, type HouseholdMember } from './api'
import { useCancelInvite, useChangeRole, useHouseholdMembers, useInvites, useRemoveMember, useRenameHousehold } from './hooks'
import { InviteModal } from './InviteModal'
import { EditMemberModal } from './EditMemberModal'
import { CreateHouseholdModal } from './CreateHouseholdModal'

export function HouseholdPage() {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const isManager = !!me?.roles.some((r) => r === 'Owner' || r === 'Admin')
  const { data: members, isLoading } = useHouseholdMembers()
  const { data: invites } = useInvites(isManager)
  const changeRole = useChangeRole()
  const removeMember = useRemoveMember()
  const cancelInvite = useCancelInvite()
  const rename = useRenameHousehold()
  const { data: myHouseholds = [] } = useSwitchableHouseholds()
  const switchTo = useSwitchHousehold()
  const [inviteOpen, setInviteOpen] = useState(false)
  const [createHouseholdOpen, setCreateHouseholdOpen] = useState(false)
  const [editName, setEditName] = useState<string | null>(null)
  const [editMember, setEditMember] = useState<HouseholdMember | null>(null)

  const onSwitchHousehold = (householdId: string) => {
    if (switchTo.isPending) return
    switchTo.mutate(householdId, { onSuccess: () => window.location.assign('/'), onError: () => toast.error(t('common.error')) })
  }

  const saveName = () => {
    const v = (editName ?? '').trim()
    if (v && v !== me?.householdName) rename.mutate(v)
    setEditName(null)
  }

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow">{t('nav.household')}</div>
          {editName === null ? (
            <h1 className="editable-h">
              {me?.householdName ?? t('household.title')}
              {isManager && (
                <button className="btn ghost icon sm" type="button" onClick={() => setEditName(me?.householdName ?? '')} aria-label={t('common.edit')}><Pencil size={15} /></button>
              )}
            </h1>
          ) : (
            <div className="name-edit">
              <input className="inp" autoFocus value={editName} onChange={(e) => setEditName(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') saveName(); if (e.key === 'Escape') setEditName(null) }} />
              <button className="btn primary icon" type="button" onClick={saveName} aria-label={t('common.save')}><Check size={16} /></button>
              <button className="btn ghost icon" type="button" onClick={() => setEditName(null)} aria-label={t('common.cancel')}><X size={16} /></button>
            </div>
          )}
          <p className="sub">{isManager ? t('household.subManager') : t('household.subMember')}</p>
        </div>
        {isManager && (
          <div className="actions">
            <button className="btn primary" type="button" onClick={() => setInviteOpen(true)}><UserPlus size={15} />{t('household.invite')}</button>
          </div>
        )}
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="card-h"><div className="t"><h3>{t('household.members')}</h3></div><span className="chip">{members?.length ?? 0}</span></div>
        <div className="card-b flush">
          {isLoading && <div className="card-b"><p className="hint">{t('common.loading')}</p></div>}
          {members?.map((m) => (
            <div className="row-item" key={m.id}>
              <Avatar name={m.displayName} memberId={m.id} />
              <div className="body">
                <div className="ttl">{m.displayName}{m.isYou && <span className="chip solid">{t('household.you')}</span>}</div>
                <div className="meta">{m.email}</div>
              </div>
              <div className="end">
                {isManager && !m.isYou && m.role !== 'Owner' ? (
                  <>
                    <select className="sel" style={{ width: 'auto', minHeight: 30 }} value={m.role}
                      onChange={(e) => changeRole.mutate({ id: m.id, role: e.target.value })}>
                      {ROLES.filter((r) => r !== 'Owner').map((r) => <option key={r} value={r}>{t(`household.role.${r.toLowerCase()}`)}</option>)}
                    </select>
                    <button className="btn ghost icon sm" type="button" title={t('household.editMember')} onClick={() => setEditMember(m)}>
                      <Pencil size={14} />
                    </button>
                    <button className="btn ghost icon sm danger" type="button" title={t('household.remove')}
                      onClick={() => { void (async () => {
                        if (await confirm({ title: t('household.confirmRemoveTitle'), message: t('household.confirmRemove', { name: m.displayName }), confirmLabel: t('household.remove'), danger: true })) removeMember.mutate(m.id)
                      })() }}>
                      <Trash2 size={14} />
                    </button>
                  </>
                ) : (
                  <>
                    <span className="chip">{t(`household.role.${m.role.toLowerCase()}`)}</span>
                    {isManager && (
                      <button className="btn ghost icon sm" type="button" title={t('household.editMember')} onClick={() => setEditMember(m)}>
                        <Pencil size={14} />
                      </button>
                    )}
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>

      {isManager && invites && invites.length > 0 && (
        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('household.pending')}</h3></div><span className="chip warn">{invites.length}</span></div>
          <div className="card-b flush">
            {invites.map((inv) => (
              <div className="row-item" key={inv.id}>
                <div className="body">
                  <div className="ttl">{inv.displayName}</div>
                  <div className="meta">{inv.email} · <span className="chip">{t(`household.role.${inv.role.toLowerCase()}`)}</span></div>
                </div>
                <div className="end">
                  <button className="btn sm ghost" type="button"
                    onClick={() => { void (async () => {
                      if (await confirm({ title: t('household.confirmCancelInvite.title'), message: t('household.confirmCancelInvite.message', { name: inv.displayName }), confirmLabel: t('household.cancelInvite'), danger: true })) cancelInvite.mutate(inv.id)
                    })() }}>{t('household.cancelInvite')}</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="card" style={{ marginTop: 14 }}>
        <div className="card-h">
          <div className="t"><h3>{t('households.yours')}</h3></div>
          <button className="btn sm" type="button" onClick={() => setCreateHouseholdOpen(true)}><Plus size={14} />{t('households.create')}</button>
        </div>
        <div className="card-b flush">
          {myHouseholds.map((h) => (
            <div className="row-item" key={h.householdId}>
              <span className="empty-ico sm" style={{ ['--mc' as string]: 'var(--brand)' }}><Home size={15} /></span>
              <div className="body">
                <div className="ttl">{h.householdName}{h.current && <span className="chip solid">{t('households.current')}</span>}</div>
                <div className="meta">{h.roles}</div>
              </div>
              <div className="end">
                {!h.current && (
                  <button className="btn sm ghost" type="button" disabled={switchTo.isPending} onClick={() => onSwitchHousehold(h.householdId)}>{t('households.switch')}</button>
                )}
              </div>
            </div>
          ))}
          <p className="hint" style={{ padding: '10px 16px 0' }}>{t('households.manageHint')}</p>
        </div>
      </div>

      {inviteOpen && <InviteModal onClose={() => setInviteOpen(false)} />}
      {createHouseholdOpen && <CreateHouseholdModal onClose={() => setCreateHouseholdOpen(false)} />}
      {editMember && <EditMemberModal member={editMember} onClose={() => setEditMember(null)} />}
    </div>
  )
}
