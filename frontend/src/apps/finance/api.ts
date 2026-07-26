import { api } from '@/platform/api/client'

export interface FinTransaction {
  id: string
  kind: 'Expense' | 'Income'
  amount: number
  currency: string
  category: string
  occurredOn: string
  description: string | null
  paidById: string
  paidByName: string | null
}

export interface FinBill {
  id: string
  name: string
  amount: number
  currency: string
  cadence: 'Monthly' | 'Quarterly' | 'Yearly' | 'OneOff'
  nextDue: string
  category: string
  whoPaysId: string | null
  whoPaysName: string | null
  dueInDays: number
}

export interface CategoryTotal { category: string; amount: number }
export interface MemberBalance { memberId: string; name: string; paid: number; net: number }
export interface FinanceSummary {
  month: string
  currency: string
  income: number
  spent: number
  balance: number
  byCategory: CategoryTotal[]
  members: MemberBalance[]
  dueSoonCount: number
  dueSoonAmount: number
}

export interface TransactionInput {
  kind: string
  amount: number
  currency?: string
  category: string
  occurredOn?: string | null
  description?: string
  paidById?: string | null
  visibility?: string
}

export interface BillInput {
  name: string
  amount: number
  currency?: string
  cadence: string
  nextDue: string
  category: string
  whoPaysId?: string | null
  visibility?: string
}

export interface Budget {
  category: string
  limit: number
  spent: number
  remaining: number
  percent: number
  currency: string
}

export interface BudgetInput { category: string; monthlyLimit: number }

export const financeKeys = {
  transactions: ['finance', 'transactions'] as const,
  summary: ['finance', 'summary'] as const,
  bills: ['finance', 'bills'] as const,
  budgets: ['finance', 'budgets'] as const,
}

export const financeApi = {
  transactions: () => api.get<FinTransaction[]>('/api/finance/transactions'),
  summary: () => api.get<FinanceSummary>('/api/finance/summary'),
  bills: () => api.get<FinBill[]>('/api/finance/bills'),
  addTransaction: (input: TransactionInput) => api.post<FinTransaction>('/api/finance/transactions', input),
  deleteTransaction: (id: string) => api.del<void>(`/api/finance/transactions/${id}`),
  addBill: (input: BillInput) => api.post<FinBill>('/api/finance/bills', input),
  deleteBill: (id: string) => api.del<void>(`/api/finance/bills/${id}`),
  budgets: () => api.get<Budget[]>('/api/finance/budgets'),
  saveBudget: (input: BudgetInput) => api.put<void>('/api/finance/budgets', input),
  deleteBudget: (category: string) => api.del<void>(`/api/finance/budgets/${encodeURIComponent(category)}`),
}
