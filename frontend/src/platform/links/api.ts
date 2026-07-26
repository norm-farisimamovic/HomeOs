import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/platform/api/client'

/** A link from one app object to another (the "connected web"). */
export interface EntityLink {
  id: string
  toType: string
  toId: string
  toTitle: string
  toLink: string
}

export interface CreateLink {
  fromType: string
  fromId: string
  toType: string
  toId: string
  toTitle: string
  toLink: string
}

export const linksApi = {
  list: (fromType: string, fromId: string) =>
    api.get<EntityLink[]>(`/api/links?fromType=${encodeURIComponent(fromType)}&fromId=${fromId}`),
  create: (input: CreateLink) => api.post<EntityLink>('/api/links', input),
  remove: (id: string) => api.del<void>(`/api/links/${id}`),
}

/** Links from a given object. */
export function useLinks(fromType: string, fromId: string | undefined) {
  return useQuery({
    queryKey: ['links', fromType, fromId],
    queryFn: () => linksApi.list(fromType, fromId!),
    enabled: !!fromId,
  })
}

export function useCreateLink(fromType: string, fromId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateLink) => linksApi.create(input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['links', fromType, fromId] }),
  })
}

export function useDeleteLink(fromType: string, fromId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => linksApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['links', fromType, fromId] }),
  })
}
