import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { readingService, type LibraryInput, type LibraryUpdate, type SessionInput } from '../services/reading.service'
import type { Shelf } from '../types/domain'
import { challengeKeys } from './challengeKeys'

export const readingKeys = {
  library: (shelf?: Shelf) => ['library', shelf ?? 'ALL'] as const,
  sessions: ['reading-sessions'] as const,
}

function invalidateReadingSummaries(
  queryClient: ReturnType<typeof useQueryClient>,
  includeSessions = false,
) {
  const invalidations = [
    queryClient.invalidateQueries({ queryKey: ['library'] }),
    queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
    queryClient.invalidateQueries({ queryKey: ['reading-goals'] }),
    queryClient.invalidateQueries({ queryKey: ['reading-insights'] }),
    queryClient.invalidateQueries({ queryKey: challengeKeys.all }),
    queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  ]

  if (includeSessions) {
    invalidations.push(queryClient.invalidateQueries({ queryKey: readingKeys.sessions }))
  }

  return Promise.all(invalidations)
}

export function useLibrary(shelf?: Shelf, enabled = true) {
  return useQuery({
    queryKey: readingKeys.library(shelf),
    queryFn: () => readingService.library(shelf),
    enabled,
  })
}

export function useAddToLibrary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: LibraryInput) => readingService.addToLibrary(input),
    onSuccess: () => invalidateReadingSummaries(queryClient),
  })
}

export function useUpdateLibrary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: LibraryUpdate }) =>
      readingService.updateLibrary(id, input),
    onSuccess: () => invalidateReadingSummaries(queryClient),
  })
}

export function useRemoveFromLibrary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => readingService.removeFromLibrary(id),
    onSuccess: () => invalidateReadingSummaries(queryClient),
  })
}

export function useSessions() {
  return useQuery({
    queryKey: readingKeys.sessions,
    queryFn: readingService.sessions,
  })
}

export function useCreateSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SessionInput) => readingService.createSession(input),
    onSuccess: () => invalidateReadingSummaries(queryClient, true),
  })
}
