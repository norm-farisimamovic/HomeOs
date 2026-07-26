/**
 * Typed fetch wrapper — the single entry point for talking to the API. Owns JSON handling,
 * credentials (cookie auth from M1), and turning non-2xx responses into a typed {@link ApiError}
 * carrying the RFC-9457 ProblemDetails body. Apps never call `fetch` directly.
 */

/** Error thrown for any non-2xx API response; `problem` holds the parsed ProblemDetails when present. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, problem: unknown, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

/**
 * Best human message from an RFC-9457 body. Prefers a localized field message from `errors`
 * (ValidationProblemDetails — whose top-level `title` is an English framework default), then the
 * localized `title`, then a `message` field, then the generic fallback.
 */
function problemTitle(body: unknown, fallback: string): string {
  if (body && typeof body === 'object') {
    const b = body as Record<string, unknown>
    if (b.errors && typeof b.errors === 'object') {
      for (const msgs of Object.values(b.errors as Record<string, unknown>)) {
        if (Array.isArray(msgs) && typeof msgs[0] === 'string') return msgs[0]
      }
    }
    if (typeof b.title === 'string') return b.title
    if (typeof b.message === 'string') return b.message
  }
  return fallback
}

/**
 * The UI language to send as `Accept-Language`. Read from the same localStorage key i18next persists
 * to (`homeos.lang`) rather than importing the i18n instance — keeps this module dependency-free and
 * avoids any init-order coupling. Normalized to a supported language; defaults to Bosnian.
 */
function currentLanguage(): string {
  const stored = typeof localStorage !== 'undefined' ? localStorage.getItem('homeos.lang') : null
  return stored?.startsWith('en') ? 'en' : 'bs'
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    credentials: 'include',
    ...init,
    // Tell the API which language to answer in (error titles, emails) — mirrors the current UI language.
    headers: { 'Content-Type': 'application/json', 'Accept-Language': currentLanguage(), ...init?.headers },
  })

  // Match any JSON media type — crucially `application/problem+json` (RFC-9457 errors), whose localized
  // `title` we must read. A plain `application/json` substring check misses `+json` types.
  const contentType = response.headers.get('content-type') ?? ''
  const isJson = contentType.includes('json')
  const raw = isJson ? await response.json().catch(() => null) : await response.text()
  const body: unknown = raw === '' ? null : raw

  if (!response.ok) {
    // Prefer the server's localized ProblemDetails title; only fall back to a generic message
    // (never the raw English HTTP status text, which reads as "Unauthorized"/"Bad Request").
    throw new ApiError(response.status, body, problemTitle(body, genericError()))
  }
  return body as T
}

/** Last-resort error text when the response carries no ProblemDetails title. Localized, not HTTP status text. */
function genericError(): string {
  return currentLanguage() === 'en' ? 'Something went wrong.' : 'Nešto je pošlo po zlu.'
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, data?: unknown) =>
    request<T>(path, { method: 'POST', body: data === undefined ? undefined : JSON.stringify(data) }),
  put: <T>(path: string, data?: unknown) =>
    request<T>(path, { method: 'PUT', body: data === undefined ? undefined : JSON.stringify(data) }),
  del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
