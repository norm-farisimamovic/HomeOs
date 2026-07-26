import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, Plus } from 'lucide-react'
import { useCreateHousehold, useSwitchHousehold, useSwitchableHouseholds } from '@/platform/households/api'
import { toast } from '@/platform/ui/toastStore'

/** Household switcher shown inside the account menu: list of the person's households + create-new. */
export function HouseholdMenu() {
  const { t } = useTranslation()
  const { data: households = [] } = useSwitchableHouseholds()
  const switchTo = useSwitchHousehold()
  const create = useCreateHousehold()
  const [adding, setAdding] = useState(false)
  const [name, setName] = useState('')

  const onSwitch = (householdId: string, current: boolean) => {
    if (current || switchTo.isPending) return
    switchTo.mutate(householdId, {
      // Full reload: the whole session (household, roles, every cache) changes at once.
      onSuccess: () => window.location.assign('/'),
      onError: () => toast.error(t('common.error')),
    })
  }

  const onCreate = () => {
    const v = name.trim()
    if (!v || create.isPending) return
    create.mutate(v, {
      onSuccess: (r) => {
        setName(''); setAdding(false)
        toast.success(t('households.created'))
        switchTo.mutate(r.householdId, { onSuccess: () => window.location.assign('/') })
      },
      onError: () => toast.error(t('common.error')),
    })
  }

  return (
    <div className="hh-menu">
      <div className="lab">{t('households.yours')}</div>
      {households.map((h) => (
        <button type="button" key={h.householdId} className={h.current ? 'hh-cur' : undefined} onClick={() => onSwitch(h.householdId, h.current)}>
          <span className="hh-nm">{h.householdName}</span>
          {h.current && <Check size={14} />}
        </button>
      ))}
      {adding ? (
        <div className="hh-add">
          <input className="inp sm" autoFocus value={name} maxLength={60} placeholder={t('households.namePh')}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') onCreate(); if (e.key === 'Escape') { setAdding(false); setName('') } }} />
          <button type="button" className="btn primary sm" onClick={onCreate} disabled={create.isPending || !name.trim()}>{t('common.add')}</button>
        </div>
      ) : (
        <button type="button" onClick={() => setAdding(true)}><Plus size={15} />{t('households.create')}</button>
      )}
    </div>
  )
}
