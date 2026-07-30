import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Trash2 } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import { useExamAttempts, useExamMutations, useExamSubjects } from './hooks'

/** Past attempts, newest first — open one to re-read its mark sheet, or delete it. */
export function AttemptHistory({ onOpen, emptyIcon }: { onOpen: (attemptId: string) => void; emptyIcon: ReactNode }) {
  const { t, i18n } = useTranslation()
  const { data: attempts, isLoading } = useExamAttempts()
  const { data: subjects } = useExamSubjects()
  const { remove } = useExamMutations()

  /** Turns the stored "zup,znr" into readable short titles (empty means the whole bank). */
  const lawLabel = (codes: string) => {
    if (!codes) return t('exams.allLaws')
    return codes
      .split(',')
      .map((c) => subjects?.laws.find((l) => l.code === c)?.shortTitle ?? c)
      .join(' · ')
  }

  const del = async (id: string) => {
    if (await confirm({ title: t('exams.history.deleteTitle'), message: t('exams.history.deleteMsg'), confirmLabel: t('common.delete'), danger: true }))
      remove.mutate(id)
  }

  if (isLoading) return <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>

  if ((attempts?.length ?? 0) === 0) {
    return (
      <div className="card"><div className="card-b empty">
        <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-exams)' }}>{emptyIcon}</span>
        <h4>{t('exams.history.emptyTitle')}</h4>
        <p>{t('exams.history.emptySub')}</p>
      </div></div>
    )
  }

  return (
    <div className="card">
      <div className="card-h"><div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-exams)' }} /><h3>{t('exams.tab.history')}</h3></div></div>
      <div className="card-b flush scroll-list">
        {attempts!.map((a) => {
          const finished = !!a.finishedAtUtc
          return (
          <div className="row-item exam-hist" key={a.id}>
            <span className={`grade${finished ? (a.passed ? ' ok' : ' no') : ' open'}`}>{finished ? a.grade : '—'}</span>
            <button type="button" className="body" onClick={() => onOpen(a.id)}>
              <div className="ttl">{lawLabel(a.laws)}</div>
              <div className="sub">
                {new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(a.startedAtUtc))}
                {' · '}{t('exams.history.questions', { n: a.questionCount })}
                {' · '}{t(`exams.mode.${a.mode}`)}
              </div>
            </button>
            {finished
              ? <span className={`chip ${a.passed ? 'ok' : 'danger'}`}>{a.percent}%</span>
              : <span className="chip warn">{t('exams.history.unfinished')}</span>}
            <button className="btn ghost icon sm danger" type="button" onClick={() => void del(a.id)} aria-label={t('common.delete')}>
              <Trash2 size={13} />
            </button>
          </div>
          )
        })}
      </div>
    </div>
  )
}
