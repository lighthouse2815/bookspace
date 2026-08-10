import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { PrivacyPage, TermsPage } from '../../pages/public/LegalPages'
import { AppShell } from './AppShell'

vi.mock('./Header', () => ({
  Header: () => <header>Header</header>,
}))

function renderShell(path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<p>Nội dung kiểm tra</p>} />
            <Route path="privacy" element={<PrivacyPage />} />
            <Route path="terms" element={<TermsPage />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('release shell', () => {
  it('provides skip navigation, legal links and route metadata', async () => {
    document.head.innerHTML = `
      <meta name="description" content="">
      <meta property="og:title" content="">
      <meta property="og:description" content="">
    `
    renderShell('/privacy')

    expect(screen.getByRole('link', { name: 'Bỏ qua điều hướng' })).toHaveAttribute(
      'href',
      '#main-content',
    )
    expect(document.querySelector('main')).toHaveAttribute('id', 'main-content')
    expect(screen.getByRole('link', { name: 'Quyền riêng tư' })).toHaveAttribute(
      'href',
      '/privacy',
    )
    expect(screen.getByRole('link', { name: 'Điều khoản' })).toHaveAttribute('href', '/terms')
    expect(screen.getByRole('heading', { name: 'Quyền riêng tư' })).toBeInTheDocument()
    await waitFor(() => expect(document.title).toBe('Quyền riêng tư | BookSpace'))
    expect(document.querySelector('meta[name="description"]')).toHaveAttribute(
      'content',
      'Cách BookSpace thu thập, sử dụng và bảo vệ dữ liệu.',
    )
  })

  it('renders the public terms page with a path back home', () => {
    renderShell('/terms')

    expect(screen.getByRole('heading', { name: 'Điều khoản sử dụng' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Về trang chủ' })).toHaveAttribute('href', '/')
  })
})
