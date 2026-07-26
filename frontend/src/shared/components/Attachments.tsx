import { useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Download, FileText, Image as ImageIcon, Paperclip, Plus, X } from 'lucide-react'
import { attachmentDownloadUrl, useAttachments, useDeleteAttachment, useUploadAttachment } from '@/platform/attachments/api'
import { toast } from '@/platform/ui/toastStore'

/** Human-readable file size. */
function humanSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

/**
 * Kernel "attach a file" surface — reusable across apps. Drop it into any detail view with the object's
 * type + id (e.g. <c>task</c>, <c>bill</c>, <c>life</c>). Lists, uploads, downloads and removes files.
 */
export function Attachments({ ownerType, ownerId }: { ownerType: string; ownerId: string }) {
  const { t } = useTranslation()
  const { data: files = [] } = useAttachments(ownerType, ownerId)
  const upload = useUploadAttachment(ownerType, ownerId)
  const del = useDeleteAttachment(ownerType, ownerId)
  const inputRef = useRef<HTMLInputElement>(null)

  const onPick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    upload.mutate(file, { onError: (err) => toast.error(err instanceof Error ? err.message : t('common.error')) })
  }

  return (
    <div className="linked">
      <div className="linked-h"><Paperclip size={13} /><span>{t('attachments.title')}</span></div>
      <div className="attach-list">
        {files.map((f) => {
          const isImg = f.contentType.startsWith('image/')
          return (
            <div className="attach-row" key={f.id}>
              <span className="attach-ic">{isImg ? <ImageIcon size={14} /> : <FileText size={14} />}</span>
              <a className="attach-nm" href={attachmentDownloadUrl(f.id)} target="_blank" rel="noreferrer" title={f.fileName}>{f.fileName}</a>
              <span className="attach-sz">{humanSize(f.size)}</span>
              <a className="attach-btn" href={attachmentDownloadUrl(f.id)} download={f.fileName} aria-label={t('common.download')}><Download size={13} /></a>
              <button type="button" className="attach-btn" onClick={() => del.mutate(f.id)} aria-label={t('common.delete')}><X size={13} /></button>
            </div>
          )
        })}
      </div>
      <input ref={inputRef} type="file" hidden onChange={onPick} />
      <button type="button" className="link-chip add" onClick={() => inputRef.current?.click()} disabled={upload.isPending}>
        <Plus size={12} />{upload.isPending ? t('common.loading') : t('attachments.add')}
      </button>
    </div>
  )
}
