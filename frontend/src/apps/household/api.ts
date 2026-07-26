import { api } from '@/platform/api/client'

/** A household member (management view). */
export interface HouseholdMember {
  id: string
  firstName: string
  lastName: string
  displayName: string
  email: string
  role: string
  isYou: boolean
}

export interface UpdateMemberInput { firstName: string; lastName: string; email?: string }

/** A pending invitation. */
export interface Invite {
  id: string
  email: string
  displayName: string
  role: string
  createdAtUtc: string
}

export interface InviteInput { email: string; firstName: string; lastName: string; role: string }

export const householdKeys = {
  members: ['household', 'members'] as const,
  invites: ['household', 'invites'] as const,
}

export const householdApi = {
  members: () => api.get<HouseholdMember[]>('/api/members'),
  invites: () => api.get<Invite[]>('/api/members/invites'),
  invite: (input: InviteInput) => api.post<Invite>('/api/members/invite', input),
  cancelInvite: (id: string) => api.del<void>(`/api/members/invites/${id}`),
  changeRole: (id: string, role: string) => api.put<void>(`/api/members/${id}/role`, { role }),
  update: (id: string, input: UpdateMemberInput) => api.put<void>(`/api/members/${id}`, input),
  remove: (id: string) => api.del<void>(`/api/members/${id}`),
  rename: (name: string) => api.put<{ name: string }>('/api/members/household', { name }),
}

/** Household roles, highest privilege first. */
export const ROLES = ['Owner', 'Admin', 'Adult', 'Child', 'Guest'] as const
