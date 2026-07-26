import { useEffect } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from '@/platform/ui/toastStore'
import { playPing } from '@/platform/ui/sound'
import { useMe } from '@/platform/auth/useAuth'
import { notificationKeys } from './api'

/**
 * Opens the SignalR connection for live notifications. On a pushed `notify`, refreshes the bell feed
 * and shows a toast. Mount once inside the authenticated shell. Failure is non-fatal — the feed still
 * refreshes on navigation and via query refetch.
 */
export function useNotificationsRealtime() {
  const qc = useQueryClient()
  const { data: me } = useMe()
  const myId = me?.id

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/notifications')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('notify', (payload: { title?: string }) => {
      void qc.invalidateQueries({ queryKey: notificationKeys.feed })
      if (payload?.title) { toast.info(payload.title); playPing() }
    })

    // A household member changed data anywhere → refetch everything the current screen is showing
    // (dashboard feed, task lists, the board, finance…). Cause-and-effect across screens.
    connection.on('changed', () => {
      void qc.invalidateQueries()
    })

    // A new household chat message → refresh the chat stream, and ping/toast if it's from someone else.
    connection.on('chatMessage', (payload: { senderId?: string; senderName?: string; text?: string }) => {
      void qc.invalidateQueries({ queryKey: ['chat'] })
      if (payload?.senderId && payload.senderId !== myId) {
        playPing()
        if (!window.location.pathname.startsWith('/chat')) toast.info(`${payload.senderName ?? ''}: ${payload.text ?? ''}`.trim())
      }
    })

    connection.start().catch(() => { /* ignore — realtime is an enhancement, not required */ })

    return () => { void connection.stop() }
  }, [qc, myId])
}
