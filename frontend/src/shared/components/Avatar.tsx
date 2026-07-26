import type { CSSProperties } from 'react'
import { useEffect, useState } from 'react'

/**
 * Circular member avatar — the photo when the member has one, initials otherwise. Single source of truth for
 * the member "chip" across the app. Pass `memberId` to show their uploaded picture (falls back to initials if
 * none / on load error). `bust` forces a reload after the current user changes their own photo.
 */
export function Avatar({ name, memberId, size, color, title, bust }: {
  name?: string | null
  memberId?: string | null
  size?: 'xs' | 'lg'
  color?: string
  title?: string
  bust?: number
}) {
  const initials = name?.trim().charAt(0).toUpperCase() || '?'
  const style = color ? ({ background: color } as CSSProperties) : undefined
  const [failed, setFailed] = useState(false)

  // Reset the error state if the member or cache-bust changes (e.g. after an upload).
  useEffect(() => { setFailed(false) }, [memberId, bust])

  const showImg = !!memberId && !failed
  const src = memberId ? `/api/members/${memberId}/avatar${bust ? `?v=${bust}` : ''}` : ''

  return (
    <span className={`av${size ? ` ${size}` : ''}${showImg ? ' has-img' : ''}`} style={style} title={title ?? name ?? undefined}>
      {showImg
        ? <img src={src} alt={name ?? ''} onError={() => setFailed(true)} />
        : initials}
    </span>
  )
}
