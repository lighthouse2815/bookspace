import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { bookListService, type BookListInput } from '../services/book-list.service'
import type { BookListDetail } from '../types/domain'
import { bookListKeys } from './bookListKeys'

function useBookListScope() {
  const { user } = useAuth()
  return user?.id ?? 'guest'
}

export function useMyBookLists(page = 1, bookId?: string, enabled = true) {
  const { user, isLoading } = useAuth()
  const scope = user?.id ?? 'guest'
  return useQuery({
    queryKey: bookListKeys.minePage(scope, page, bookId),
    queryFn: () => bookListService.mine({ page, bookId }),
    enabled: enabled && Boolean(user) && !isLoading,
  })
}

export function useProfileBookLists(userId?: string, page = 1, enabled = true) {
  const { user, isLoading } = useAuth()
  const scope = user?.id ?? 'guest'
  const isOwner = Boolean(userId && user?.id === userId)
  return useQuery({
    queryKey: isOwner
      ? bookListKeys.minePage(scope, page)
      : bookListKeys.publicByUser(scope, userId ?? '', page),
    queryFn: () =>
      isOwner
        ? bookListService.mine({ page })
        : bookListService.publicByUser(userId!, page),
    enabled: enabled && Boolean(userId) && !isLoading,
  })
}

export function useBookListDetail(listId?: string) {
  const scope = useBookListScope()
  return useQuery({
    queryKey: bookListKeys.detail(scope, listId ?? ''),
    queryFn: () => bookListService.detail(listId!),
    enabled: Boolean(listId),
  })
}

function useBookListInvalidation() {
  const queryClient = useQueryClient()
  return async (detail?: BookListDetail) => {
    if (detail) {
      const scope = detail.isOwner ? detail.owner.id : 'guest'
      queryClient.setQueryData(bookListKeys.detail(scope, detail.id), detail)
    }
    await queryClient.invalidateQueries({ queryKey: bookListKeys.all })
  }
}

export function useCreateBookList() {
  const invalidate = useBookListInvalidation()
  return useMutation({ mutationFn: bookListService.create, onSuccess: invalidate })
}

export function useUpdateBookList(listId: string) {
  const invalidate = useBookListInvalidation()
  return useMutation({
    mutationFn: (input: BookListInput) => bookListService.update(listId, input),
    onSuccess: invalidate,
  })
}

export function useDeleteBookList() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: bookListService.delete,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: bookListKeys.all }),
  })
}

export function useAddBookToList() {
  const invalidate = useBookListInvalidation()
  return useMutation({
    mutationFn: ({ listId, bookId }: { listId: string; bookId: string }) =>
      bookListService.addBook(listId, bookId),
    onSuccess: invalidate,
  })
}

export function useRemoveBookFromList(listId: string) {
  const invalidate = useBookListInvalidation()
  return useMutation({
    mutationFn: (bookId: string) => bookListService.removeBook(listId, bookId),
    onSuccess: invalidate,
  })
}

export function useToggleBookInList() {
  const invalidate = useBookListInvalidation()
  return useMutation({
    mutationFn: ({
      listId,
      bookId,
      containsBook,
    }: {
      listId: string
      bookId: string
      containsBook: boolean
    }) =>
      containsBook
        ? bookListService.removeBook(listId, bookId)
        : bookListService.addBook(listId, bookId),
    onSuccess: invalidate,
  })
}

export function useReorderBookList(listId: string) {
  const invalidate = useBookListInvalidation()
  return useMutation({
    mutationFn: (bookIds: string[]) => bookListService.reorder(listId, bookIds),
    onSuccess: invalidate,
  })
}
