import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, ChevronLeft, ChevronRight, Flag } from 'lucide-react'
import { confirm } from '@/platform/ui/confirmStore'
import type { ExamAttempt, ExamQuestion } from './api'
import { formatPicked, isAnswered, parsePicked } from './answers'
import { useExamMutations } from './hooks'

/** Sits the paper: one question at a time, answers autosaved, then hand in for marking. */
export function ExamRunner({ attempt, onLeave }: { attempt: ExamAttempt; onLeave: () => void }) {
  const { t } = useTranslation()
  const { saveAnswer, finish } = useExamMutations()
  const [index, setIndex] = useState(0)
  // Answers live here while the paper is open; the server copy is updated in the background.
  const [answers, setAnswers] = useState<Record<string, string>>(
    () => Object.fromEntries(attempt.questions.map((q) => [q.id, q.given])),
  )
  const timers = useRef<Record<string, ReturnType<typeof setTimeout>>>({})

  // Any pending debounce would fire after the paper is gone — drop them on unmount.
  useEffect(() => {
    const pending = timers.current
    return () => { Object.values(pending).forEach(clearTimeout) }
  }, [])

  const question = attempt.questions[Math.min(index, attempt.questions.length - 1)]
  const answered = attempt.questions.filter((q) => isAnswered(answers[q.id])).length
  const total = attempt.questions.length

  const persist = (questionId: string, answer: string, debounceMs: number) => {
    clearTimeout(timers.current[questionId])
    timers.current[questionId] = setTimeout(
      () => saveAnswer.mutate({ attemptId: attempt.id, questionId, answer }),
      debounceMs,
    )
  }

  const setAnswer = (value: string, debounceMs = 0) => {
    if (!question) return
    setAnswers((prev) => ({ ...prev, [question.id]: value }))
    persist(question.id, value, debounceMs)
  }

  const pick = (option: number) => {
    if (!question) return
    if (question.type === 'single') { setAnswer(String(option)); return }
    const picked = parsePicked(answers[question.id] ?? '')
    const next = picked.includes(option) ? picked.filter((p) => p !== option) : [...picked, option]
    setAnswer(formatPicked(next))
  }

  const handIn = async () => {
    const blank = total - answered
    const ok = await confirm({
      title: t('exams.finishTitle'),
      message: blank > 0 ? t('exams.finishBlank', { n: blank }) : t('exams.finishMsg'),
      confirmLabel: t('exams.finish'),
    })
    if (!ok) return
    // Flush anything still waiting on a debounce so the last keystrokes are marked too.
    Object.values(timers.current).forEach(clearTimeout)
    await Promise.all(
      attempt.questions
        .filter((q) => (answers[q.id] ?? '') !== q.given)
        .map((q) => saveAnswer.mutateAsync({ attemptId: attempt.id, questionId: q.id, answer: answers[q.id] ?? '' })),
    )
    finish.mutate(attempt.id)
  }

  const abandon = async () => {
    if (await confirm({ title: t('exams.leaveTitle'), message: t('exams.leaveMsg'), confirmLabel: t('exams.leave') })) onLeave()
  }

  return (
    <div className="card exam-run">
      <div className="card-h">
        <div className="t">
          <i className="mdot" style={{ ['--mc' as string]: 'var(--m-exams)' }} />
          <h3>{t('exams.questionOf', { n: index + 1, total })}</h3>
        </div>
        <div className="exam-run-meta">
          <span className="chip">{t('exams.answeredOf', { answered, total })}</span>
          <button className="btn ghost sm" type="button" onClick={() => void abandon()}>{t('exams.leave')}</button>
        </div>
      </div>

      <div className="exam-progress" role="progressbar" aria-valuenow={answered} aria-valuemin={0} aria-valuemax={total}>
        <span style={{ width: `${total ? (answered / total) * 100 : 0}%` }} />
      </div>

      <div className="card-b">
        {question && <QuestionBody
          question={question}
          value={answers[question.id] ?? ''}
          onPick={pick}
          onWrite={(v) => setAnswer(v, 600)}
        />}
      </div>

      <div className="modal-f exam-nav">
        <button className="btn ghost" type="button" onClick={() => setIndex((i) => Math.max(0, i - 1))} disabled={index === 0}>
          <ChevronLeft size={15} />{t('exams.prev')}
        </button>
        <div className="exam-dots">
          {attempt.questions.map((q, i) => (
            <button
              key={q.id}
              type="button"
              className={`dot${i === index ? ' cur' : ''}${isAnswered(answers[q.id]) ? ' done' : ''}`}
              onClick={() => setIndex(i)}
              aria-label={t('exams.goToQuestion', { n: i + 1 })}
            />
          ))}
        </div>
        {index < total - 1 ? (
          <button className="btn primary" type="button" onClick={() => setIndex((i) => Math.min(total - 1, i + 1))}>
            {t('exams.next')}<ChevronRight size={15} />
          </button>
        ) : (
          <button className="btn primary" type="button" onClick={() => void handIn()} disabled={finish.isPending}>
            <Flag size={15} />{finish.isPending ? t('exams.marking') : t('exams.finish')}
          </button>
        )}
      </div>
    </div>
  )
}

/** The question itself — options for choice questions, a text box for written ones. */
function QuestionBody({ question, value, onPick, onWrite }: {
  question: ExamQuestion
  value: string
  onPick: (option: number) => void
  onWrite: (value: string) => void
}) {
  const { t } = useTranslation()
  const picked = parsePicked(value)

  return (
    <div className="exam-q">
      <div className="meta">
        <span className="chip" data-m style={{ ['--mc' as string]: 'var(--m-exams)' }}>{question.lawShort}</span>
        {question.article && <span className="chip">{question.article}</span>}
        <span className="chip">{t(`exams.type.${question.type}`)}</span>
        <span className="chip">{t('exams.points', { n: question.maxPoints })}</span>
      </div>
      <p className="text">{question.text}</p>

      {question.type === 'open' ? (
        <>
          <textarea
            className="ta"
            rows={7}
            value={value}
            onChange={(e) => onWrite(e.target.value)}
            placeholder={t('exams.writePlaceholder')}
            aria-label={question.text}
          />
          <p className="hint">{t('exams.openHint')}</p>
        </>
      ) : (
        <div className="exam-opts">
          {question.options.map((option, i) => {
            const on = picked.includes(i)
            return (
              <button key={i} type="button" className={`exam-opt${on ? ' on' : ''}`} onClick={() => onPick(i)} aria-pressed={on}>
                <span className={`mark${question.type === 'multi' ? ' sq' : ''}`}>{on && <Check size={12} />}</span>
                <span>{option}</span>
              </button>
            )
          })}
          {question.type === 'multi' && <p className="hint">{t('exams.multiHint')}</p>}
        </div>
      )}
    </div>
  )
}
