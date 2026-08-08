import { Books, Sparkle } from '@phosphor-icons/react'
import { BookCard } from '../../components/books/BookCard'
import { EmptyState, ErrorState, LoadingGrid } from '../../components/ui/States'
import { useRelatedBooks } from '../../hooks/useCatalog'
import { errorMessage } from '../../lib/api'

export function RelatedBooksSection({
  bookId,
  bookTitle,
}: {
  bookId: string
  bookTitle: string
}) {
  const relatedBooks = useRelatedBooks(bookId, 4)

  return (
    <section className="mt-16 border-t border-border pt-12" aria-labelledby="related-books-title">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 text-accent-strong">
            <Sparkle size={21} weight="fill" aria-hidden />
            <p className="eyebrow">Cùng mạch khám phá</p>
          </div>
          <h2 id="related-books-title" className="mt-3 text-2xl font-bold tracking-tight text-heading">
            Sách liên quan
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
            Gợi ý dựa trên tác giả và thể loại chung với “{bookTitle}”.
          </p>
        </div>
        {relatedBooks.data?.length ? (
          <p className="text-sm font-medium text-muted">{relatedBooks.data.length} gợi ý gần nhất</p>
        ) : null}
      </div>

      <div className="mt-7">
        {relatedBooks.isLoading ? <LoadingGrid count={4} /> : null}
        {relatedBooks.isError ? (
          <ErrorState
            message={errorMessage(
              relatedBooks.error,
              'Không thể tải sách liên quan. Vui lòng thử lại.',
            )}
            retry={() => void relatedBooks.refetch()}
          />
        ) : null}
        {relatedBooks.data?.length ? (
          <div className="book-grid">
            {relatedBooks.data.map((relatedBook) => (
              <BookCard key={relatedBook.id} book={relatedBook} />
            ))}
          </div>
        ) : null}
        {relatedBooks.data && relatedBooks.data.length === 0 ? (
          <EmptyState
            title="Chưa có sách liên quan"
            description="Catalog chưa có cuốn sách khác cùng tác giả hoặc thể loại với cuốn này."
            icon={Books}
          />
        ) : null}
      </div>
    </section>
  )
}
