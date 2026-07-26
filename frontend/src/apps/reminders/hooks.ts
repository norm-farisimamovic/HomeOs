import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { type Reminder, reminderKeys, type ReminderInput, remindersApi } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useReminders() {
  return useQuery({ queryKey: reminderKeys.all, queryFn: remindersApi.list })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: reminderKeys.all })
  // Reminders show on the calendar too.
  void qc.invalidateQueries({ queryKey: ['calendar'] })
}

export function useCreateReminder() {
  const qc = useQueryClient()
  // Error surfaces inline in the modal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: ReminderInput) => remindersApi.create(input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('reminders.toast.created')) },
  })
}

export function useUpdateReminder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: ReminderInput }) => remindersApi.update(id, input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('reminders.toast.updated')) },
  })
}

export function useToggleReminder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => remindersApi.toggle(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: reminderKeys.all })
      const previous = qc.getQueryData<Reminder[]>(reminderKeys.all)
      qc.setQueryData<Reminder[]>(reminderKeys.all, (old) => old?.map((r) => (r.id === id ? { ...r, isDone: !r.isDone } : r)))
      return { previous }
    },
    onError: (e, _id, ctx) => { if (ctx?.previous) qc.setQueryData(reminderKeys.all, ctx.previous); toastError(e) },
    onSettled: () => invalidate(qc),
  })
}

export function useDeleteReminder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => remindersApi.remove(id),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('reminders.toast.deleted')) },
    onError: toastError,
  })
}
