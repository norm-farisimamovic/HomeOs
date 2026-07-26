import { api, ApiError } from '@/platform/api/client'
import type { Me } from './types'

/** Registration input — creates a household and its first (Owner) member. */
export interface RegisterInput {
  email: string
  password: string
  firstName: string
  lastName: string
  householdName: string
  preferredCulture?: string
}

/** Login input. */
export interface LoginInput {
  email: string
  password: string
  rememberMe: boolean
}

/** Profile edit input. */
export interface ProfileInput {
  firstName: string
  lastName: string
  preferredCulture?: string
  preferredCurrency?: string
  digestFrequency?: string
}

/** Public invite details (for the accept page). */
export interface InviteInfo {
  householdName: string
  email: string
  displayName: string
  role: string
}

/** Registration result — strict flow returns a confirmation prompt, not a session. */
export interface RegisterResult {
  requiresConfirmation: boolean
  email: string
}

/** Auth endpoints. Cookies are handled by the browser (httpOnly, set by the API). */
export const authApi = {
  me: () => api.get<Me>('/api/auth/me'),
  register: (input: RegisterInput) => api.post<RegisterResult>('/api/auth/register', input),
  login: (input: LoginInput) => api.post<Me>('/api/auth/login', input),
  logout: () => api.post<void>('/api/auth/logout'),
  confirmEmail: (userId: string, token: string) => api.post<Me>('/api/auth/confirm-email', { userId, token }),
  resendConfirmation: (email: string) => api.post<void>('/api/auth/resend-confirmation', { email }),
  forgotPassword: (email: string) => api.post<void>('/api/auth/forgot-password', { email }),
  resetPassword: (userId: string, token: string, newPassword: string) =>
    api.post<void>('/api/auth/reset-password', { userId, token, newPassword }),
  updateProfile: (input: ProfileInput) => api.put<Me>('/api/auth/profile', input),
  sendDigestPreview: () => api.post<{ sent: boolean }>('/api/digest/preview'),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<void>('/api/auth/password', { currentPassword, newPassword }),
  // Multipart upload — bypass the JSON client so the browser sets the multipart boundary itself.
  uploadAvatar: async (file: File) => {
    const form = new FormData()
    form.append('file', file)
    const res = await fetch('/api/auth/avatar', { method: 'POST', credentials: 'include', body: form })
    if (!res.ok) {
      const body = await res.json().catch(() => null)
      throw new ApiError(res.status, body, (body as { title?: string })?.title ?? 'Upload failed.')
    }
  },
  deleteAvatar: () => api.del<void>('/api/auth/avatar'),
  getInvite: (token: string) => api.get<InviteInfo>(`/api/invites/${token}`),
  acceptInvite: (token: string, password: string) =>
    api.post<{ id: string }>(`/api/invites/${token}/accept`, { password }),
}
