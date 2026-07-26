import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMe, useUpdateProfile } from '@/platform/auth/useAuth'
import { currenciesApi } from '@/platform/money/api'
import { toast } from '@/platform/ui/toastStore'
import { financeKeys } from './api'

/**
 * A right-here currency switcher on the Finance page — the same per-member preferred currency as in the
 * profile, surfaced where money lives. Changing it re-converts every amount on read.
 */
export function CurrencyPicker() {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const update = useUpdateProfile()
  const qc = useQueryClient()
  const { data: currencies = [] } = useQuery({ queryKey: ['currencies'], queryFn: currenciesApi.list })

  if (!me) return null

  const change = (code: string) => {
    update.mutate(
      {
        firstName: me.firstName, lastName: me.lastName,
        preferredCulture: me.preferredCulture, preferredCurrency: code, digestFrequency: me.digestFrequency,
      },
      {
        onSuccess: () => {
          void qc.invalidateQueries({ queryKey: financeKeys.summary })
          void qc.invalidateQueries({ queryKey: financeKeys.transactions })
          void qc.invalidateQueries({ queryKey: financeKeys.bills })
          void qc.invalidateQueries({ queryKey: financeKeys.budgets })
          toast.success(t('finance.currencyChanged'))
        },
      },
    )
  }

  return (
    <label className="cur-pick" title={t('profile.currency')}>
      <span>{t('profile.currency')}</span>
      <select className="sel" value={me.preferredCurrency} onChange={(e) => change(e.target.value)} disabled={update.isPending}>
        {currencies.map((c) => <option key={c.code} value={c.code}>{c.symbol} · {c.code}</option>)}
      </select>
    </label>
  )
}
