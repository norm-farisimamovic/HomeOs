import { api } from '@/platform/api/client'

/** A reminder as returned by the API. */
export interface Reminder {
  id: string
  title: string
  remindOn: string
  remindAt: string | null
  notes: string | null
  forMemberId: string
  forMemberName: string | null
  visibility: 'Private' | 'Household' | 'Shared'
  recurrence: 'None' | 'Daily' | 'Weekly' | 'Monthly' | 'Yearly'
  isDone: boolean
  isOverdue: boolean
  ownerId: string
  canEdit: boolean
}

export interface ReminderInput {
  title: string
  remindOn: string
  remindAt?: string | null
  notes?: string
  forMemberId?: string | null
  visibility?: string
  recurrence?: string
}

export const reminderKeys = {
  all: ['reminders'] as const,
}

export const remindersApi = {
  list: () => api.get<Reminder[]>('/api/reminders'),
  create: (input: ReminderInput) => api.post<Reminder>('/api/reminders', input),
  update: (id: string, input: ReminderInput) => api.put<Reminder>(`/api/reminders/${id}`, input),
  toggle: (id: string) => api.post<Reminder>(`/api/reminders/${id}/toggle`),
  remove: (id: string) => api.del<void>(`/api/reminders/${id}`),
}
