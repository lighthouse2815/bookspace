import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  readingService,
  type FinishActiveSessionInput,
  type LibraryInput,
  type LibraryUpdate,
  type SessionInput,
  type SessionUpdateInput,
  type StartActiveSessionInput,
} from '../services/reading.service'
import type { Shelf } from '../types/domain'
import { challengeKeys } from './challengeKeys'
import { recommendationKeys } from './recommendationKeys'

export const readingKeys = {
  library: (shelf?: Shelf) => ['library', shelf ?? 'ALL'] as const,
  sessions: ['reading-sessions'] as const,
  activeSession: ['reading-sessions', 'active'] as const,
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
    queryClient.invalidateQueries({ queryKey: ['feed'] }),
    queryClient.invalidateQueries({ queryKey: ['catalog'] }),
    queryClient.invalidateQueries({ queryKey: recommendationKeys.all }),
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

export function useUpdateSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: SessionUpdateInput }) =>
      readingService.updateSession(id, input),
    onSuccess: () => invalidateReadingSummaries(queryClient, true),
  })
}

export function useActiveReadingSession() {
  return useQuery({
    queryKey: readingKeys.activeSession,
    queryFn: readingService.activeSession,
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: true,
    refetchOnReconnect: true,
  })
}

export function useStartActiveReadingSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: StartActiveSessionInput) => readingService.startActiveSession(input),
    onSuccess: (session) => {
      queryClient.setQueryData(readingKeys.activeSession, session)
      return invalidateReadingSummaries(queryClient)
    },
  })
}

export function usePauseActiveReadingSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: readingService.pauseActiveSession,
    onSuccess: (session) => queryClient.setQueryData(readingKeys.activeSession, session),
  })
}

export function useResumeActiveReadingSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: readingService.resumeActiveSession,
    onSuccess: (session) => queryClient.setQueryData(readingKeys.activeSession, session),
  })
}

export function useFinishActiveReadingSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: FinishActiveSessionInput) => readingService.finishActiveSession(input),
    onSuccess: async () => {
      queryClient.setQueryData(readingKeys.activeSession, null)
      await invalidateReadingSummaries(queryClient, true)
    },
  })
}

export function useCancelActiveReadingSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: readingService.cancelActiveSession,
    onSuccess: () => queryClient.setQueryData(readingKeys.activeSession, null),
  })
}
