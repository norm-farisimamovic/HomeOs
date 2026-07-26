import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { type Board, type BoardInput, boardsApi, type Task, type TaskInput, taskKeys, tasksApi } from './api'

/** Turn any thrown error into a localized toast (the API already localizes its messages). */
function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

/** All tasks visible to the current member. */
export function useTasks() {
  return useQuery({ queryKey: taskKeys.all, queryFn: tasksApi.list })
}

/** Dashboard counts. */
export function useTasksSummary() {
  return useQuery({ queryKey: taskKeys.summary, queryFn: tasksApi.summary })
}

/** Kanban boards for the household. */
export function useBoards() {
  return useQuery({ queryKey: taskKeys.boards, queryFn: boardsApi.list })
}

export function useCreateBoard() {
  const qc = useQueryClient()
  return useMutation<Board, unknown, BoardInput>({
    mutationFn: (input) => boardsApi.create(input),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: taskKeys.boards }) },
    onError: toastError,
  })
}

export function useDeleteBoard() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => boardsApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: taskKeys.boards })
      void qc.invalidateQueries({ queryKey: taskKeys.all })
      toast.success(i18n.t('kanban.boardDeleted'))
    },
    onError: toastError,
  })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: taskKeys.all })
  void qc.invalidateQueries({ queryKey: taskKeys.summary })
}

export function useCreateTask() {
  const qc = useQueryClient()
  // Error surfaces inline in TaskModal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: TaskInput) => tasksApi.create(input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('tasks.toast.created')) },
  })
}

export function useUpdateTask() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: TaskInput }) => tasksApi.update(id, input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('tasks.toast.updated')) },
  })
}

export function useToggleTask() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => tasksApi.toggle(id),
    // Optimistic: flip isDone/status immediately, roll back on error.
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: taskKeys.all })
      const previous = qc.getQueryData<Task[]>(taskKeys.all)
      qc.setQueryData<Task[]>(taskKeys.all, (old) =>
        old?.map((t) =>
          t.id === id ? { ...t, isDone: !t.isDone, status: t.isDone ? 'Todo' : 'Done' } : t,
        ),
      )
      return { previous }
    },
    onSuccess: (task) => toast.success(i18n.t(task.isDone ? 'tasks.toast.completed' : 'tasks.toast.reopened')),
    onError: (e, _id, ctx) => {
      if (ctx?.previous) qc.setQueryData(taskKeys.all, ctx.previous)
      toastError(e)
    },
    onSettled: () => invalidate(qc),
  })
}

export function useDeleteTask() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => tasksApi.remove(id),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('tasks.toast.deleted')) },
    onError: toastError,
  })
}

/** Move a task between Kanban columns (Todo/Doing/Done). Optimistic; errors roll back + toast. */
export function useSetTaskStatus() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) => tasksApi.setStatus(id, status),
    onMutate: async ({ id, status }) => {
      await qc.cancelQueries({ queryKey: taskKeys.all })
      const previous = qc.getQueryData<Task[]>(taskKeys.all)
      qc.setQueryData<Task[]>(taskKeys.all, (old) =>
        old?.map((t) => (t.id === id ? { ...t, status: status as Task['status'], isDone: status === 'Done' } : t)),
      )
      return { previous }
    },
    onError: (e, _v, ctx) => { if (ctx?.previous) qc.setQueryData(taskKeys.all, ctx.previous); toastError(e) },
    onSettled: () => invalidate(qc),
  })
}
