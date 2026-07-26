import { api } from '@/platform/api/client'

export interface ShoppingItem { id: string; text: string; done: boolean }
export interface ShoppingList { id: string; name: string; items: ShoppingItem[] }

export const shoppingKeys = { all: ['shopping'] as const }

export const shoppingApi = {
  lists: () => api.get<ShoppingList[]>('/api/shopping/lists'),
  createList: (name: string) => api.post<ShoppingList>('/api/shopping/lists', { name }),
  deleteList: (id: string) => api.del<void>(`/api/shopping/lists/${id}`),
  addItem: (listId: string, text: string) => api.post<ShoppingItem>(`/api/shopping/lists/${listId}/items`, { text }),
  toggleItem: (id: string) => api.post<ShoppingItem>(`/api/shopping/items/${id}/toggle`),
  deleteItem: (id: string) => api.del<void>(`/api/shopping/items/${id}`),
}
