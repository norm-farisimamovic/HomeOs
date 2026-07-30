import { useTranslation } from 'react-i18next'
import { Check, Info, RotateCcw, Sparkles, X } from 'lucide-react'
import type { ExamAttempt, ExamQuestion } from './api'
import { parsePicked } from './answers'
import { ArticleLink } from './ArticleLink'

/** The mark sheet: the score, the grade, and every question with the right answer next to yours. */
export function ExamResult({ attempt, onClose, onOpenLaw }: {
  attempt: ExamAttempt
  onClose: () => void
  onOpenLaw?: (law: string, articleKey: string) => void
}) {
  const { t } = useTranslation()
  const graded = attempt.questions.filter((q) => q.graded)
  const correct = graded.filter((q) => q.correct).length
  // A paper where nothing could be marked (only written questions, no AI examiner) carries grade 0.
  const scored = attempt.grade > 0

  return (
    <>
      <div className={`card exam-score${!scored ? ' none' : attempt.passed ? ' pass' : ' fail'}`}>
        <div className="card-b">
          <div className="dial">
            <span className="pct">{scored ? `${attempt.percent}%` : '—'}</span>
            <span className="lbl">{t('exams.result.score')}</span>
          </div>
          <div className="verdict">
            {scored ? (
              <>
                <div className="grade">
                  <span className="n">{attempt.grade}</span>
                  <span className="w">{t(`exams.grade.${attempt.grade}`)}</span>
                </div>
                <div className={`state ${attempt.passed ? 'ok' : 'no'}`}>
                  {attempt.passed ? t('exams.result.passed') : t('exams.result.failed')}
                </div>
                <div className="hint">
                  {t('exams.result.points', { earned: attempt.earnedPoints, max: attempt.maxPoints })} ·{' '}
                  {t('exams.result.correct', { correct, total: graded.length })}
                </div>
              </>
            ) : (
              <>
                <div className="grade"><span className="w">{t('exams.result.notScored')}</span></div>
                <div className="hint">{t('exams.result.notScoredSub')}</div>
              </>
            )}
            {attempt.ungradedCount > 0 && scored && (
              <div className="note"><Info size={13} />{t('exams.result.ungradedNote', { n: attempt.ungradedCount })}</div>
            )}
          </div>
          <div className="acts">
            <button className="btn primary" type="button" onClick={onClose}><RotateCcw size={15} />{t('exams.result.again')}</button>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-h"><div className="t"><h3>{t('exams.result.review')}</h3></div></div>
        <div className="card-b flush">
          {attempt.questions.map((q, i) => (
            <ReviewRow key={q.id} question={q} index={i + 1} onOpenLaw={onOpenLaw} />
          ))}
        </div>
      </div>
    </>
  )
}

/** One marked question: what was asked, what you answered, what was right, and why. */
function ReviewRow({ question, index, onOpenLaw }: {
  question: ExamQuestion
  index: number
  onOpenLaw?: (law: string, articleKey: string) => void
}) {
  const { t } = useTranslation()
  const picked = parsePicked(question.given)
  const partial = question.graded && !question.correct && (question.points ?? 0) > 0
  const state = !question.graded ? 'skipped' : question.correct ? 'ok' : partial ? 'partial' : 'no'

  return (
    <div className={`exam-review ${state}`}>
      <div className="head">
        <span className="ic">
          {!question.graded ? <Info size={13} /> : question.correct ? <Check size={13} /> : <X size={13} />}
        </span>
        <div className="q">
          <div className="meta">
            <span className="chip">{index}.</span>
            <span className="chip" data-m style={{ ['--mc' as string]: 'var(--m-exams)' }}>{question.lawShort}</span>
            {question.article && (
              <ArticleLink law={question.law} articleKey={question.articleKey} citation={question.article} onOpenLaw={onOpenLaw} />
            )}
            {question.graded ? (
              <span className={`chip ${question.correct ? 'ok' : partial ? 'warn' : 'danger'}`}>
                {t('exams.result.gotPoints', { points: question.points ?? 0, max: question.maxPoints })}
              </span>
            ) : (
              <span className="chip">{t('exams.result.skipped')}</span>
            )}
          </div>
          <p className="text">{question.text}</p>
        </div>
      </div>

      {question.type === 'open' ? (
        <div className="body">
          <div className="line"><b>{t('exams.result.yourAnswer')}</b><span>{question.given || t('exams.result.blank')}</span></div>
          {question.modelAnswer && <div className="line good"><b>{t('exams.result.modelAnswer')}</b><span>{question.modelAnswer}</span></div>}
          {question.feedback && (
            <div className="line fb">
              <b>
                {question.aiGraded
                  ? <><Sparkles size={12} /> {t('exams.result.examiner')}</>
                  : t('exams.result.note')}
              </b>
              <span>{question.feedback}</span>
            </div>
          )}
        </div>
      ) : (
        <div className="body">
          <ul className="opts">
            {question.options.map((option, i) => {
              const isCorrect = question.correctOptions.includes(i)
              const isPicked = picked.includes(i)
              return (
                <li key={i} className={`${isCorrect ? 'right' : ''}${isPicked && !isCorrect ? ' wrong' : ''}`}>
                  <span className="tag">{isCorrect ? t('exams.result.correctTag') : isPicked ? t('exams.result.yoursTag') : ''}</span>
                  <span>{option}</span>
                </li>
              )
            })}
          </ul>
        </div>
      )}

      {question.explanation && <p className="why">{question.explanation}</p>}
    </div>
  )
}
