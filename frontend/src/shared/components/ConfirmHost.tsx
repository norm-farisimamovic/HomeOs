import { AlertTriangle, HelpCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Modal } from '@/shared/components/Modal'
import { useConfirmStore } from '@/platform/ui/confirmStore'

/** Renders the single global confirm dialog when a `confirm({ … })` call is pending. Mount once at root. */
export function ConfirmHost() {
  const { t } = useTranslation()
  const current = useConfirmStore((s) => s.current)
  const resolve = useConfirmStore((s) => s.resolve)

  if (!current) return null

  const danger = current.danger ?? false

  return (
    <Modal
      title={current.title}
      size="sm"
      icon={danger ? AlertTriangle : HelpCircle}
      hue={danger ? 'var(--danger)' : undefined}
      onClose={() => resolve(false)}
      footer={
        <>
          <div className="spacer" />
          <button className="btn" type="button" onClick={() => resolve(false)}>
            {current.cancelLabel ?? t('common.cancel')}
          </button>
          <button className={`btn ${danger ? 'danger-solid' : 'primary'}`} type="button" autoFocus onClick={() => resolve(true)}>
            {current.confirmLabel ?? t('common.confirm')}
          </button>
        </>
      }
    >
      {current.message && <p className="confirm-msg">{current.message}</p>}
    </Modal>
  )
}
