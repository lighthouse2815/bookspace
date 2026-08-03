export const bookListKeys = {
  all: ['book-lists'] as const,
  mine: (scope: string) => [...bookListKeys.all, scope, 'mine'] as const,
  minePage: (scope: string, page: number, bookId?: string) =>
    [...bookListKeys.mine(scope), page, bookId ?? 'ALL'] as const,
  publicByUser: (scope: string, userId: string, page: number) =>
    [...bookListKeys.all, scope, 'public', userId, page] as const,
  detail: (scope: string, listId: string) =>
    [...bookListKeys.all, scope, 'detail', listId] as const,
}
