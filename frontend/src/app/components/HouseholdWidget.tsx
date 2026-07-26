import type { CSSProperties } from 'react'
import { useTranslation } from 'react-i18next'
import { useMe } from '@/platform/auth/useAuth'
import { Avatar } from '@/shared/components/Avatar'

const mc = (hue: string) => ({ ['--mc' as string]: hue } as CSSProperties)

/** Dashboard household summary card (registry widget). */
export function HouseholdWidget() {
  const { t } = useTranslation()
  const { data: me } = useMe()
  return (
    <div className="card">
      <div className="card-h"><div className="t"><i className="mdot" style={mc('var(--brand)')} /><h3>{t('dashboard.household')}</h3></div></div>
      <div className="card-b flush">
        <div className="row-item">
          <div className="body"><div className="ttl">{me?.householdName}</div><div className="meta">{me?.email}</div></div>
          <div className="end"><span className="chip solid">{me?.roles.join(', ')}</span></div>
        </div>
        <div className="row-item">
          <Avatar name={me?.displayName} memberId={me?.id} />
          <div className="body"><div className="ttl">{me?.displayName}</div><div className="meta">{t('dashboard.role')}: {me?.roles.join(', ')}</div></div>
        </div>
      </div>
    </div>
  )
}
