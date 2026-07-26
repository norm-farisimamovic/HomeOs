import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Bot, Send, Sparkles, Trash2 } from 'lucide-react'
import { assistantApi, type AssistantChatMessage } from '@/platform/assistant/api'

const THREAD_KEY = 'homeos.assistant.thread'

function loadThread(): AssistantChatMessage[] {
  try { return JSON.parse(localStorage.getItem(THREAD_KEY) ?? '[]') as AssistantChatMessage[] } catch { return [] }
}

/**
 * The private AI assistant — a personal 1:1 conversation, kept separate from the household chat. Runs as the
 * current member (its actions + answers are scoped to them), with the thread persisted locally so it survives
 * navigation. Nobody else in the household sees this.
 */
export function AssistantPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { data: status } = useQuery({ queryKey: ['assistant', 'status'], queryFn: assistantApi.status })
  const [messages, setMessages] = useState<AssistantChatMessage[]>(loadThread)
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => { localStorage.setItem(THREAD_KEY, JSON.stringify(messages)) }, [messages])
  useEffect(() => { endRef.current?.scrollIntoView({ behavior: 'smooth' }) }, [messages, busy])

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
      if (reply.actions.length > 0) void qc.invalidateQueries()
    } catch {
      setMessages([...next, { role: 'assistant', text: t('common.error') }])
    } finally {
      setBusy(false)
    }
  }

  const notConfigured = status && !status.configured

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--brand)' }}>{t('assistant.title')}</div>
          <h1>{t('assistant.pageTitle')}</h1>
          <p className="sub">{t('assistant.pageSub')}</p>
        </div>
        {messages.length > 0 && (
          <button className="btn ghost sm" type="button" onClick={() => setMessages([])}><Trash2 size={14} />{t('assistant.clear')}</button>
        )}
      </div>

      <div className="card chat-card">
        <div className="chat-thread">
          {notConfigured && (
            <div className="empty">
              <span className="empty-ico" style={{ ['--mc' as string]: 'var(--brand)' }}><Sparkles size={20} /></span>
              <h4>{t('assistant.title')}</h4><p>{t('assistant.notConfigured')}</p>
            </div>
          )}
          {!notConfigured && messages.length === 0 && (
            <div className="empty">
              <span className="empty-ico" style={{ ['--mc' as string]: 'var(--brand)' }}><Sparkles size={20} /></span>
              <h4>{t('assistant.emptyTitle')}</h4><p>{t('assistant.hint')}</p>
              <div className="assistant-hints" style={{ marginTop: 10 }}>
                {[t('assistant.ex1'), t('assistant.ex2'), t('assistant.ex3')].map((ex) => (
                  <button key={ex} type="button" className="chip" onClick={() => setInput(ex)}>{ex}</button>
                ))}
              </div>
            </div>
          )}
          {messages.map((m, i) => (
            <div key={i} className={`chat-msg${m.role === 'user' ? ' mine' : ' bot'}`}>
              {m.role === 'assistant' && <span className="chat-bot-av"><Bot size={16} /></span>}
              <div className="chat-bubble"><div className="chat-text">{m.text}</div></div>
            </div>
          ))}
          {busy && (
            <div className="chat-msg bot">
              <span className="chat-bot-av"><Bot size={16} /></span>
              <div className="chat-bubble"><div className="chat-text typing">{t('assistant.thinking')}</div></div>
            </div>
          )}
          <div ref={endRef} />
        </div>
        <div className="chat-input">
          <input className="inp" value={input} onChange={(e) => setInput(e.target.value)} placeholder={t('assistant.placeholder')}
            disabled={!!notConfigured || busy} onKeyDown={(e) => { if (e.key === 'Enter') void send() }} maxLength={1000} />
          <button className="btn primary icon" type="button" onClick={() => void send()} disabled={!!notConfigured || busy || !input.trim()} aria-label={t('chat.send')}><Send size={16} /></button>
        </div>
      </div>
    </div>
  )
}
