import { useTranslation } from 'react-i18next'
import { Check } from 'lucide-react'
import { useSwitchHousehold, useSwitchableHouseholds } from '@/platform/households/api'
import { toast } from '@/platform/ui/toastStore'

/** Household switcher in the account menu — only shown when the person belongs to more than one household. */
export function HouseholdMenu() {
  const { t } = useTranslation()
  const { data: households = [] } = useSwitchableHouseholds()
  const switchTo = useSwitchHousehold()

  // Nothing to switch between → don't clutter the menu.
  if (households.length < 2) return null

  const onSwitch = (householdId: string, current: boolean) => {
    if (current || switchTo.isPending) return
    switchTo.mutate(householdId, {
      // Full reload: the whole session (household, roles, every cache) changes at once.
      onSuccess: () => window.location.assign('/'),
      onError: () => toast.error(t('common.error')),
    })
  }

  return (
    <>
      <div className="sep" />
      <div className="hh-menu">
        <div className="lab">{t('households.yours')}</div>
        {households.map((h) => (
          <button type="button" key={h.householdId} className={h.current ? 'hh-cur' : undefined} onClick={() => onSwitch(h.householdId, h.current)}>
            <span className="hh-nm">{h.householdName}</span>
            {h.current && <Check size={14} />}
          </button>
        ))}
      </div>
    </>
  )
}
