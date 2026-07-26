import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '@/platform/api/client'
import { authApi, type LoginInput, type ProfileInput, type RegisterInput } from './api'
import type { Me } from './types'

/** Query key for the current member. Reused by any app needing the signed-in user. */
export const meQueryKey = ['auth', 'me'] as const

/** Current member, or `null` when not authenticated (a 401 is not treated as an error). */
export function useMe() {
  return useQuery<Me | null>({
    queryKey: meQueryKey,
    queryFn: async () => {
      try {
        return await authApi.me()
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: 60_000,
    retry: false,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: LoginInput) => authApi.login(input),
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, me),
  })
}

export function useRegister() {
  // Strict flow: registration does NOT sign in — it returns a "confirm your email" prompt.
  return useMutation({
    mutationFn: (input: RegisterInput) => authApi.register(input),
  })
}

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ProfileInput) => authApi.updateProfile(input),
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, me),
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: ({ current, next }: { current: string; next: string }) =>
      authApi.changePassword(current, next),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: () => {
      queryClient.setQueryData(meQueryKey, null)
      queryClient.clear()
    },
  })
}
