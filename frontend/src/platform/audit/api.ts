import { api } from '@/platform/api/client'

export interface AuditEntry {
  id: string
  action: string
  detail: string
  actorName: string | null
  createdAt: string
}

export const auditApi = {
  list: () => api.get<AuditEntry[]>('/api/audit'),
}
