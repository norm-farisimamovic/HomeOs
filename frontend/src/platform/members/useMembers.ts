import { useQuery } from '@tanstack/react-query'
import { api } from '@/platform/api/client'

/** A household member (from the platform's /api/members). */
export interface Member {
  id: string
  displayName: string
  email: string
}

/** Household members — reused by assignee pickers, avatars, the Household app. */
export function useMembers() {
  return useQuery({
    queryKey: ['members'],
    queryFn: () => api.get<Member[]>('/api/members'),
    staleTime: 5 * 60_000,
  })
}
