import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '@/platform/i18n'
import { ApiError } from '@/platform/api/client'
import { toast } from '@/platform/ui/toastStore'
import { shoppingApi, shoppingKeys } from './api'

function toastError(e: unknown) {
  toast.error(e instanceof ApiError ? e.message : i18n.t('common.error'))
}

export function useShoppingLists() {
  return useQuery({ queryKey: shoppingKeys.all, queryFn: shoppingApi.lists })
}

export function useShoppingMutations() {
  const qc = useQueryClient()
  const refresh = () => qc.invalidateQueries({ queryKey: shoppingKeys.all })

  const createList = useMutation({ mutationFn: (name: string) => shoppingApi.createList(name), onSuccess: refresh, onError: toastError })
  const deleteList = useMutation({ mutationFn: (id: string) => shoppingApi.deleteList(id), onSuccess: refresh, onError: toastError })
  const addItem = useMutation({ mutationFn: (v: { listId: string; text: string }) => shoppingApi.addItem(v.listId, v.text), onSuccess: refresh, onError: toastError })
  const toggleItem = useMutation({ mutationFn: (id: string) => shoppingApi.toggleItem(id), onSuccess: refresh, onError: toastError })
  const deleteItem = useMutation({ mutationFn: (id: string) => shoppingApi.deleteItem(id), onSuccess: refresh, onError: toastError })

  return { createList, deleteList, addItem, toggleItem, deleteItem }
}
