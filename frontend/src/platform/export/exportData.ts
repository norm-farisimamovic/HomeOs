import { api } from '@/platform/api/client'

/** The app data sets included in an export. Each is fetched independently; a disabled app just yields null. */
const SOURCES: Record<string, string> = {
  tasks: '/api/tasks',
  boards: '/api/tasks/boards',
  notes: '/api/notes',
  reminders: '/api/reminders',
  calendar: '/api/calendar/events',
  transactions: '/api/finance/transactions',
  bills: '/api/finance/bills',
  budgets: '/api/finance/budgets',
  life: '/api/life',
  shopping: '/api/shopping/lists',
}

export interface ExportBundle {
  exportedAt: string
  household: unknown
  data: Record<string, unknown>
}

/**
 * Gathers the household's data from every app the household has enabled into one JSON bundle.
 * Runs client-side against the same authorized endpoints the UI uses — disabled apps (403/404) are
 * simply skipped, so the export always reflects exactly what this member is allowed to see.
 */
export async function buildExport(household: unknown): Promise<ExportBundle> {
  const data: Record<string, unknown> = {}
  await Promise.all(
    Object.entries(SOURCES).map(async ([key, path]) => {
      try {
        data[key] = await api.get(path)
      } catch {
        // App disabled or not permitted — leave it out of the bundle.
      }
    }),
  )
  return { exportedAt: new Date().toISOString(), household, data }
}

/** Triggers a browser download of the given bundle as a pretty-printed JSON file. */
export function downloadJson(bundle: ExportBundle) {
  const blob = new Blob([JSON.stringify(bundle, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `homeos-export-${new Date().toISOString().slice(0, 10)}.json`
  a.click()
  URL.revokeObjectURL(url)
}
