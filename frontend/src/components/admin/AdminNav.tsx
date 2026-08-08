import { Books, Flag, IdentificationCard, ShieldWarning, Tag } from '@phosphor-icons/react'
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
        to="/admin/authors"
        className={({ isActive }) => `filter-tab ${isActive ? 'filter-active' : ''}`}
      >
        <IdentificationCard size={17} />
        Tác giả
      </NavLink>
      <NavLink
        to="/admin/categories"
        className={({ isActive }) => `filter-tab ${isActive ? 'filter-active' : ''}`}
      >
        <Tag size={17} />
        Thể loại
      </NavLink>
      <NavLink
        to="/admin/challenges"
        className={({ isActive }) => `filter-tab ${isActive ? 'filter-active' : ''}`}
      >
        <Flag size={17} />
        Thử thách
      </NavLink>
      <NavLink
        to="/admin/moderation"
        className={({ isActive }) => `filter-tab ${isActive ? 'filter-active' : ''}`}
      >
        <ShieldWarning size={17} />
        Kiểm duyệt
      </NavLink>
    </nav>
  )
}
