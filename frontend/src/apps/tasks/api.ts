import { api } from '@/platform/api/client'

/** A task, as returned by the API. */
export interface Task {
  id: string
  title: string
  description: string | null
  dueDate: string | null
  assigneeId: string | null
  assigneeName: string | null
  priority: 'Low' | 'Normal' | 'High'
  status: 'Todo' | 'Doing' | 'Done'
  isDone: boolean
  isOverdue: boolean
  tags: string[]
  visibility: 'Private' | 'Household' | 'Shared'
  recurrence: 'None' | 'Daily' | 'Weekly' | 'Monthly' | 'Yearly'
  parentId: string | null
  subtaskDone: number
  subtaskTotal: number
  boardId: string | null
  ownerId: string
  canEdit: boolean
  canDelete: boolean
}

export interface Board { id: string; name: string; color: string }
export interface BoardInput { name: string; color?: string }

/** Dashboard counts. */
export interface TasksSummary {
  dueToday: number
  overdue: number
  openTotal: number
  doneTotal: number
}

/** Create / update payload. */
export interface TaskInput {
  title: string
  description?: string
  dueDate?: string | null
  assigneeId?: string | null
  priority?: string
  tags?: string[]
  visibility?: string
  recurrence?: string
  parentId?: string | null
  boardId?: string | null
}

/** Query keys — a contract other apps (Calendar/Kanban) reuse. */
export const taskKeys = {
  all: ['tasks'] as const,
  summary: ['tasks', 'summary'] as const,
  boards: ['tasks', 'boards'] as const,
}

export const boardsApi = {
  list: () => api.get<Board[]>('/api/tasks/boards'),
  create: (input: BoardInput) => api.post<Board>('/api/tasks/boards', input),
  remove: (id: string) => api.del<void>(`/api/tasks/boards/${id}`),
}

export const tasksApi = {
  list: () => api.get<Task[]>('/api/tasks'),
  summary: () => api.get<TasksSummary>('/api/tasks/summary'),
  create: (input: TaskInput) => api.post<Task>('/api/tasks', input),
  update: (id: string, input: TaskInput) => api.put<Task>(`/api/tasks/${id}`, input),
  toggle: (id: string) => api.post<Task>(`/api/tasks/${id}/toggle`),
  setStatus: (id: string, status: string) => api.post<Task>(`/api/tasks/${id}/status`, { status }),
  remove: (id: string) => api.del<void>(`/api/tasks/${id}`),
}
