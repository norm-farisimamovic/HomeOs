import type { CSSProperties, ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Globe, Home, Moon, Sun } from 'lucide-react'
import { useUiStore } from '@/platform/ui/uiStore'
import '@/app/auth.css'

const legend: Array<[string, string]> = [
  ['nav.tasks', 'var(--m-tasks)'],
  ['nav.calendar', 'var(--m-calendar)'],
  ['nav.finance', 'var(--m-finance)'],
  ['nav.reminders', 'var(--m-reminders)'],
  ['nav.life', 'var(--m-life)'],
]

/** Split-screen auth frame: an ambient "connected home" art panel + the form panel with tabs. */
export function AuthLayout({ active, children }: { active: 'in' | 'up'; children: ReactNode }) {
  const { t, i18n } = useTranslation()
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)
  const isDark =
    theme === 'dark' ||
    (theme === 'system' && typeof window !== 'undefined' &&
      window.matchMedia('(prefers-color-scheme: dark)').matches)
  const nextLang = i18n.resolvedLanguage === 'bs' ? 'en' : 'bs'

  return (
    <div className="auth">
      <div className="auth-art">
        <svg className="plan" viewBox="0 0 600 800" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
          <g stroke="var(--line-strong)" fill="none" strokeWidth="1">
            <rect x="60" y="90" width="220" height="170" rx="4" />
            <rect x="300" y="90" width="240" height="110" rx="4" />
            <rect x="300" y="220" width="240" height="200" rx="4" />
            <rect x="60" y="280" width="220" height="140" rx="4" />
            <rect x="60" y="440" width="480" height="230" rx="4" />
          </g>
          <g fill="none" strokeWidth="1.4" strokeDasharray="4 6" opacity=".85">
            <path d="M170 175 C 250 175 340 145 420 145" stroke="var(--m-finance)" />
            <path d="M420 145 C 470 200 430 280 420 320" stroke="var(--m-tasks)" />
            <path d="M420 320 C 330 380 240 340 170 350" stroke="var(--m-calendar)" />
            <path d="M170 350 C 140 420 220 500 300 555" stroke="var(--m-reminders)" />
          </g>
          <g>
            <circle cx="170" cy="175" r="7" fill="var(--m-finance)" />
            <circle cx="420" cy="145" r="7" fill="var(--m-tasks)" />
            <circle cx="420" cy="320" r="7" fill="var(--m-calendar)" />
            <circle cx="170" cy="350" r="7" fill="var(--m-reminders)" />
            <circle cx="300" cy="555" r="7" fill="var(--m-notes)" />
          </g>
        </svg>

        <div className="brand">
          <span className="mark"><Home size={18} /></span>
          <span className="nm">Home<span>OS</span></span>
        </div>

        <div className="copy">
          <div className="eyebrow">{t('auth.eyebrow')}</div>
          <h1>{t('auth.headline1')} <em>{t('auth.headlineEm')}</em>{t('auth.headline2')}</h1>
          <p>{t('auth.blurb')}</p>
          <div className="legend">
            {legend.map(([key, hue]) => (
              <span key={key} className="chip" data-m style={{ ['--mc' as string]: hue } as CSSProperties}>
                <i className="dot" />{t(key)}
              </span>
            ))}
          </div>
        </div>

        <div className="eyebrow foot">{t('auth.foot')}</div>
      </div>

      <div className="auth-panel">
        <div className="auth-card">
          <div className="auth-topbar">
            <button className="btn ghost sm langbtn" type="button" onClick={() => void i18n.changeLanguage(nextLang)} aria-label={t('common.language')}>
              <Globe size={14} /> <span className="code">{i18n.resolvedLanguage?.toUpperCase()}</span>
            </button>
            <button className="btn ghost sm icon" type="button" onClick={() => setTheme(isDark ? 'light' : 'dark')} aria-label={t('common.theme')}>
              {isDark ? <Sun size={16} /> : <Moon size={16} />}
            </button>
          </div>

          <div className="auth-tabs">
            <Link to="/login" className={active === 'in' ? 'on' : undefined}>{t('auth.signin')}</Link>
            <Link to="/register" className={active === 'up' ? 'on' : undefined}>{t('auth.signup')}</Link>
          </div>

          {children}
        </div>
      </div>
    </div>
  )
}
