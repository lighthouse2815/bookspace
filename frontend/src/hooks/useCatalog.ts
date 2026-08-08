import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import {
  catalogService,
  type BookQuery,
  type MetadataDirectoryQuery,
  type RecommendationQuery,
} from '../services/catalog.service'
import { recommendationKeys } from './recommendationKeys'

export type MetadataDirectoryKind = 'author' | 'category'

export const catalogKeys = {
  all: ['catalog'] as const,
  books: (query: BookQuery) => [...catalogKeys.all, 'books', query] as const,
  book: (id: string) => [...catalogKeys.all, 'book', id] as const,
  relatedBooks: (id: string, limit: number) =>
    [...catalogKeys.all, 'book', id, 'related', limit] as const,
  author: (id: string) => [...catalogKeys.all, 'author', id] as const,
  category: (id: string) => [...catalogKeys.all, 'category', id] as const,
  authorDirectory: (query: MetadataDirectoryQuery) =>
    [...catalogKeys.all, 'author-directory', query] as const,
  categoryDirectory: (query: MetadataDirectoryQuery) =>
    [...catalogKeys.all, 'category-directory', query] as const,
  metadataDirectory: (kind: MetadataDirectoryKind, query: MetadataDirectoryQuery) =>
    [...catalogKeys.all, `${kind}-directory`, query] as const,
  categories: () => [...catalogKeys.all, 'categories'] as const,
  authors: () => [...catalogKeys.all, 'authors'] as const,
  following: (scope: string) => [...catalogKeys.all, 'following', scope] as const,
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

export function useRelatedBooks(id?: string, limit = 4) {
  return useQuery({
    queryKey: catalogKeys.relatedBooks(id ?? '', limit),
    queryFn: () => catalogService.relatedBooks(id!, limit),
    enabled: Boolean(id),
  })
}

export function useAuthor(id?: string) {
  return useQuery({
    queryKey: catalogKeys.author(id ?? ''),
    queryFn: () => catalogService.author(id!),
    enabled: Boolean(id),
  })
}

export function useCategory(id?: string) {
  return useQuery({
    queryKey: catalogKeys.category(id ?? ''),
    queryFn: () => catalogService.category(id!),
    enabled: Boolean(id),
  })
}

export function useAuthorDirectory(query: MetadataDirectoryQuery) {
  return useQuery({
    queryKey: catalogKeys.authorDirectory(query),
    queryFn: () => catalogService.authorDirectory(query),
  })
}

export function useCategoryDirectory(query: MetadataDirectoryQuery) {
  return useQuery({
    queryKey: catalogKeys.categoryDirectory(query),
    queryFn: () => catalogService.categoryDirectory(query),
  })
}

export function useMetadataDirectory(
  kind: MetadataDirectoryKind,
  query: MetadataDirectoryQuery,
) {
  return useQuery({
    queryKey: catalogKeys.metadataDirectory(kind, query),
    queryFn: () =>
      kind === 'author'
        ? catalogService.authorDirectory(query)
        : catalogService.categoryDirectory(query),
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

export function useCatalogFollowing() {
  const { user, isLoading } = useAuth()
  const scope = user?.id ?? 'guest'
  return useQuery({
    queryKey: catalogKeys.following(scope),
    queryFn: catalogService.following,
    enabled: Boolean(user) && !isLoading,
  })
}

export type CatalogFollowKind = 'author' | 'category'

export function useSetCatalogFollow() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const scope = user?.id ?? 'guest'

  return useMutation({
    mutationFn: async ({
      kind,
      id,
      following,
    }: {
      kind: CatalogFollowKind
      id: string
      following: boolean
    }) => {
      if (kind === 'author') {
        return following
          ? catalogService.followAuthor(id)
          : catalogService.unfollowAuthor(id)
      }
      return following
        ? catalogService.followCategory(id)
        : catalogService.unfollowCategory(id)
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: catalogKeys.following(scope) }),
        queryClient.invalidateQueries({ queryKey: recommendationKeys.scoped(scope) }),
      ])
    },
  })
}
