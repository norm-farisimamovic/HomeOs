import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { type NoteInput, noteKeys, notesApi } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useNotes() {
  return useQuery({ queryKey: noteKeys.all, queryFn: notesApi.list })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: noteKeys.all })
}

export function useCreateNote() {
  const qc = useQueryClient()
  // Error surfaces inline in the modal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: NoteInput) => notesApi.create(input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('notes.toast.created')) },
  })
}

export function useUpdateNote() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: NoteInput }) => notesApi.update(id, input),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('notes.toast.updated')) },
  })
}

export function usePinNote() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, pinned }: { id: string; pinned: boolean }) => notesApi.pin(id, pinned),
    onSuccess: () => invalidate(qc),
    onError: toastError,
  })
}

export function useDeleteNote() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => notesApi.remove(id),
    onSuccess: () => { invalidate(qc); toast.success(i18n.t('notes.toast.deleted')) },
    onError: toastError,
  })
}
