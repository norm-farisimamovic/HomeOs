import { api } from '@/platform/api/client'

/** A global-search result from any app. */
export interface SearchHit {
  source: string
  id: string
  title: string
  subtitle: string | null
  link: string
}

export const searchApi = {
  query: (q: string) => api.get<SearchHit[]>(`/api/search?q=${encodeURIComponent(q)}`),
}
