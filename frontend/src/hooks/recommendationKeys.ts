export const recommendationKeys = {
  all: ['book-recommendations'] as const,
  scoped: (scope: string) => [...recommendationKeys.all, scope] as const,
  page: (scope: string, page: number, pageSize: number) =>
    [...recommendationKeys.scoped(scope), page, pageSize] as const,
}
