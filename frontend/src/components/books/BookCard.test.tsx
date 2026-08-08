import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { Book } from '../../types/domain'
import { BookCard } from './BookCard'

describe('BookCard', () => {
  it('links the title to the book and the author to the public author profile', () => {
    const book: Book = {
      id: 'book-1',
      title: 'The Left Hand of Darkness',
      author: { id: 'author-1', name: 'Ursula K. Le Guin' },
      averageRating: 4.8,
      reviewCount: 12,
    }

    render(
      <MemoryRouter>
        <BookCard book={book} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: /The Left Hand of Darkness/ })).toHaveAttribute(
      'href',
      '/books/book-1',
    )
    expect(screen.getByRole('link', { name: 'Ursula K. Le Guin' })).toHaveAttribute(
      'href',
      '/authors/author-1',
    )
  })
})
