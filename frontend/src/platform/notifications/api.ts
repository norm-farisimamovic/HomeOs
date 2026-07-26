import { api } from '@/platform/api/client'

export interface NotificationItem {
  id: string
  category: string
  title: string
  body: string | null
  link: string | null
  isRead: boolean
  createdAt: string
}

export interface NotificationsResponse {
  unread: number
  items: NotificationItem[]
}

export interface NotificationPref {
  category: string
  email: boolean
}

export const notificationKeys = {
  feed: ['notifications', 'feed'] as const,
  prefs: ['notifications', 'prefs'] as const,
}

export const notificationsApi = {
  feed: () => api.get<NotificationsResponse>('/api/notifications'),
  markRead: (id: string) => api.post<void>(`/api/notifications/${id}/read`),
  markAll: () => api.post<void>('/api/notifications/read-all'),
  prefs: () => api.get<NotificationPref[]>('/api/notifications/preferences'),
  savePrefs: (prefs: NotificationPref[]) => api.put<void>('/api/notifications/preferences', prefs),
}
