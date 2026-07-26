import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Send, Sparkles } from 'lucide-react'
import { assistantApi, type AssistantChatMessage } from '@/platform/assistant/api'

/**
 * Ask-anything box on the dashboard. Sends the conversation to the backend assistant (Claude tool-use over
 * the app's kernel contracts), which can answer "what's coming up" and take actions like scheduling reminders.
 */
export function AssistantWidget() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { data: status } = useQuery({ queryKey: ['assistant', 'status'], queryFn: assistantApi.status })
  const [messages, setMessages] = useState<AssistantChatMessage[]>([])
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)

  if (status && !status.configured) {
    return (
      <div className="card assistant">
        <div className="card-h"><div className="t"><i className="mdot" style={{ ['--mc' as string]: 'var(--brand)' }} /><h3>{t('assistant.title')}</h3></div></div>
        <div className="card-b"><p className="hint">{t('assistant.notConfigured')}</p></div>
      </div>
    )
  }

  const send = async () => {
    const text = input.trim()
    if (!text || busy) return
    const next = [...messages, { role: 'user' as const, text }]
    setMessages(next)
    setInput('')
    setBusy(true)
    try {
      const reply = await assistantApi.chat(next)
      setMessages([...next, { role: 'assistant', text: reply.text || t('assistant.done') }])
      // Actions may have changed data across apps → refresh the dashboard's queries.
      if (reply.actions.length > 0) void qc.invalidateQueries()
    } catch {
      setMessages([...next, { role: 'assistant', text: t('common.error') }])
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card assistant">
      <div className="card-h"><div className="t"><span className="empty-ico sm" style={{ ['--mc' as string]: 'var(--brand)' }}><Sparkles size={15} /></span><h3>{t('assistant.title')}</h3></div></div>
      <div className="card-b">
        {messages.length === 0 && (
          <div className="assistant-hints">
            <p className="hint">{t('assistant.hint')}</p>
            {[t('assistant.ex1'), t('assistant.ex2')].map((ex) => (
              <button key={ex} type="button" className="chip" onClick={() => setInput(ex)}>{ex}</button>
            ))}
          </div>
        )}
        {messages.length > 0 && (
          <div className="assistant-thread">
            {messages.map((m, i) => (
              <div key={i} className={`a-msg ${m.role}`}>{m.text}</div>
            ))}
            {busy && <div className="a-msg assistant a-typing">{t('assistant.thinking')}</div>}
          </div>
        )}
        <div className="assistant-input">
          <input className="inp" value={input} onChange={(e) => setInput(e.target.value)} placeholder={t('assistant.placeholder')}
            onKeyDown={(e) => { if (e.key === 'Enter') void send() }} disabled={busy} />
          <button className="btn primary icon" type="button" onClick={() => void send()} disabled={busy || !input.trim()} aria-label={t('assistant.send')}><Send size={15} /></button>
        </div>
      </div>
    </div>
  )
}
