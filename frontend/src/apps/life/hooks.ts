import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { lifeApi, lifeKeys, type LifeRecordInput } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useLifeRecords() {
  return useQuery({ queryKey: lifeKeys.all, queryFn: lifeApi.list })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: lifeKeys.all })
  // A record's expiry auto-creates/updates a reminder, which also shows on the calendar.
  void qc.invalidateQueries({ queryKey: ['reminders'] })
  void qc.invalidateQueries({ queryKey: ['calendar'] })
}

export function useCreateLifeRecord() {
  const qc = useQueryClient()
  // Error surfaces inline in the modal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: LifeRecordInput) => lifeApi.create(input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('life.toast.created')) },
  })
}

export function useUpdateLifeRecord() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: LifeRecordInput }) => lifeApi.update(id, input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('life.toast.updated')) },
  })
}

export function useDeleteLifeRecord() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => lifeApi.remove(id),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('life.toast.deleted')) },
    onError: toastError,
  })
}
