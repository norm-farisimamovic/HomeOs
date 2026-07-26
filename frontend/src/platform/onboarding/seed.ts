import type { QueryClient } from '@tanstack/react-query'
import { api } from '@/platform/api/client'

/** Localized sample content for the "load examples" onboarding action. */
export interface SeedText {
  task1: string
  task2: string
  note: string
  reminder: string
  shoppingList: string
  shoppingItems: string[]
}

function todayPlus(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/**
 * Seeds a handful of example items across the core apps so a brand-new household isn't a blank slate.
 * Each call is independent and tolerant — a disabled app simply doesn't get its sample. Invalidates all
 * queries at the end so every screen reflects the new data.
 */
export async function seedExamples(text: SeedText, qc: QueryClient): Promise<void> {
  const attempts: Array<Promise<unknown>> = [
    api.post('/api/tasks', { title: text.task1, priority: 'High', dueDate: todayPlus(1), visibility: 'Household' }),
    api.post('/api/tasks', { title: text.task2, priority: 'Normal', dueDate: todayPlus(3), visibility: 'Household' }),
    api.post('/api/notes', { title: text.note, content: '' }),
    api.post('/api/reminders', { title: text.reminder, remindOn: todayPlus(2) }),
    api.post('/api/shopping/lists', { name: text.shoppingList }).then(async (list) => {
      const id = (list as { id?: string })?.id
      if (id) for (const item of text.shoppingItems) await api.post(`/api/shopping/lists/${id}/items`, { text: item })
    }),
  ]
  await Promise.allSettled(attempts)
  await qc.invalidateQueries()
}
