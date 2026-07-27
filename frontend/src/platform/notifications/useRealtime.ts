import { useEffect, useRef } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from '@/platform/ui/toastStore'
import { playPing } from '@/platform/ui/sound'
import { useMe } from '@/platform/auth/useAuth'
import { notificationKeys } from './api'

/**
 * Opens ONE SignalR connection for live updates and keeps it for the shell's lifetime. Mount once inside the
 * authenticated shell. The current member id is read through a ref so the connection is never torn down and
 * re-opened when `me` loads — re-opening used to leave two live connections briefly, which double-fired every
 * notification. Failure is non-fatal — the feed still refreshes on navigation and via query refetch.
 */
export function useNotificationsRealtime() {
  const qc = useQueryClient()
  const { data: me } = useMe()
  const myIdRef = useRef<string | undefined>(undefined)
  myIdRef.current = me?.id

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

    // A household member changed data anywhere → refetch everything the current screen is showing.
    connection.on('changed', () => {
      void qc.invalidateQueries()
    })

    // A new household chat message → refresh the stream, and ping/toast if it's from someone else.
    connection.on('chatMessage', (payload: { senderId?: string; senderName?: string; text?: string }) => {
      void qc.invalidateQueries({ queryKey: ['chat'] })
      if (payload?.senderId && payload.senderId !== myIdRef.current) {
        playPing()
        if (!window.location.pathname.startsWith('/chat')) toast.info(`${payload.senderName ?? ''}: ${payload.text ?? ''}`.trim())
      }
    })

    connection.start().catch(() => { /* ignore — realtime is an enhancement, not required */ })

    return () => { void connection.stop() }
  }, [qc])
}
