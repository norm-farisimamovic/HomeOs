import { create } from 'zustand'

/** UI theme preference. `system` follows the OS; `light`/`dark` force it via `[data-theme]`. */
export type Theme = 'light' | 'dark' | 'system'
/** Layout density. */
export type Density = 'cozy' | 'compact'
/** Accent (brand) colour. `default` is the Home OS green; others override `--brand` via `[data-accent]`. */
export type Accent = 'default' | 'blue' | 'violet' | 'rose' | 'amber' | 'cyan'

/** Accent options in display order (default first). Values are the swatch colours shown in the picker. */
export const ACCENTS: Array<{ id: Accent; swatch: string }> = [
  { id: 'default', swatch: '#24685A' },
  { id: 'blue', swatch: '#4F7DD9' },
  { id: 'violet', swatch: '#8168B5' },
  { id: 'rose', swatch: '#C4437B' },
  { id: 'amber', swatch: '#B08322' },
  { id: 'cyan', swatch: '#2F8AA6' },
]

interface UiState {
  theme: Theme
  density: Density
  accent: Accent
  /** Desktop: collapse the side rail to icons + tiny labels. */
  railCollapsed: boolean
  setTheme: (theme: Theme) => void
  setDensity: (density: Density) => void
  setAccent: (accent: Accent) => void
  toggleRail: () => void
}

function applyTheme(theme: Theme): void {
  const root = document.documentElement
  if (theme === 'system') root.removeAttribute('data-theme')
  else root.setAttribute('data-theme', theme)
}

function applyDensity(density: Density): void {
  document.documentElement.setAttribute('data-density', density)
}

function applyAccent(accent: Accent): void {
  const root = document.documentElement
  if (accent === 'default') root.removeAttribute('data-accent')
  else root.setAttribute('data-accent', accent)
}

const storedTheme = (localStorage.getItem('homeos.theme') as Theme | null) ?? 'system'
const storedDensity = (localStorage.getItem('homeos.density') as Density | null) ?? 'cozy'
const storedAccent = (localStorage.getItem('homeos.accent') as Accent | null) ?? 'default'
const storedRail = localStorage.getItem('homeos.rail') === 'collapsed'
applyTheme(storedTheme)
applyDensity(storedDensity)
applyAccent(storedAccent)

/** Ephemeral UI state only (never server data — that lives in TanStack Query). */
export const useUiStore = create<UiState>((set, get) => ({
  theme: storedTheme,
  density: storedDensity,
  accent: storedAccent,
  railCollapsed: storedRail,
  setTheme: (theme) => {
    applyTheme(theme)
    localStorage.setItem('homeos.theme', theme)
    set({ theme })
  },
  setDensity: (density) => {
    applyDensity(density)
    localStorage.setItem('homeos.density', density)
    set({ density })
  },
  setAccent: (accent) => {
    applyAccent(accent)
    localStorage.setItem('homeos.accent', accent)
    set({ accent })
  },
  toggleRail: () => {
    const next = !get().railCollapsed
    localStorage.setItem('homeos.rail', next ? 'collapsed' : 'open')
    set({ railCollapsed: next })
  },
}))
