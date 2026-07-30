import { useTranslation } from 'react-i18next'
import { Check } from 'lucide-react'
import type { LawInfo } from './api'

/**
 * The "which laws" control, shared by the exam setup and study mode so both offer the same choice:
 * tick one law, tick several to mix them, or tick none for the whole bank.
 */
export function LawPicker({ laws, selected, onToggle, counts = 'all' }: {
  laws: LawInfo[]
  selected: string[]
  onToggle: (code: string) => void
  /** Which per-law count to show under the title: the full breakdown, or none. */
  counts?: 'all' | 'none'
}) {
  const { t } = useTranslation()

  return (
    <div className="exam-laws">
      {laws.map((l) => {
        const on = selected.includes(l.code)
        return (
          <button key={l.code} type="button" className={`exam-law${on ? ' on' : ''}`} onClick={() => onToggle(l.code)} aria-pressed={on}>
            <span className="tick">{on && <Check size={13} />}</span>
            <span className="body">
              <span className="ttl">{l.title}</span>
              <span className="gz">{l.gazette}</span>
              {counts === 'all' && (
                <span className="cnt">{t('exams.lawCounts', { total: l.total, choice: l.choice, open: l.open })}</span>
              )}
            </span>
          </button>
        )
      })}
    </div>
  )
}
