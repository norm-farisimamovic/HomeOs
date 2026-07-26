import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BellPlus, Bot, MessagesSquare, Send } from 'lucide-react'
import { Avatar } from '@/shared/components/Avatar'
import { toast } from '@/platform/ui/toastStore'
import { useMembers } from '@/platform/members/useMembers'
import { useMe } from '@/platform/auth/useAuth'
import { ASSISTANT_ID, chatApi, chatKeys } from './api'

/** Household chat — a shared message stream, live over SignalR (the shell invalidates ['chat'] on push). */
export function ChatPage() {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const { data: messages, isLoading } = useQuery({ queryKey: chatKeys.all, queryFn: chatApi.list })
  const { data: members = [] } = useMembers()
  const { data: me } = useMe()
  const [text, setText] = useState('')
  const [mention, setMention] = useState<{ query: string; at: number } | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const endRef = useRef<HTMLDivElement>(null)
  const locale = i18n.resolvedLanguage === 'bs' ? 'bs-BA' : 'en-GB'

  const send = useMutation({
    mutationFn: (t2: string) => chatApi.send(t2),
    onSuccess: () => { setText(''); setMention(null); void qc.invalidateQueries({ queryKey: chatKeys.all }) },
  })

  const toReminder = useMutation({
    mutationFn: (id: string) => chatApi.toReminder(id),
    onSuccess: (r) => toast.success(t('chat.reminderCreated', { date: r.date })),
    onError: () => toast.error(t('common.error')),
  })

  // Stick to the newest message.
  useEffect(() => { endRef.current?.scrollIntoView({ behavior: 'smooth' }) }, [messages])

  // People matching the @mention being typed (excluding the current user).
  const suggestions = useMemo(() => {
    if (!mention) return []
    const q = mention.query.toLowerCase()
    return members
      .filter((m) => m.id !== me?.id && m.displayName.toLowerCase().includes(q))
      .slice(0, 6)
  }, [mention, members, me?.id])

  // Detect an "@token" immediately before the caret so we can suggest members.
  const onInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const v = e.target.value
    setText(v)
    const caret = e.target.selectionStart ?? v.length
    const upto = v.slice(0, caret)
    const m = /(^|\s)@(\S*)$/.exec(upto)
    const q = m?.[2] ?? ''
    setMention(m ? { query: q, at: caret - q.length - 1 } : null)
  }

  const pickMention = (displayName: string) => {
    if (!mention) return
    const first = displayName.split(' ')[0]
    const before = text.slice(0, mention.at)
    const after = text.slice(mention.at + 1 + mention.query.length)
    const next = `${before}@${first} ${after.replace(/^\s/, '')}`
    setText(next)
    setMention(null)
    requestAnimationFrame(() => inputRef.current?.focus())
  }

  const submit = () => { const v = text.trim(); if (v && !send.isPending) send.mutate(v) }
  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') { setMention(null); return }
    if (e.key === 'Enter') {
      // If the suggestion list is open, Enter picks the first match instead of sending.
      if (mention && suggestions[0]) { e.preventDefault(); pickMention(suggestions[0].displayName); return }
      submit()
    }
  }
  const fmtTime = (iso: string) => new Date(iso).toLocaleString(locale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

  return (
    <div className="wrap">
      <div className="page-h">
        <div className="txt">
          <div className="eyebrow" style={{ color: 'var(--m-boards)' }}>{t('nav.chat')}</div>
          <h1>{t('chat.title')}</h1>
          <p className="sub">{t('chat.sub')}</p>
        </div>
      </div>

      <div className="card chat-card">
        <div className="chat-thread">
          {isLoading && <p className="hint" style={{ padding: 16 }}>{t('common.loading')}</p>}
          {!isLoading && (messages?.length ?? 0) === 0 && (
            <div className="empty">
              <span className="empty-ico" style={{ ['--mc' as string]: 'var(--m-boards)' }}><MessagesSquare size={20} /></span>
              <h4>{t('chat.emptyTitle')}</h4><p>{t('chat.emptySub')}</p>
            </div>
          )}
          {(messages ?? []).map((m) => {
            const isBot = m.senderId === ASSISTANT_ID
            return (
              <div key={m.id} className={`chat-msg${m.mine ? ' mine' : ''}${isBot ? ' bot' : ''}`}>
                {!m.mine && (isBot
                  ? <span className="chat-bot-av"><Bot size={16} /></span>
                  : <Avatar name={m.senderName} memberId={m.senderId} size="xs" />)}
                <div className="chat-bubble">
                  {!m.mine && <div className="chat-sender">{isBot ? t('chat.assistant') : m.senderName}</div>}
                  <div className="chat-text">{m.text}</div>
                  <div className="chat-foot">
                    <span className="chat-time">{fmtTime(m.sentAt)}</span>
                    {!isBot && (
                      <button type="button" className="chat-act" title={t('chat.toReminder')}
                        onClick={() => toReminder.mutate(m.id)} disabled={toReminder.isPending}>
                        <BellPlus size={13} />
                      </button>
                    )}
                  </div>
                </div>
              </div>
            )
          })}
          <div ref={endRef} />
        </div>
        <div className="chat-input">
          {mention && suggestions.length > 0 && (
            <div className="mention-pop">
              {suggestions.map((s) => (
                <button type="button" key={s.id} className="mention-opt" onClick={() => pickMention(s.displayName)}>
                  <Avatar name={s.displayName} memberId={s.id} size="xs" />
                  <span>{s.displayName}</span>
                </button>
              ))}
            </div>
          )}
          <input ref={inputRef} className="inp" value={text} onChange={onInput} placeholder={t('chat.placeholder')}
            onKeyDown={onKeyDown} maxLength={2000} />
          <button className="btn primary icon" type="button" onClick={submit} disabled={send.isPending || !text.trim()} aria-label={t('chat.send')}><Send size={16} /></button>
        </div>
        <p className="chat-hint">{t('chat.hint')}</p>
      </div>
    </div>
  )
}
