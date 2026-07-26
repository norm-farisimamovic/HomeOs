import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { type NotificationPref, notificationKeys, notificationsApi } from './api'

/** The bell feed (unread count + recent items). Also kept fresh by the SignalR listener. */
export function useNotifications() {
  return useQuery({ queryKey: notificationKeys.feed, queryFn: notificationsApi.feed })
}

export function useMarkNotificationRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKeys.feed }),
  })
}

export function useMarkAllRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => notificationsApi.markAll(),
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKeys.feed }),
  })
}

export function useNotificationPrefs() {
  return useQuery({ queryKey: notificationKeys.prefs, queryFn: notificationsApi.prefs })
}

export function useSaveNotificationPrefs() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (prefs: NotificationPref[]) => notificationsApi.savePrefs(prefs),
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKeys.prefs }),
  })
}
