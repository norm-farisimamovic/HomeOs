import { api } from '@/platform/api/client'

export const TRIGGERS = ['task.completed', 'bill.added', 'event.scheduled', 'chat.message'] as const
export const ACTIONS = ['notify'] as const

export interface Automation {
  id: string
  name: string
  trigger: string
  action: string
  message: string | null
  enabled: boolean
  ownerId: string
  canEdit: boolean
}

export interface AutomationInput {
  name: string
  trigger: string
  action: string
  message?: string
  enabled?: boolean
}

export const automationKeys = { all: ['automations'] as const }

export const automationsApi = {
  list: () => api.get<Automation[]>('/api/automations'),
  create: (input: AutomationInput) => api.post<Automation>('/api/automations', input),
  update: (id: string, input: AutomationInput) => api.put<Automation>(`/api/automations/${id}`, input),
  remove: (id: string) => api.del<void>(`/api/automations/${id}`),
}
