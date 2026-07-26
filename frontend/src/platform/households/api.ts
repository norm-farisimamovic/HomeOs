import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/platform/api/client'

export interface SwitchableHousehold {
  householdId: string
  householdName: string
  memberId: string
  roles: string
  current: boolean
}

export const householdsKeys = { switchable: ['households', 'switchable'] as const }

export function useSwitchableHouseholds() {
  return useQuery({ queryKey: householdsKeys.switchable, queryFn: () => api.get<SwitchableHousehold[]>('/api/households/switchable') })
}

/** Switch the session to another of the person's households, then refresh everything. */
export function useSwitchHousehold() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (householdId: string) => api.post<{ householdId: string }>('/api/households/switch', { householdId }),
    onSuccess: async () => { await qc.invalidateQueries() },
  })
}

/** Create a new household owned by the current person (does not switch to it). */
export function useCreateHousehold() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => api.post<{ householdId: string }>('/api/households', { name }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: householdsKeys.switchable }),
  })
}
