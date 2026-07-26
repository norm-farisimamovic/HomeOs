import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { meQueryKey } from '@/platform/auth/useAuth'
import { householdApi, householdKeys, type InviteInput, type UpdateMemberInput } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useRenameHousehold() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => householdApi.rename(name),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: meQueryKey }); toast.success(i18n.t('household.renamed')) },
    onError: toastError,
  })
}

export function useHouseholdMembers() {
  return useQuery({ queryKey: householdKeys.members, queryFn: householdApi.members })
}

export function useInvites(enabled: boolean) {
  return useQuery({ queryKey: householdKeys.invites, queryFn: householdApi.invites, enabled })
}

function refresh(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: householdKeys.members })
  void qc.invalidateQueries({ queryKey: householdKeys.invites })
}

export function useInviteMember() {
  const qc = useQueryClient()
  // Error surfaces inline in InviteModal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: InviteInput) => householdApi.invite(input),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('household.toast.invited')) },
  })
}

export function useCancelInvite() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => householdApi.cancelInvite(id),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('household.toast.inviteCancelled')) },
    onError: toastError,
  })
}

export function useChangeRole() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, role }: { id: string; role: string }) => householdApi.changeRole(id, role),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('household.toast.roleChanged')) },
    onError: toastError,
  })
}

export function useRemoveMember() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => householdApi.remove(id),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('household.toast.memberRemoved')) },
    onError: toastError,
  })
}

export function useUpdateMember() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateMemberInput }) => householdApi.update(id, input),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('household.toast.memberUpdated')) },
    onError: toastError,
  })
}
