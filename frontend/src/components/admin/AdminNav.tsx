import { Books, Flag } from '@phosphor-icons/react'
import { NavLink } from 'react-router-dom'

export function AdminNav() {
  return (
    <nav className="mb-8 flex gap-2 overflow-x-auto border-b border-border pb-3" aria-label="Điều hướng quản trị">
      <NavLink
        to="/admin/books"
        className={({ isActive }) => `filter-tab ${isActive ? 'filter-active' : ''}`}
      >
        <Books size={17} />
        Catalog sách
      </NavLink>
      <NavLink
        to="/admin/challenges"
        className={({ isActive }) => `filter-tab ${isActive ? 'filter-active' : ''}`}
      >
        <Flag size={17} />
        Thử thách
      </NavLink>
    </nav>
  )
}
