import { api } from '@/platform/api/client'

/** An app and its state for the current household (from `GET /api/apps`). */
export interface AppInfo {
  id: string
  nameKey: string
  descriptionKey: string
  icon: string
  hue: string
  route: string
  isCore: boolean
  enabled: boolean
  capabilities: string[]
  grantedCapabilities: string[]
}

export const appsApi = {
  list: () => api.get<AppInfo[]>('/api/apps'),
  setEnabled: (id: string, enabled: boolean) => api.put<void>(`/api/apps/${id}/enabled`, { enabled }),
  setCapabilities: (id: string, capabilities: string[]) =>
    api.put<void>(`/api/apps/${id}/capabilities`, { capabilities }),
}
