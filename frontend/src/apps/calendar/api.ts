import { api } from '@/platform/api/client'

/** One dated item on the month feed — could be an event, a task due date, or a bill. */
export interface FeedItem {
  source: 'calendar' | 'tasks' | 'finance' | 'reminders' | 'life'
  id: string
  title: string
  date: string
  kind: 'event' | 'task' | 'bill' | 'reminder' | 'renewal'
  time: string | null
  isDone: boolean
}

export interface MonthFeed {
  year: number
  month: number
  items: FeedItem[]
}

/** A calendar event (the Calendar app's own object). */
export interface CalendarEvent {
  id: string
  title: string
  startsOn: string
  startTime: string | null
  location: string | null
  notes: string | null
  visibility: 'Private' | 'Household' | 'Shared'
  ownerId: string
  canEdit: boolean
  sharedWith: string[]
}

export interface EventInput {
  title: string
  startsOn: string
  startTime?: string | null
  location?: string
  notes?: string
  visibility?: string
  sharedWith?: string[]
}

export const calendarKeys = {
  month: (y: number, m: number) => ['calendar', 'month', y, m] as const,
  events: ['calendar', 'events'] as const,
}

export const calendarApi = {
  month: (year: number, month: number) => api.get<MonthFeed>(`/api/calendar/month?year=${year}&month=${month}`),
  events: () => api.get<CalendarEvent[]>('/api/calendar/events'),
  create: (input: EventInput) => api.post<CalendarEvent>('/api/calendar/events', input),
  update: (id: string, input: EventInput) => api.put<CalendarEvent>(`/api/calendar/events/${id}`, input),
  remove: (id: string) => api.del<void>(`/api/calendar/events/${id}`),
}
