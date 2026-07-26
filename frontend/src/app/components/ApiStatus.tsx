import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/platform/api/client'

interface Ping { message: string; utc: string }
type State = 'checking' | 'online' | 'offline'

const chipClass: Record<State, string> = { online: 'ok', offline: 'danger', checking: 'warn' }

/** M1 sanity widget: proves the frontend reaches the API and reads DB readiness. */
export function ApiStatus() {
  const { t } = useTranslation()

  const ping = useQuery({ queryKey: ['diagnostics', 'ping'], queryFn: () => api.get<Ping>('/api/ping') })
  const dbReady = useQuery({ queryKey: ['diagnostics', 'health-ready'], queryFn: async () => (await fetch('/health/ready')).ok })

  const apiState: State = ping.isLoading ? 'checking' : ping.isSuccess ? 'online' : 'offline'
  const dbState: State = dbReady.isLoading ? 'checking' : dbReady.data ? 'online' : 'offline'

  return (
    <div className="card">
      <div className="card-h"><div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--brand)' }} /><h3>{t('status.title')}</h3></div></div>
      <div className="card-b flush">
        <div className="row-item">
          <div className="body"><div className="ttl">{t('status.api')}</div></div>
          <div className="end"><span className={`chip ${chipClass[apiState]}`}>{t(`status.${apiState}`)}</span></div>
        </div>
        <div className="row-item">
          <div className="body"><div className="ttl">{t('status.database')}</div></div>
          <div className="end"><span className={`chip ${chipClass[dbState]}`}>{t(`status.${dbState}`)}</span></div>
        </div>
      </div>
    </div>
  )
}
