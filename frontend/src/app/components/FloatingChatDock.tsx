import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bot, MessageCircle, MessagesSquare, Send, Sparkles, X } from 'lucide-react'
import { Avatar } from '@/shared/components/Avatar'
import { ASSISTANT_ID, chatApi, chatKeys } from '@/apps/chat/api'
import { assistantApi, type AssistantChatMessage } from '@/platform/assistant/api'

type Tab = 'assistant' | 'chat'
const THREAD_KEY = 'homeos.assistant.thread'

/**
 * A floating Messenger-style dock. The full Chat and Assistant pages stay as they are; this adds a quick
 * bubble on every screen that opens a small panel where you pick Assistant or Chat. Hidden on those two
 * full pages (where it would be redundant).
 */
const CHAT_READ_KEY = 'homeos.chat.lastRead'

export function FloatingChatDock() {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const [tab, setTab] = useState<Tab>('assistant')
  // Chat query lives here too so the bubble can show an unread badge even when closed.
  const { data: messages } = useQuery({ queryKey: chatKeys.all, queryFn: chatApi.list })
  const [lastRead, setLastRead] = useState<string>(() => localStorage.getItem(CHAT_READ_KEY) ?? '')

  const markChatRead = () => {
    const newest = messages?.[messages.length - 1]?.sentAt
    if (newest && newest !== lastRead) { setLastRead(newest); localStorage.setItem(CHAT_READ_KEY, newest) }
  }

  // Baseline on first load (so old history isn't counted), then clear while viewing the chat tab.
  useEffect(() => {
    if (!messages?.length) return
    if (!lastRead || (open && tab === 'chat')) markChatRead()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [messages, open, tab])

  const unread = lastRead ? (messages ?? []).filter((m) => !m.mine && m.sentAt > lastRead).length : 0

  if (pathname.startsWith('/chat') || pathname.startsWith('/assistant')) return null

  return (
    <>
      {!open && (
        <button className="dock-fab" type="button" onClick={() => { setOpen(true) }} aria-label={t('dock.open')} title={t('dock.open')}>
          <MessagesSquare size={22} />
          {unread > 0 && <span className="dock-badge">{unread > 9 ? '9+' : unread}</span>}
        </button>
      )}
      {open && (
        <div className="dock-panel" role="dialog" aria-label={t('dock.open')}>
          <div className="dock-head">
            <div className="seg dock-tabs">
              <button type="button" className={tab === 'assistant' ? 'on' : undefined} onClick={() => setTab('assistant')}><Sparkles size={14} /> {t('nav.assistant')}</button>
              <button type="button" className={tab === 'chat' ? 'on' : undefined} onClick={() => setTab('chat')}><MessageCircle size={14} /> {t('nav.chat')}</button>
            </div>
            <button className="dock-close" type="button" onClick={() => setOpen(false)} aria-label={t('common.close')}><X size={16} /></button>
          </div>
          {tab === 'assistant' ? <AssistantPanel /> : <ChatPanel />}
        </div>
      )}
    </>
  )
}

/** Compact household-chat panel (reuses the same live-updated ['chat'] query). */
function ChatPanel() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { data: messages } = useQuery({ queryKey: chatKeys.all, queryFn: chatApi.list })
  const [text, setText] = useState('')
  const endRef = useRef<HTMLDivElement>(null)
  const send = useMutation({
    mutationFn: (v: string) => chatApi.send(v),
    onSuccess: () => { setText(''); void qc.invalidateQueries({ queryKey: chatKeys.all }) },
  })
  useEffect(() => { endRef.current?.scrollIntoView({ behavior: 'smooth' }) }, [messages])
  const submit = () => { const v = text.trim(); if (v && !send.isPending) send.mutate(v) }

  return (
    <div className="dock-body">
      <div className="dock-thread">
        {(messages ?? []).slice(-40).map((m) => {
          const isBot = m.senderId === ASSISTANT_ID
          return (
            <div key={m.id} className={`chat-msg${m.mine ? ' mine' : ''}${isBot ? ' bot' : ''}`}>
              {!m.mine && (isBot ? <span className="chat-bot-av"><Bot size={15} /></span> : <Avatar name={m.senderName} memberId={m.senderId} size="xs" />)}
              <div className="chat-bubble">
                {!m.mine && <div className="chat-sender">{isBot ? t('chat.assistant') : m.senderName}</div>}
                <div className="chat-text">{m.text}</div>
              </div>
            </div>
          )
        })}
        {(messages?.length ?? 0) === 0 && <p className="hint" style={{ padding: 8 }}>{t('chat.emptySub')}</p>}
        <div ref={endRef} />
      </div>
      <div className="dock-input">
        <input className="inp" value={text} onChange={(e) => setText(e.target.value)} placeholder={t('chat.placeholder')}
          onKeyDown={(e) => { if (e.key === 'Enter') submit() }} maxLength={2000} />
        <button className="btn primary icon" type="button" onClick={submit} disabled={send.isPending || !text.trim()} aria-label={t('chat.send')}><Send size={15} /></button>
      </div>
    </div>
  )
}

/** Compact assistant panel — shares the same saved conversation as the full Assistant page. */
function AssistantPanel() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { data: status } = useQuery({ queryKey: ['assistant', 'status'], queryFn: assistantApi.status })
  const [messages, setMessages] = useState<AssistantChatMessage[]>(() => {
    try { return JSON.parse(localStorage.getItem(THREAD_KEY) ?? '[]') as AssistantChatMessage[] } catch { return [] }
  })
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const endRef = useRef<HTMLDivElement>(null)
  useEffect(() => { localStorage.setItem(THREAD_KEY, JSON.stringify(messages)) }, [messages])
  useEffect(() => { endRef.current?.scrollIntoView({ behavior: 'smooth' }) }, [messages, busy])

  const notConfigured = status && !status.configured

  const send = async () => {
    const v = input.trim()
    if (!v || busy) return
    const next = [...messages, { role: 'user' as const, text: v }]
    setMessages(next); setInput(''); setBusy(true)
    try {
      const reply = await assistantApi.chat(next)
      setMessages([...next, { role: 'assistant', text: reply.text || t('assistant.done') }])
      if (reply.actions.length > 0) void qc.invalidateQueries()
    } catch {
      setMessages([...next, { role: 'assistant', text: t('common.error') }])
    } finally { setBusy(false) }
  }

  return (
    <div className="dock-body">
      <div className="dock-thread">
        {notConfigured && <p className="hint" style={{ padding: 8 }}>{t('assistant.notConfigured')}</p>}
        {!notConfigured && messages.length === 0 && <p className="hint" style={{ padding: 8 }}>{t('assistant.hint')}</p>}
        {messages.map((m, i) => (
          <div key={i} className={`chat-msg${m.role === 'user' ? ' mine' : ' bot'}`}>
            {m.role === 'assistant' && <span className="chat-bot-av"><Bot size={15} /></span>}
            <div className="chat-bubble"><div className="chat-text">{m.text}</div></div>
          </div>
        ))}
        {busy && <div className="chat-msg bot"><span className="chat-bot-av"><Bot size={15} /></span><div className="chat-bubble"><div className="chat-text typing">{t('assistant.thinking')}</div></div></div>}
        <div ref={endRef} />
      </div>
      <div className="dock-input">
        <input className="inp" value={input} onChange={(e) => setInput(e.target.value)} placeholder={t('assistant.placeholder')}
          disabled={!!notConfigured || busy} onKeyDown={(e) => { if (e.key === 'Enter') void send() }} maxLength={1000} />
        <button className="btn primary icon" type="button" onClick={() => void send()} disabled={!!notConfigured || busy || !input.trim()} aria-label={t('chat.send')}><Send size={15} /></button>
      </div>
    </div>
  )
}
