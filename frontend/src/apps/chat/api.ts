import { api } from '@/platform/api/client'

export interface ChatMessage {
  id: string
  senderId: string
  senderName: string | null
  text: string
  sentAt: string
  mine: boolean
}

/** Messages from the assistant come back with an all-zero sender id. */
export const ASSISTANT_ID = '00000000-0000-0000-0000-000000000000'

export const chatKeys = { all: ['chat'] as const }

export const chatApi = {
  list: () => api.get<ChatMessage[]>('/api/chat'),
  send: (text: string) => api.post<ChatMessage>('/api/chat', { text }),
  toReminder: (id: string) => api.post<{ date: string }>(`/api/chat/${id}/reminder`, {}),
}
