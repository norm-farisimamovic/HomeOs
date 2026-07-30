import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { GraduationCap } from 'lucide-react'
import { useExamAttempt } from './hooks'
import { ExamSetup } from './ExamSetup'
import { ExamRunner } from './ExamRunner'
import { ExamResult } from './ExamResult'
import { StudyPanel } from './StudyPanel'
import { AttemptHistory } from './AttemptHistory'

type Tab = 'exam' | 'study' | 'history'

/**
 * Exam practice for the professional exam: draw a paper from the question bank, answer it and get it
 * marked (multiple-choice by rule, written answers on meaning), plus a study mode over the same bank.
 */
export function ExamsPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const [attemptId, setAttemptId] = useState<string | null>(null)

  // Global search links straight into study mode (`/exams?tab=study&law=…&q=…`).
  const tab = (params.get('tab') as Tab | null) ?? 'exam'
  const setTab = (next: Tab) => {
    const p = new URLSearchParams(params)
    p.set('tab', next)
    setParams(p, { replace: true })
  }

  const { data: attempt, isLoading } = useExamAttempt(attemptId)

  return (
    <div className="wrap wide exams-page">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-exams)' }}>{t('nav.exams')}</div>
          <h1>{t('exams.title')}</h1>
          <p className="sub">{t('exams.sub')}</p>
        </div>
        <div className="actions">
          <div className="seg" role="tablist">
            <button type="button" role="tab" aria-selected={tab === 'exam'} className={tab === 'exam' ? 'on' : ''} onClick={() => setTab('exam')}>{t('exams.tab.exam')}</button>
            <button type="button" role="tab" aria-selected={tab === 'study'} className={tab === 'study' ? 'on' : ''} onClick={() => setTab('study')}>{t('exams.tab.study')}</button>
            <button type="button" role="tab" aria-selected={tab === 'history'} className={tab === 'history' ? 'on' : ''} onClick={() => setTab('history')}>{t('exams.tab.history')}</button>
          </div>
        </div>
      </div>

      {tab === 'exam' && (
        <>
          {!attemptId && <ExamSetup onStarted={setAttemptId} />}
          {attemptId && isLoading && (
            <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>
          )}
          {attempt && !attempt.finished && <ExamRunner attempt={attempt} onLeave={() => setAttemptId(null)} />}
          {attempt && attempt.finished && <ExamResult attempt={attempt} onClose={() => setAttemptId(null)} />}
        </>
      )}

      {tab === 'study' && <StudyPanel />}

      {tab === 'history' && (
        <AttemptHistory
          onOpen={(id) => { setAttemptId(id); setTab('exam') }}
          emptyIcon={<GraduationCap size={20} />}
        />
      )}
    </div>
  )
}
