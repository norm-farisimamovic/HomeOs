import { api } from '@/platform/api/client'

export interface Currency {
  code: string
  symbol: string
  name: string
}

export const currenciesApi = {
  list: () => api.get<Currency[]>('/api/currencies'),
}

/** Format an amount with a currency, preferring the known symbol (BAM→KM, EUR→€…), falling back to the code. */
const symbols: Record<string, string> = { BAM: 'KM', EUR: '€', USD: '$', GBP: '£', CHF: 'CHF', RSD: 'дин' }
export function formatMoney(amount: number, currency: string, locale: string, opts?: Intl.NumberFormatOptions): string {
  const n = amount.toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2, ...opts })
  return `${n} ${symbols[currency] ?? currency}`
}
