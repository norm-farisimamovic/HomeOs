import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { GraduationCap } from 'lucide-react'
import { useExamAttempt } from './hooks'
import { ExamSetup } from './ExamSetup'
import { ExamRunner } from './ExamRunner'
import { ExamResult } from './ExamResult'
import { StudyPanel } from './StudyPanel'
import { LawsPanel } from './LawsPanel'
import { AttemptHistory } from './AttemptHistory'

type Tab = 'exam' | 'study' | 'laws' | 'history'
const TABS: Tab[] = ['exam', 'study', 'laws', 'history']

/**
 * Exam practice for the professional exam: draw a paper from the question bank and get it marked, revise
 * the same bank with the answers shown, or read the four laws in full — every article a question cites.
 */
export function ExamsPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const [attemptId, setAttemptId] = useState<string | null>(null)
  // Which law the reading tab is on, and the article to scroll to when arriving from a citation.
  const [lawCode, setLawCode] = useState('zup')
  const [jumpTo, setJumpTo] = useState<string | null>(null)

  // Global search links straight into study mode (`/exams?tab=study&law=…&q=…`).
  const tab = (TABS.find((x) => x === params.get('tab')) ?? 'exam') as Tab
  const setTab = (next: Tab) => {
    const p = new URLSearchParams(params)
    p.set('tab', next)
    setParams(p, { replace: true })
  }

  /** Open a cited article in the reading tab. */
  const openLaw = (law: string, articleKey: string) => {
    setLawCode(law)
    setJumpTo(articleKey)
    setTab('laws')
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
          <div className="seg wrap" role="tablist">
            {TABS.map((x) => (
              <button key={x} type="button" role="tab" aria-selected={tab === x} className={tab === x ? 'on' : ''} onClick={() => setTab(x)}>
                {t(`exams.tab.${x}`)}
              </button>
            ))}
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
          {attempt && attempt.finished && (
            <ExamResult attempt={attempt} onClose={() => setAttemptId(null)} onOpenLaw={openLaw} />
          )}
        </>
      )}

      {tab === 'study' && <StudyPanel onOpenLaw={openLaw} />}

      {tab === 'laws' && (
        <LawsPanel law={lawCode} jumpTo={jumpTo} onLawChange={(code) => { setLawCode(code); setJumpTo(null) }} />
      )}

      {tab === 'history' && (
        <AttemptHistory
          onOpen={(id) => { setAttemptId(id); setTab('exam') }}
          emptyIcon={<GraduationCap size={20} />}
        />
      )}
    </div>
  )
}
