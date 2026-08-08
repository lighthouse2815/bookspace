import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RelatedBooksSection } from './RelatedBooksSection'

const mocks = vi.hoisted(() => ({
  relatedBooks: vi.fn(),
}))

vi.mock('../../hooks/useCatalog', () => ({
  useRelatedBooks: (...args: unknown[]) => mocks.relatedBooks(...args),
}))

function queryResult(data: unknown, overrides: Record<string, unknown> = {}) {
  return {
    data,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...overrides,
  }
}

function renderSection() {
  return render(
    <MemoryRouter>
      <RelatedBooksSection bookId="book-1" bookTitle="Kafka bên bờ biển" />
    </MemoryRouter>,
  )
}

describe('RelatedBooksSection', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders bounded related books with their public author links', () => {
    mocks.relatedBooks.mockReturnValue(
      queryResult([
        {
          id: 'book-2',
          title: 'Rừng Na Uy',
          author: { id: 'author-1', name: 'Haruki Murakami' },
          averageRating: 4.5,
          reviewCount: 10,
        },
      ]),
    )

    renderSection()

    expect(mocks.relatedBooks).toHaveBeenCalledWith('book-1', 4)
    expect(screen.getByRole('heading', { name: 'Sách liên quan' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Rừng Na Uy' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Haruki Murakami' })).toHaveAttribute(
      'href',
      '/authors/author-1',
    )
  })

  it('renders the explicit empty state when no catalog relation exists', () => {
    mocks.relatedBooks.mockReturnValue(queryResult([]))

    renderSection()

    expect(screen.getByRole('heading', { name: 'Chưa có sách liên quan' })).toBeInTheDocument()
  })
})
