import { Outlet } from 'react-router-dom'
import { RouteMetadata } from '../routing/RouteMetadata'
import { Footer } from './Footer'
import { Header } from './Header'

export function AppShell() {
  return (
    <div className="min-h-[100dvh] bg-page text-body">
      <RouteMetadata />
      <a
        href="#main-content"
        className="fixed left-4 top-3 z-[60] -translate-y-20 rounded-[10px] bg-heading px-4 py-2.5 text-sm font-semibold text-page transition-transform focus:translate-y-0 focus:outline-none focus:ring-2 focus:ring-accent"
      >
        Bỏ qua điều hướng
      </a>
      <Header />
      <main id="main-content" tabIndex={-1}>
        <Outlet />
      </main>
      <Footer />
    </div>
  )
}
