import {
  Bell,
  Books,
  CaretDown,
  ChartLineUp,
  Compass,
  EnvelopeSimple,
  List,
  Moon,
  Plus,
  SignOut,
  Sun,
  Users,
  UsersThree,
  X,
} from '@phosphor-icons/react'
import { QueryClientContext } from '@tanstack/react-query'
import { useContext, useState } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { useTheme } from '../../contexts/ThemeContext'
import { useUnreadNotificationCount } from '../../hooks/useNotifications'
import { useUnreadDirectMessageCount } from '../../hooks/useDirectMessages'
import { Avatar } from '../ui/Avatar'
import { Button } from '../ui/Button'
import { Logo } from './Logo'

const publicLinks = [
  { to: '/explore', label: 'Khám phá', icon: Compass },
  { to: '/people', label: 'Độc giả', icon: Users },
  { to: '/clubs', label: 'Câu lạc bộ', icon: UsersThree },
  { to: '/challenges', label: 'Thử thách', icon: Books },
]

export function Header() {
  const [mobileOpen, setMobileOpen] = useState(false)
  const [accountOpen, setAccountOpen] = useState(false)
  const { user, isAuthenticated, logout } = useAuth()
  const queryClient = useContext(QueryClientContext)
  const { isDark, toggleTheme } = useTheme()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await logout()
    setAccountOpen(false)
    navigate('/')
  }

  return (
    <header className="sticky top-0 z-50 border-b border-border/80 bg-page/90 backdrop-blur-xl">
      <div className="container-page flex h-16 items-center gap-5">
        <Logo />
        <nav className="hidden items-center gap-1 lg:flex" aria-label="Điều hướng chính">
          {publicLinks.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className={({ isActive }) => `nav-link ${isActive ? 'nav-active' : ''}`}>
              <Icon size={17} aria-hidden />
              {label}
            </NavLink>
          ))}
          {isAuthenticated ? (
            <>
              <NavLink to="/feed" className={({ isActive }) => `nav-link ${isActive ? 'nav-active' : ''}`}>
                Cộng đồng
              </NavLink>
              <NavLink
                to="/library"
                className={({ isActive }) => `nav-link ${isActive ? 'nav-active' : ''}`}
              >
                Thư viện
              </NavLink>
            </>
          ) : null}
        </nav>
        <div className="ml-auto flex items-center gap-2">
          <button
            type="button"
            className="icon-button"
            onClick={toggleTheme}
            aria-label={isDark ? 'Chuyển nhanh sang giao diện sáng' : 'Chuyển nhanh sang giao diện tối'}
          >
            {isDark ? <Sun size={19} /> : <Moon size={19} />}
          </button>
          {isAuthenticated && user ? (
            <>
              {queryClient ? (
                <>
                  <DirectMessageBell />
                  <NotificationBell />
                </>
              ) : (
                <>
                  <Link to="/messages" className="icon-button" aria-label="Tin nhắn">
                    <EnvelopeSimple size={19} />
                  </Link>
                  <Link to="/notifications" className="icon-button" aria-label="Thông báo">
                    <Bell size={19} />
                  </Link>
                </>
              )}
              <div className="relative hidden sm:block">
                <button
                  type="button"
                  onClick={() => setAccountOpen((value) => !value)}
                  className="flex items-center gap-2 rounded-xl p-1.5 pr-2 text-left transition-colors hover:bg-surface-muted focus-visible:focus-ring"
                  aria-expanded={accountOpen}
                >
                  <Avatar src={user.avatarUrl} name={user.displayName} size="sm" />
                  <span className="hidden max-w-32 truncate text-sm font-semibold text-heading xl:block">
                    {user.displayName}
                  </span>
                  <CaretDown size={14} className="text-muted" />
                </button>
                {accountOpen ? (
                  <div className="account-menu">
                    <Link to="/dashboard" onClick={() => setAccountOpen(false)}>
                      Tổng quan
                    </Link>
                    <Link to={`/users/${user.id}`} onClick={() => setAccountOpen(false)}>
                      Hồ sơ
                    </Link>
                    <Link to="/lists" onClick={() => setAccountOpen(false)}>
                      Bộ sưu tập
                    </Link>
                    <Link to="/journal" onClick={() => setAccountOpen(false)}>
                      Nhật ký đọc
                    </Link>
                    <Link to="/following-topics" onClick={() => setAccountOpen(false)}>
                      Nội dung theo dõi
                    </Link>
                    <Link to="/messages" onClick={() => setAccountOpen(false)}>
                      Tin nhắn
                    </Link>
                    <Link to="/goals" onClick={() => setAccountOpen(false)}>
                      Mục tiêu đọc
                    </Link>
                    <Link to="/notes" onClick={() => setAccountOpen(false)}>
                      Ghi chú sách
                    </Link>
                    <Link to="/insights" onClick={() => setAccountOpen(false)}>
                      Phân tích đọc
                    </Link>
                    <Link to="/clubs/invitations" onClick={() => setAccountOpen(false)}>
                      Lời mời câu lạc bộ
                    </Link>
                    <Link to="/clubs/new" onClick={() => setAccountOpen(false)}>
                      Tạo câu lạc bộ
                    </Link>
                    <Link to="/settings" onClick={() => setAccountOpen(false)}>
                      Cài đặt
                    </Link>
                    {user.role === 'ADMIN' ? (
                      <Link to="/admin/books" onClick={() => setAccountOpen(false)}>
                        Quản trị
                      </Link>
                    ) : null}
                    <button type="button" onClick={handleLogout}>
                      <SignOut size={17} />
                      Đăng xuất
                    </button>
                  </div>
                ) : null}
              </div>
            </>
          ) : (
            <div className="hidden items-center gap-2 sm:flex">
              <Link to="/login" className="button button-ghost button-sm">
                Đăng nhập
              </Link>
              <Link to="/register" className="button button-primary button-sm">
                Bắt đầu đọc
              </Link>
            </div>
          )}
          <button
            type="button"
            className="icon-button lg:hidden"
            onClick={() => setMobileOpen((value) => !value)}
            aria-label={mobileOpen ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={mobileOpen}
          >
            {mobileOpen ? <X size={20} /> : <List size={20} />}
          </button>
        </div>
      </div>
      {mobileOpen ? (
        <nav className="mobile-nav" aria-label="Điều hướng di động">
          {publicLinks.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} onClick={() => setMobileOpen(false)}>
              <Icon size={19} />
              {label}
            </NavLink>
          ))}
          {isAuthenticated && user ? (
            <>
              <NavLink to="/feed" onClick={() => setMobileOpen(false)}>
                Cộng đồng
              </NavLink>
              <NavLink to="/library" onClick={() => setMobileOpen(false)}>
                Thư viện cá nhân
              </NavLink>
              <NavLink to="/lists" onClick={() => setMobileOpen(false)}>
                <Books size={19} />
                Bộ sưu tập
              </NavLink>
              <NavLink to="/dashboard" onClick={() => setMobileOpen(false)}>
                Tổng quan
              </NavLink>
              <NavLink to="/journal" onClick={() => setMobileOpen(false)}>
                Nhật ký đọc
              </NavLink>
              <NavLink to="/following-topics" onClick={() => setMobileOpen(false)}>
                <Books size={19} />
                Nội dung theo dõi
              </NavLink>
              <NavLink to="/messages" onClick={() => setMobileOpen(false)}>
                <EnvelopeSimple size={19} />
                Tin nhắn
              </NavLink>
              <NavLink to="/goals" onClick={() => setMobileOpen(false)}>
                Mục tiêu đọc
              </NavLink>
              <NavLink to="/notes" onClick={() => setMobileOpen(false)}>
                Ghi chú sách
              </NavLink>
              <NavLink to="/insights" onClick={() => setMobileOpen(false)}>
                <ChartLineUp size={19} />
                Phân tích đọc
              </NavLink>
              <NavLink to="/clubs/invitations" onClick={() => setMobileOpen(false)}>
                <EnvelopeSimple size={19} />
                Lời mời câu lạc bộ
              </NavLink>
              <NavLink to="/clubs/new" onClick={() => setMobileOpen(false)}>
                <Plus size={19} />
                Tạo câu lạc bộ
              </NavLink>
              <NavLink to={`/users/${user.id}`} onClick={() => setMobileOpen(false)}>
                Hồ sơ
              </NavLink>
              {user.role === 'ADMIN' ? (
                <NavLink to="/admin/books" onClick={() => setMobileOpen(false)}>
                  Quản trị
                </NavLink>
              ) : null}
              <Button variant="ghost" onClick={handleLogout} icon={<SignOut size={18} />}>
                Đăng xuất
              </Button>
            </>
          ) : (
            <div className="grid grid-cols-2 gap-2 pt-2">
              <Link to="/login" onClick={() => setMobileOpen(false)} className="button button-secondary button-md">
                Đăng nhập
              </Link>
              <Link to="/register" onClick={() => setMobileOpen(false)} className="button button-primary button-md">
                Đăng ký
              </Link>
            </div>
          )}
        </nav>
      ) : null}
    </header>
  )
}

function NotificationBell() {
  const unreadNotifications = useUnreadNotificationCount()
  const count = unreadNotifications.data?.count ?? 0

  return (
    <Link
      to="/notifications"
      className="icon-button relative"
      aria-label={count ? `Thông báo, ${count} chưa đọc` : 'Thông báo'}
    >
      <Bell size={19} />
      {count ? (
        <span className="absolute -right-1 -top-1 grid min-h-4 min-w-4 place-items-center rounded-full bg-red-500 px-1 text-[10px] font-bold leading-none text-white">
          {count > 99 ? '99+' : count}
        </span>
      ) : null}
    </Link>
  )
}

function DirectMessageBell() {
  const unreadMessages = useUnreadDirectMessageCount()
  const count = unreadMessages.data?.count ?? 0

  return (
    <Link
      to="/messages"
      className="icon-button relative"
      aria-label={count ? `Tin nhắn, ${count} chưa đọc` : 'Tin nhắn'}
    >
      <EnvelopeSimple size={19} />
      {count ? (
        <span className="absolute -right-1 -top-1 grid min-h-4 min-w-4 place-items-center rounded-full bg-accent px-1 text-[10px] font-bold leading-none text-white">
          {count > 99 ? '99+' : count}
        </span>
      ) : null}
    </Link>
  )
}
