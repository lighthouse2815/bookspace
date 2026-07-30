import { useQuery } from '@tanstack/react-query'
import { catalogService, type BookQuery } from '../services/catalog.service'

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
