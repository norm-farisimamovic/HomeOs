/** The current authenticated member + household, as returned by `/api/auth/me`. */
export interface Me {
  id: string
  email: string
  firstName: string
  lastName: string
  displayName: string
  householdId: string
  householdName: string
  roles: string[]
  preferredCulture: string
  preferredCurrency: string
  digestFrequency: 'Off' | 'Daily' | 'Weekly'
  hasAvatar: boolean
}
