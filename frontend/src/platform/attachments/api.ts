import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/platform/api/client'

export interface Attachment {
  id: string
  fileName: string
  contentType: string
  size: number
  uploadedById: string
  uploadedAt: string
}

const key = (ownerType: string, ownerId: string) => ['attachments', ownerType, ownerId] as const

export function useAttachments(ownerType: string, ownerId: string | undefined) {
  return useQuery({
    queryKey: key(ownerType, ownerId ?? ''),
    queryFn: () => api.get<Attachment[]>(`/api/attachments?ownerType=${encodeURIComponent(ownerType)}&ownerId=${ownerId}`),
    enabled: !!ownerId,
  })
}

export function useUploadAttachment(ownerType: string, ownerId: string) {
  const qc = useQueryClient()
  return useMutation({
    // Multipart upload — bypass the JSON client so the browser sets the boundary itself.
    mutationFn: async (file: File) => {
      const form = new FormData()
      form.append('file', file)
      form.append('ownerType', ownerType)
      form.append('ownerId', ownerId)
      const res = await fetch('/api/attachments', { method: 'POST', credentials: 'include', body: form })
      if (!res.ok) {
        const body = await res.json().catch(() => null)
        throw new ApiError(res.status, body, (body as { title?: string })?.title ?? 'Upload failed.')
      }
      return (await res.json()) as Attachment
    },
    onSuccess: () => void qc.invalidateQueries({ queryKey: key(ownerType, ownerId) }),
  })
}

export function useDeleteAttachment(ownerType: string, ownerId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/api/attachments/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: key(ownerType, ownerId) }),
  })
}

export const attachmentDownloadUrl = (id: string) => `/api/attachments/${id}`
