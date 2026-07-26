import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/platform/api/client'
import { Avatar } from '@/shared/components/Avatar'

interface ScoreEntry { memberId: string; memberName: string; points: number; count: number }

const MEDALS = ['🥇', '🥈', '🥉']

/** Household scoreboard — points from completed chores. Hidden until someone has scored. */
export function ScoreboardCard() {
  const { t } = useTranslation()
  const { data: rows } = useQuery({ queryKey: ['scoreboard'], queryFn: () => api.get<ScoreEntry[]>('/api/scoreboard') })
  if (!rows) return null

  return (
    <div className="card">
      <div className="card-h"><div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--brass)' }} /><h3>{t('scoreboard.title')}</h3></div></div>
      <div className="card-b flush">
        {rows.length === 0 && (
          <div className="empty"><span className="empty-ico" style={{ ['--mc' as string]: 'var(--brass)' }}>🏆</span><h4>{t('scoreboard.emptyTitle')}</h4><p>{t('scoreboard.emptySub')}</p></div>
        )}
        {rows.map((r, i) => (
          <div className="row-item score-row" key={r.memberId}>
            <span className="score-rank">{MEDALS[i] ?? i + 1}</span>
            <Avatar name={r.memberName} memberId={r.memberId} size="xs" />
            <div className="body"><div className="ttl">{r.memberName}</div><div className="meta">{t('scoreboard.tasksDone', { count: r.count })}</div></div>
            <div className="end"><span className="chip solid">{t('scoreboard.points', { points: r.points })}</span></div>
          </div>
        ))}
      </div>
    </div>
  )
}
