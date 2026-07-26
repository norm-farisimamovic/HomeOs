import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { appsApi } from './api'

/** The household's app catalogue + state. Used by the Apps page and to filter navigation. */
export function useApps() {
  return useQuery({ queryKey: ['apps'], queryFn: appsApi.list })
}

/** Enable/disable an app for the household (Owner/Admin). */
export function useSetAppEnabled() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) => appsApi.setEnabled(id, enabled),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['apps'] }),
  })
}

/** Replace the capabilities granted to an app (Owner/Admin). */
export function useSetAppCapabilities() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, capabilities }: { id: string; capabilities: string[] }) =>
      appsApi.setCapabilities(id, capabilities),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['apps'] }),
  })
}
