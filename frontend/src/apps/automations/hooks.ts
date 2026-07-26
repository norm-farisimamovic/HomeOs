import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { type AutomationInput, automationKeys, automationsApi } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useAutomations() {
  return useQuery({ queryKey: automationKeys.all, queryFn: automationsApi.list })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: automationKeys.all })
}

export function useCreateAutomation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: AutomationInput) => automationsApi.create(input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('automations.toast.created')) },
  })
}

export function useUpdateAutomation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: AutomationInput }) => automationsApi.update(id, input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('automations.toast.updated')) },
    onError: toastError,
  })
}

export function useDeleteAutomation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => automationsApi.remove(id),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('automations.toast.deleted')) },
    onError: toastError,
  })
}
