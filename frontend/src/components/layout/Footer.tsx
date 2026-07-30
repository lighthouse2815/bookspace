import { Link } from 'react-router-dom'
import { Logo } from './Logo'

export function Footer() {
  return (
    <footer className="border-t border-border bg-surface">
      <div className="container-page grid gap-8 py-10 sm:grid-cols-[1fr_auto] sm:items-end">
        <div>
          <Logo />
          <p className="mt-4 max-w-md text-sm leading-6 text-muted">
            Không gian độc lập để lưu hành trình đọc, chia sẻ góc nhìn và tìm những người đồng điệu.
          </p>
        </div>
        <nav className="flex flex-wrap gap-x-5 gap-y-2 text-sm font-medium text-muted" aria-label="Liên kết cuối trang">
          <Link to="/explore" className="hover:text-heading">
            Khám phá
          </Link>
          <Link to="/clubs" className="hover:text-heading">
            Câu lạc bộ
          </Link>
          <Link to="/challenges" className="hover:text-heading">
            Thử thách
          </Link>
        </nav>
      </div>
    </footer>
  )
}
