import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { GraduationCap, ListChecks, Play, Sparkles } from 'lucide-react'
import { toast } from '@/platform/ui/toastStore'
import type { StartExam } from './api'
import { LawPicker } from './LawPicker'
import { useExamMutations, useExamSubjects } from './hooks'

const PRESETS = [10, 20, 30, 50]
const MODES: StartExam['mode'][] = ['mixed', 'choice', 'open']
const MIN = 5
const MAX = 100

/** Pick the laws, how many questions and which kinds, then draw a paper. */
export function ExamSetup({ onStarted }: { onStarted: (attemptId: string) => void }) {
  const { t } = useTranslation()
  const { data: subjects, isLoading } = useExamSubjects()
  const { start } = useExamMutations()
  const [selected, setSelected] = useState<string[]>([])
  const [count, setCount] = useState(20)
  const [mode, setMode] = useState<StartExam['mode']>('mixed')

  const toggle = (code: string) =>
    setSelected((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]))

  // Nothing ticked means "the whole bank" — the server treats an empty list the same way.
  const laws = subjects?.laws ?? []
  const chosen = selected.length > 0 ? selected : laws.map((l) => l.code)
  const available = laws
    .filter((l) => chosen.includes(l.code))
    .reduce((sum, l) => sum + (mode === 'choice' ? l.choice : mode === 'open' ? l.open : l.total), 0)

  // Asking for more than the pool holds isn't an error — the paper is simply as long as the pool allows.
  const willAsk = Math.min(count, available)

  const begin = () => {
    if (available === 0) { toast.error(t('exams.noQuestions')); return }
    start.mutate({ laws: selected, count, mode }, { onSuccess: (a) => onStarted(a.id) })
  }

  if (isLoading) return <div className="card"><div className="card-b"><p className="hint">{t('common.loading')}</p></div></div>

  return (
    <>
      <div className="card">
        <div className="card-h">
          <div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--m-exams)' }} /><h3>{t('exams.pickLaws')}</h3></div>
          <span className="chip">{t('exams.bankSize', { n: subjects?.totalQuestions ?? 0 })}</span>
        </div>
        <div className="card-b">
          <LawPicker laws={laws} selected={selected} onToggle={toggle} />
          <p className="hint exam-allhint">{t('exams.allHint')}</p>
        </div>
      </div>

      <div className="grid g2 exam-config">
        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('exams.length')}</h3></div></div>
          <div className="card-b">
            <div className="exam-count">
              <div className="seg wrap">
                {PRESETS.map((c) => (
                  <button key={c} type="button" className={count === c ? 'on' : ''} onClick={() => setCount(c)}>{c}</button>
                ))}
              </div>
              <label className="exam-count-own">
                <span className="lbl">{t('exams.customCount')}</span>
                <input
                  className="inp sm"
                  type="number"
                  min={MIN}
                  max={MAX}
                  step={1}
                  value={count}
                  onChange={(e) => setCount(Math.min(MAX, Math.max(MIN, Number(e.target.value) || MIN)))}
                />
              </label>
            </div>
            <p className="hint">{t('exams.availableQuestions', { n: available })}</p>
            {willAsk < count && <p className="hint">{t('exams.cappedTo', { n: willAsk })}</p>}
          </div>
        </div>

        <div className="card">
          <div className="card-h"><div className="t"><h3>{t('exams.modeLabel')}</h3></div></div>
          <div className="card-b">
            <div className="seg wrap">
              {MODES.map((m) => (
                <button key={m} type="button" className={mode === m ? 'on' : ''} onClick={() => setMode(m)}>{t(`exams.mode.${m}`)}</button>
              ))}
            </div>
            <p className="hint">{t(`exams.modeHint.${mode}`)}</p>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-b exam-start">
          <div className="marking-list">
            <div className="marking">
              <span className="ico" style={{ ['--mc' as string]: 'var(--m-exams)' }}><ListChecks size={15} /></span>
              <div>
                <div className="ttl">{t('exams.localMarking')}</div>
                <div className="hint">{t('exams.passHint', { percent: subjects?.passPercent ?? 60 })}</div>
              </div>
            </div>
            <div className="marking">
              <span className="ico" style={{ ['--mc' as string]: 'var(--m-exams)' }}><Sparkles size={15} /></span>
              <div>
                <div className="ttl">{subjects?.aiGrading ? t('exams.aiOn') : t('exams.aiOff')}</div>
                <div className="hint">{t('exams.aiScope')}</div>
              </div>
            </div>
          </div>
          <button className="btn primary exam-go" type="button" onClick={begin} disabled={start.isPending}>
            {start.isPending ? <GraduationCap size={15} /> : <Play size={15} />}
            {t('exams.begin')}
          </button>
        </div>
      </div>
    </>
  )
}
