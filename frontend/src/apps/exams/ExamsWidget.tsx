import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { GraduationCap } from 'lucide-react'
import { useExamAttempts, useExamSubjects } from './hooks'

/** Dashboard card: how the last few papers went, and a way straight back into practice. */
export function ExamsWidget() {
  const { t } = useTranslation()
  const { data: attempts } = useExamAttempts()
  const { data: subjects } = useExamSubjects()

  const finished = (attempts ?? []).filter((a) => !!a.finishedAtUtc).slice(0, 3)
  const last = finished[0]

  return (
    <div className="card">
      <div className="card-h">
        <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-exams)' }} /><h3>{t('nav.exams')}</h3></div>
        <Link className="view-all" to="/exams">{t('exams.widget.practice')}</Link>
      </div>
      <div className="card-b">
        {last ? (
          <>
            <div className="exam-widget-last">
              <span className={`grade${last.passed ? ' ok' : ' no'}`}>{last.grade}</span>
              <div>
                <div className="ttl">{t('exams.widget.lastScore', { percent: last.percent })}</div>
                <div className="hint">{t(`exams.grade.${last.grade}`)}</div>
              </div>
            </div>
            {finished.length > 1 && (
              <div className="exam-widget-spark">
                {[...finished].reverse().map((a) => (
                  <span key={a.id} className={a.passed ? 'ok' : 'no'} style={{ height: `${Math.max(a.percent, 6)}%` }} title={`${a.percent}%`} />
                ))}
              </div>
            )}
          </>
        ) : (
          <div className="empty">
            <span className="empty-ico sm" style={{ ['--mc' as string]: 'var(--m-exams)' }}><GraduationCap size={18} /></span>
            <p>{t('exams.widget.empty', { n: subjects?.totalQuestions ?? 0 })}</p>
          </div>
        )}
      </div>
    </div>
  )
}
