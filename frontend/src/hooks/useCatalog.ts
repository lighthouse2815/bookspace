import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import {
  catalogService,
  type BookQuery,
  type RecommendationQuery,
} from '../services/catalog.service'
import { recommendationKeys } from './recommendationKeys'

export const catalogKeys = {
  all: ['catalog'] as const,
  books: (query: BookQuery) => [...catalogKeys.all, 'books', query] as const,
  book: (id: string) => [...catalogKeys.all, 'book', id] as const,
  categories: () => [...catalogKeys.all, 'categories'] as const,
  authors: () => [...catalogKeys.all, 'authors'] as const,
}

export function useBooks(query: BookQuery = {}) {
  return useQuery({
    queryKey: catalogKeys.books(query),
    queryFn: () => catalogService.books(query),
  })
}

export function useBook(id?: string) {
  return useQuery({
    queryKey: catalogKeys.book(id ?? ''),
    queryFn: () => catalogService.book(id!),
    enabled: Boolean(id),
  })
}

export function useBookRecommendations({ page = 1, pageSize = 12 }: RecommendationQuery = {}) {
  const { user, isLoading } = useAuth()
  const scope = user?.id ?? 'guest'
  return useQuery({
    queryKey: recommendationKeys.page(scope, page, pageSize),
    queryFn: () => catalogService.recommendations({ page, pageSize }),
    enabled: Boolean(user) && !isLoading,
  })
}

export function useCategories() {
  return useQuery({
    queryKey: catalogKeys.categories(),
    queryFn: catalogService.categories,
    staleTime: 5 * 60_000,
  })
}

export function useAuthors() {
  return useQuery({
    queryKey: catalogKeys.authors(),
    queryFn: catalogService.authors,
    staleTime: 5 * 60_000,
  })
}
