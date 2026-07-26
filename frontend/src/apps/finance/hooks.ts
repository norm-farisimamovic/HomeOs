import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { type BillInput, type BudgetInput, financeApi, financeKeys, type TransactionInput } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useTransactions() {
  return useQuery({ queryKey: financeKeys.transactions, queryFn: financeApi.transactions })
}
export function useFinanceSummary() {
  return useQuery({ queryKey: financeKeys.summary, queryFn: financeApi.summary })
}
export function useBills() {
  return useQuery({ queryKey: financeKeys.bills, queryFn: financeApi.bills })
}

export function useBudgets() {
  return useQuery({ queryKey: financeKeys.budgets, queryFn: financeApi.budgets })
}

function refresh(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: financeKeys.transactions })
  void qc.invalidateQueries({ queryKey: financeKeys.summary })
  void qc.invalidateQueries({ queryKey: financeKeys.bills })
  void qc.invalidateQueries({ queryKey: financeKeys.budgets })
}

export function useSaveBudget() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: BudgetInput) => financeApi.saveBudget(input),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: financeKeys.budgets }); toast.success(i18n.t('finance.toast.budgetSaved')) },
    onError: toastError,
  })
}
export function useDeleteBudget() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (category: string) => financeApi.deleteBudget(category),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: financeKeys.budgets }); toast.success(i18n.t('finance.toast.budgetDeleted')) },
    onError: toastError,
  })
}

export function useAddTransaction() {
  const qc = useQueryClient()
  // Error surfaces inline in the add modal; here we only celebrate success.
  return useMutation({
    mutationFn: (input: TransactionInput) => financeApi.addTransaction(input),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('finance.toast.txAdded')) },
  })
}
export function useDeleteTransaction() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => financeApi.deleteTransaction(id),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('finance.toast.txDeleted')) },
    onError: toastError,
  })
}
export function useAddBill() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: BillInput) => financeApi.addBill(input),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('finance.toast.billAdded')) },
  })
}
export function useDeleteBill() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => financeApi.deleteBill(id),
    onSuccess: () => { refresh(qc); toast.success(i18n.t('finance.toast.billDeleted')) },
    onError: toastError,
  })
}
