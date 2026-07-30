import { ArrowLeft, Compass } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="container-page grid min-h-[70dvh] place-items-center py-16 text-center">
      <div>
        <Compass size={48} weight="duotone" className="mx-auto text-accent-strong" />
        <p className="mt-6 text-sm font-semibold text-accent-strong">404</p>
        <h1 className="mt-3 text-4xl font-bold tracking-tight text-heading">Trang này không tồn tại</h1>
        <p className="mx-auto mt-4 max-w-md leading-7 text-muted">
          Liên kết có thể đã thay đổi hoặc nội dung không còn được công khai.
        </p>
        <Link to="/" className="button button-primary button-md mt-7">
          <ArrowLeft size={18} />
          Về trang chủ
        </Link>
      </div>
    </div>
  )
}
