import type { ComponentType, CSSProperties, ReactNode } from 'react'
import { useEffect } from 'react'
import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'

/** Reusable modal shell — backdrop, header (icon/title/subtitle/close), body, optional footer. Esc closes. */
export function Modal({ title, subtitle, icon: Icon, hue, size, onClose, footer, children }: {
  title: string
  subtitle?: string
  icon?: ComponentType<{ size?: number }>
  hue?: string
  size?: 'sm'
  onClose: () => void
  footer?: ReactNode
  children: ReactNode
}) {
  const { t } = useTranslation()

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const style = hue ? ({ ['--mc' as string]: hue } as CSSProperties) : undefined

  return (
    <div className="veil" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}>
      <div className={`modal${size ? ` ${size}` : ''}`} style={style} role="dialog" aria-modal="true">
        <div className="modal-h">
          {Icon && <span className="ico"><Icon size={17} /></span>}
          <div className="tt">
            <h3>{title}</h3>
            {subtitle && <div className="st">{subtitle}</div>}
          </div>
          <button className="btn ghost icon sm" type="button" onClick={onClose} aria-label={t('common.cancel')}><X size={16} /></button>
        </div>
        <div className="modal-b">{children}</div>
        {footer && <div className="modal-f">{footer}</div>}
      </div>
    </div>
  )
}
