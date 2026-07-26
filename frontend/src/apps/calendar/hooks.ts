import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { calendarApi, calendarKeys, type EventInput } from './api'

export function useMonthFeed(year: number, month: number) {
  return useQuery({ queryKey: calendarKeys.month(year, month), queryFn: () => calendarApi.month(year, month) })
}

export function useUpcomingEvents() {
  return useQuery({ queryKey: calendarKeys.events, queryFn: calendarApi.events })
}

function refresh(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: ['calendar'] })
}

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useCreateEvent() {
  const qc = useQueryClient()
  // Error surfaces inline in the modal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: EventInput) => calendarApi.create(input),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('calendar.toast.created')) },
  })
}

export function useUpdateEvent() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: EventInput }) => calendarApi.update(id, input),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('calendar.toast.updated')) },
  })
}

export function useDeleteEvent() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => calendarApi.remove(id),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('calendar.toast.deleted')) },
    onError: toastError,
  })
}
