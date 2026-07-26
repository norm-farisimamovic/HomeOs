import { api } from '@/platform/api/client'

export const LIFE_CATEGORIES = ['Document', 'Warranty', 'Insurance', 'Subscription', 'Contact', 'Other'] as const
export type LifeCategory = (typeof LIFE_CATEGORIES)[number]

/** A life-admin record as returned by the API. */
export interface LifeRecord {
  id: string
  title: string
  category: LifeCategory
  expiresOn: string | null
  daysToExpiry: number | null
  provider: string | null
  notes: string | null
  visibility: 'Private' | 'Household' | 'Shared'
  ownerId: string
  canEdit: boolean
}

export interface LifeRecordInput {
  title: string
  category?: string
  expiresOn?: string | null
  provider?: string
  notes?: string
  visibility?: string
}

export const lifeKeys = {
  all: ['life'] as const,
}

export const lifeApi = {
  list: () => api.get<LifeRecord[]>('/api/life'),
  create: (input: LifeRecordInput) => api.post<LifeRecord>('/api/life', input),
  update: (id: string, input: LifeRecordInput) => api.put<LifeRecord>(`/api/life/${id}`, input),
  remove: (id: string) => api.del<void>(`/api/life/${id}`),
}
