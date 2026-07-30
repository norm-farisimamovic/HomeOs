import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { BookOpen, Eye, EyeOff, Search } from 'lucide-react'
import type { StudyQuestion } from './api'
import { LawPicker } from './LawPicker'
import { useExamSubjects, useStudyQuestions } from './hooks'

/**
 * Revision mode: the questions of the chosen laws with the answers **already visible** — nothing to
 * guess, nothing marked. The answers can be hidden to turn the same list into a self-test.
 */
export function StudyPanel() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const { data: subjects } = useExamSubjects()
  // Global search deep-links here with a single law (`?tab=study&law=zup&q=…`).
  const [laws, setLaws] = useState<string[]>(() => (params.get('law') ?? '').split(',').filter(Boolean))
  const [query, setQuery] = useState(params.get('q') ?? '')
  const [showAnswers, setShowAnswers] = useState(true)
  const { data, isLoading, hasNextPage, fetchNextPage, isFetchingNextPage } = useStudyQuestions(laws, query)

  const toggleLaw = (code: string) =>
    setLaws((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]))

  const questions = data?.pages.flatMap((p) => p.questions) ?? []
  const total = data?.pages[0]?.total ?? 0

  return (
    <>
      <div className="card">
        <div className="card-h">
          <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-exams)' }} /><h3>{t('exams.study.pick')}</h3></div>
          <span className="chip">{t('exams.showing', { shown: questions.length, total })}</span>
        </div>
        <div className="card-b">
          <LawPicker laws={subjects?.laws ?? []} selected={laws} onToggle={toggleLaw} counts="none" />
          <p className="hint exam-allhint">{t('exams.study.allHint')}</p>

          <div className="exam-study-bar">
            <label className="exam-search">
              <Search size={14} />
              <input className="inp sm" value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('exams.searchPlaceholder')} aria-label={t('exams.searchPlaceholder')} />
            </label>
            <button className="btn ghost sm" type="button" onClick={() => setShowAnswers((s) => !s)} aria-pressed={showAnswers}>
              {showAnswers ? <EyeOff size={14} /> : <Eye size={14} />}
              {showAnswers ? t('exams.study.hideAnswers') : t('exams.study.showAnswers')}
            </button>
          </div>
        </div>
      </div>

      {isLoading && <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>}

      {!isLoading && questions.length === 0 && (
        <div className="card"><div className="card-b empty">
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-exams)' }}><BookOpen size={20} /></span>
          <h4>{t('exams.study.emptyTitle')}</h4>
          <p>{t('exams.study.emptySub')}</p>
        </div></div>
      )}

      {!isLoading && questions.length > 0 && (
        <div className="card">
          <div className="card-b flush">
            {questions.map((q, i) => <StudyRow key={q.id} question={q} index={i + 1} showAnswer={showAnswers} />)}
          </div>
          {hasNextPage && (
            <div className="modal-f exam-more">
              <button className="btn" type="button" onClick={() => void fetchNextPage()} disabled={isFetchingNextPage}>
                {isFetchingNextPage ? t('common.loading') : t('exams.study.more')}
              </button>
            </div>
          )}
        </div>
      )}
    </>
  )
}

/** One bank question with its answer — shown outright while revising, foldable for a self-test. */
function StudyRow({ question, index, showAnswer }: { question: StudyQuestion; index: number; showAnswer: boolean }) {
  const { t } = useTranslation()
  const [revealed, setRevealed] = useState(false)
  const open = showAnswer || revealed

  return (
    <div className={`exam-study${open ? ' open' : ''}`}>
      <div className="q">
        <span className="meta">
          <span className="chip">{index}.</span>
          <span className="chip" data-m style={{ ['--mc' as string]: 'var(--m-exams)' }}>{question.lawShort}</span>
          {question.article && <span className="chip">{question.article}</span>}
          {question.topic && <span className="chip hide-sm">{question.topic}</span>}
        </span>
        <p className="text">{question.text}</p>
      </div>

      {open ? (
        <div className="a">
          {question.type === 'open' ? (
            <p className="model">{question.answer}</p>
          ) : (
            <ul className="opts">
              {question.options.map((option, i) => (
                <li key={i} className={question.correct.includes(i) ? 'right' : ''}>
                  <span className="tag">{question.correct.includes(i) ? t('exams.result.correctTag') : ''}</span>
                  <span>{option}</span>
                </li>
              ))}
            </ul>
          )}
          {question.explanation && <p className="why">{question.explanation}</p>}
        </div>
      ) : (
        <div className="a">
          <button className="btn ghost sm" type="button" onClick={() => setRevealed(true)}>
            <Eye size={14} />{t('exams.study.reveal')}
          </button>
        </div>
      )}
    </div>
  )
}
