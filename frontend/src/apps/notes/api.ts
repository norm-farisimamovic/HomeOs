import { api } from '@/platform/api/client'

/** A note as returned by the API. */
export interface Note {
  id: string
  title: string
  content: string
  tags: string[]
  pinned: boolean
  visibility: 'Private' | 'Household' | 'Shared'
  sharedWith: string[]
  entryDate: string | null
  updatedAt: string
  ownerId: string
  canEdit: boolean
}

export interface NoteInput {
  title: string
  content?: string
  tags?: string[]
  visibility?: string
  sharedWith?: string[]
  entryDate?: string | null
}

export const noteKeys = {
  all: ['notes'] as const,
}

export const notesApi = {
  list: () => api.get<Note[]>('/api/notes'),
  create: (input: NoteInput) => api.post<Note>('/api/notes', input),
  update: (id: string, input: NoteInput) => api.put<Note>(`/api/notes/${id}`, input),
  pin: (id: string, pinned: boolean) => api.post<Note>(`/api/notes/${id}/pin`, { pinned }),
  remove: (id: string) => api.del<void>(`/api/notes/${id}`),
}
