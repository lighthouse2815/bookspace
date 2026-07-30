import { Outlet } from 'react-router-dom'
import { Footer } from './Footer'
import { Header } from './Header'

export function AppShell() {
  return (
    <div className="min-h-[100dvh] bg-page text-body">
      <Header />
      <main>
        <Outlet />
      </main>
      <Footer />
    </div>
  )
}
