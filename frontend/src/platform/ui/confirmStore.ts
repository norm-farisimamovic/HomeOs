import { create } from 'zustand'

export interface ConfirmOptions {
  title: string
  message?: string
  confirmLabel?: string
  cancelLabel?: string
  /** Red, destructive styling for the confirm button (delete etc.). */
  danger?: boolean
}

interface Pending extends ConfirmOptions {
  resolve: (ok: boolean) => void
}

interface ConfirmState {
  current: Pending | null
  ask: (options: ConfirmOptions) => Promise<boolean>
  resolve: (ok: boolean) => void
}

/** Backs the single global confirm dialog. */
export const useConfirmStore = create<ConfirmState>((set, get) => ({
  current: null,
  ask: (options) => new Promise<boolean>((resolve) => set({ current: { ...options, resolve } })),
  resolve: (ok) => {
    const c = get().current
    if (c) {
      c.resolve(ok)
      set({ current: null })
    }
  },
}))

/**
 * Ask the user to confirm an action; resolves true/false. Use before every destructive action (delete)
 * and before marking something complete. `await confirm({ … })` reads naturally at the call site.
 */
export const confirm = (options: ConfirmOptions) => useConfirmStore.getState().ask(options)
