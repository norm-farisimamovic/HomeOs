import { AlertTriangle, CheckCircle2, Info, X } from 'lucide-react'
import { useToastStore } from '@/platform/ui/toastStore'

const icons = { success: CheckCircle2, error: AlertTriangle, info: Info } as const

/** Renders the global toast queue (bottom-right). Mount once at the app root. */
export function Toaster() {
  const toasts = useToastStore((s) => s.toasts)
  const dismiss = useToastStore((s) => s.dismiss)

  if (toasts.length === 0) return null

  return (
    <div className="toaster" aria-live="polite" aria-atomic="false">
      {toasts.map((t) => {
        const Icon = icons[t.kind]
        return (
          <div key={t.id} className={`toast ${t.kind}`} role="status">
            <Icon size={17} className="ti" />
            <span className="tm">{t.message}</span>
            <button className="tx" type="button" onClick={() => dismiss(t.id)} aria-label="Close">
              <X size={14} />
            </button>
          </div>
        )
      })}
    </div>
  )
}
