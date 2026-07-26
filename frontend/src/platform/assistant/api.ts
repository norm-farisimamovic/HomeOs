import { api } from '@/platform/api/client'

export interface AssistantChatMessage { role: 'user' | 'assistant'; text: string }
export interface AssistantReply { configured: boolean; text: string; actions: string[] }

export const assistantApi = {
  status: () => api.get<{ configured: boolean }>('/api/assistant/status'),
  chat: (messages: AssistantChatMessage[]) => api.post<AssistantReply>('/api/assistant/chat', { messages }),
}
