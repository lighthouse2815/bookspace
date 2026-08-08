import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthorsDirectoryPage, CategoriesDirectoryPage } from './CatalogMetadataDirectoryPages'

const mocks = vi.hoisted(() => ({
  directory: vi.fn(),
}))

vi.mock('../../hooks/useCatalog', () => ({
  useMetadataDirectory: (...args: unknown[]) => mocks.directory(...args),
}))

vi.mock('../../components/catalog/CatalogFollowButton', () => ({
  CatalogFollowButton: () => null,
}))

function queryResult(data: unknown, overrides: Record<string, unknown> = {}) {
  return {
    data,
    isLoading: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...overrides,
  }
}

function page(items: unknown[], currentPage = 1, totalPages = 1) {
  return {
    items,
    page: currentPage,
    pageSize: 12,
    totalItems: items.length,
    totalPages,
  }
}

function renderRoute(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/authors" element={<AuthorsDirectoryPage />} />
        <Route path="/categories" element={<CategoriesDirectoryPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('CatalogMetadataDirectoryPages', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.directory.mockReturnValue(
      queryResult(
        page([
          {
            id: 'author-1',
            name: 'Haruki Murakami',
            biography: 'Nhà văn Nhật Bản.',
            bookCount: 2,
          },
        ], 2, 3),
      ),
    )
  })

  it('keeps author search, sorting, and paging in the URL-backed query', async () => {
    const user = userEvent.setup()
    renderRoute('/authors?search=Murakami&sort=bookCount&page=2')

    expect(screen.getByRole('heading', { name: 'Khám phá tác giả' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Haruki Murakami/ })).toHaveAttribute(
      'href',
      '/authors/author-1',
    )
    expect(mocks.directory).toHaveBeenCalledWith('author', {
      search: 'Murakami',
      sort: 'bookCount',
      page: 2,
      pageSize: 12,
    })

    await user.selectOptions(screen.getByLabelText('Sắp xếp tác giả'), 'name')
    await waitFor(() =>
      expect(mocks.directory).toHaveBeenLastCalledWith('author', {
        search: 'Murakami',
        sort: 'name',
        page: 1,
        pageSize: 12,
      }),
    )
  })

  it('submits a category search and exposes the empty-state reset', async () => {
    mocks.directory.mockReturnValue(queryResult(page([])))
    const user = userEvent.setup()
    renderRoute('/categories')

    expect(screen.getByRole('heading', { name: 'Không tìm thấy thể loại phù hợp' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Tìm thể loại'), 'Kinh điển')
    await user.click(screen.getByRole('button', { name: 'Tìm kiếm' }))

    await waitFor(() =>
      expect(mocks.directory).toHaveBeenLastCalledWith('category', {
        search: 'Kinh điển',
        sort: 'name',
        page: 1,
        pageSize: 12,
      }),
    )
    expect(screen.getByRole('link', { name: /Tác giả/ })).toHaveAttribute('href', '/authors')
  })
})
