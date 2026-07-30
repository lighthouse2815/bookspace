import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  readingProductService,
  type ReadingNoteQuery,
  type SaveReadingGoalInput,
  type SaveReadingNoteInput,
  type UpdateReadingNoteInput,
} from '../services/reading-product.service'

export const readingProductKeys = {
  goals: ['reading-goals'] as const,
  notes: (query: ReadingNoteQuery = {}) => ['reading-notes', query] as const,
}

export function useReadingGoals() {
  return useQuery({
    queryKey: readingProductKeys.goals,
    queryFn: readingProductService.goals,
  })
}

export function useCreateReadingGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveReadingGoalInput) => readingProductService.createGoal(input),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: readingProductKeys.goals }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['reading-insights'] }),
      ]),
  })
}

export function useUpdateReadingGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: SaveReadingGoalInput }) =>
      readingProductService.updateGoal(id, input),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: readingProductKeys.goals }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['reading-insights'] }),
      ]),
  })
}

export function useDeleteReadingGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => readingProductService.deleteGoal(id),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: readingProductKeys.goals }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['reading-insights'] }),
      ]),
  })
}

export function useReadingNotes(query: ReadingNoteQuery = {}) {
  return useQuery({
    queryKey: readingProductKeys.notes(query),
    queryFn: () => readingProductService.notes(query),
  })
}

export function useCreateReadingNote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveReadingNoteInput) => readingProductService.createNote(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['reading-notes'] }),
  })
}

export function useUpdateReadingNote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateReadingNoteInput }) =>
      readingProductService.updateNote(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['reading-notes'] }),
  })
}

export function useDeleteReadingNote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => readingProductService.deleteNote(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['reading-notes'] }),
  })
}
