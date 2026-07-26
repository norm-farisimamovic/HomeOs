import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertTriangle } from 'lucide-react'
import i18n from '@/platform/i18n'

interface Props {
  children: ReactNode
  /** Shown as the boundary label (e.g. which surface failed). Defaults to the whole app. */
  scope?: string
}
interface State {
  error: Error | null
}

/**
 * Catches render/runtime errors in its subtree so one crash never blanks the whole product. Shows a
 * friendly, localized fallback with a reset. Mounted at the app root and again around each route's
 * content, so an app failure is contained to that panel.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Log for diagnostics; a real deployment would ship this to an error tracker.
    console.error('UI error boundary caught an error:', error, info.componentStack)
  }

  private reset = () => this.setState({ error: null })

  render(): ReactNode {
    if (!this.state.error) return this.props.children

    return (
      <div className="err-boundary">
        <div className="card card-pad" style={{ maxWidth: 460, textAlign: 'center', margin: '0 auto' }}>
          <span className="empty-ico" style={{ ['--mc' as string]: 'var(--danger)' }}><AlertTriangle size={22} /></span>
          <h2 style={{ marginTop: 10 }}>{i18n.t('common.crashTitle')}</h2>
          <p className="hint" style={{ marginTop: 6 }}>{i18n.t('common.crashSub')}</p>
          <div className="btn-row" style={{ justifyContent: 'center', marginTop: 14 }}>
            <button className="btn" type="button" onClick={this.reset}>{i18n.t('common.retry')}</button>
            <button className="btn primary" type="button" onClick={() => window.location.reload()}>{i18n.t('common.reload')}</button>
          </div>
        </div>
      </div>
    )
  }
}
