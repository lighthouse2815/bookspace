import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Book } from '../../types/domain'
import { AuthorDetailPage, CategoryDetailPage } from './CatalogMetadataDetailPages'

const mocks = vi.hoisted(() => ({
  author: vi.fn(),
  category: vi.fn(),
  books: vi.fn(),
}))

vi.mock('../../services/catalog.service', () => ({
  catalogService: {
    author: mocks.author,
    category: mocks.category,
    books: mocks.books,
  },
}))

vi.mock('../../components/catalog/CatalogFollowButton', () => ({
  CatalogFollowButton: () => null,
}))

const book: Book = {
  id: 'book-1',
  title: 'The Left Hand of Darkness',
  author: { id: 'author-1', name: 'Ursula K. Le Guin' },
  categories: [{ id: 'category-1', name: 'Khoa học viễn tưởng' }],
  averageRating: 4.8,
  reviewCount: 12,
}

function page(items: Book[], currentPage = 1, totalPages = 1) {
  return {
    items,
    page: currentPage,
    pageSize: 12,
    totalItems: items.length,
    totalPages,
  }
}

function renderRoute(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/authors/:id" element={<AuthorDetailPage />} />
          <Route path="/categories/:id" element={<CategoryDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('CatalogMetadataDetailPages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.author.mockResolvedValue({
      id: 'author-1',
      name: 'Ursula K. Le Guin',
      biography: 'Nhà văn tiên phong của speculative fiction.',
      bookCount: 1,
    })
    mocks.category.mockResolvedValue({
      id: 'category-1',
      name: 'Khoa học viễn tưởng',
      description: 'Những thế giới đặt câu hỏi về con người và tương lai.',
      bookCount: 1,
    })
    mocks.books.mockResolvedValue(page([book]))
  })

  it('renders the public author profile and requests its paged books', async () => {
    renderRoute('/authors/author-1?page=2')

    expect(await screen.findByRole('heading', { name: 'Ursula K. Le Guin' })).toBeInTheDocument()
    expect(screen.getByText('Nhà văn tiên phong của speculative fiction.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /The Left Hand of Darkness/ })).toHaveAttribute(
      'href',
      '/books/book-1',
    )
    await waitFor(() =>
      expect(mocks.books).toHaveBeenCalledWith({
        authorId: 'author-1',
        categoryId: undefined,
        sort: 'title',
        page: 2,
        pageSize: 12,
      }),
    )
    expect(mocks.category).not.toHaveBeenCalled()
  })

  it('renders the public category profile and requests its books', async () => {
    renderRoute('/categories/category-1')

    expect(await screen.findByRole('heading', { name: 'Khoa học viễn tưởng' })).toBeInTheDocument()
    expect(
      screen.getByText('Những thế giới đặt câu hỏi về con người và tương lai.'),
    ).toBeInTheDocument()
    await waitFor(() =>
      expect(mocks.books).toHaveBeenCalledWith({
        authorId: undefined,
        categoryId: 'category-1',
        sort: 'title',
        page: 1,
        pageSize: 12,
      }),
    )
    expect(mocks.author).not.toHaveBeenCalled()
  })
})
