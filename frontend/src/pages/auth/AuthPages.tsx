import { ArrowLeft, BookOpenText, Eye, EyeSlash } from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { InputField } from '../../components/ui/FormField'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage } from '../../lib/api'

interface FormErrors {
  displayName?: string
  email?: string
  password?: string
  confirmPassword?: string
}

function safeReturnPath(state: unknown) {
  const from = (state as { from?: unknown } | null)?.from
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//')
    ? from
    : '/dashboard'
}

function AuthShell({ children, title, copy }: { children: React.ReactNode; title: string; copy: string }) {
  return (
    <div className="container-page grid min-h-[calc(100dvh-4rem)] items-center gap-10 py-10 lg:grid-cols-2">
      <div className="hidden rounded-2xl bg-slate-950 p-10 text-white dark:bg-surface-muted dark:text-heading lg:flex lg:min-h-[36rem] lg:flex-col lg:justify-between">
        <BookOpenText size={42} weight="duotone" className="text-accent-strong" />
        <div>
          <p className="max-w-lg text-3xl font-bold leading-tight tracking-tight">
            “Một cuốn sách chỉ thật sự sống khi nó tiếp tục trong cuộc trò chuyện của người đọc.”
          </p>
          <p className="mt-5 text-sm text-slate-400">BookSpace, nơi hành trình đọc có ký ức</p>
        </div>
      </div>
      <div className="mx-auto w-full max-w-md">
        <Link to="/" className="inline-flex items-center gap-2 text-sm font-medium text-muted hover:text-heading">
          <ArrowLeft size={17} />
          Về trang chủ
        </Link>
        <h1 className="mt-8 text-3xl font-bold tracking-tight text-heading">{title}</h1>
        <p className="mt-2 text-sm leading-6 text-muted">{copy}</p>
        <div className="mt-8">{children}</div>
      </div>
    </div>
  )
}

export function LoginPage() {
  const [form, setForm] = useState({ email: '', password: '' })
  const [errors, setErrors] = useState<FormErrors>({})
  const [showPassword, setShowPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const { login, isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const navigate = useNavigate()
  const location = useLocation()
  const returnPath = safeReturnPath(location.state)

  if (isAuthenticated) return <Navigate to={returnPath} replace />

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next: FormErrors = {}
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) next.email = 'Email chưa đúng định dạng'
    if (!form.password) next.password = 'Vui lòng nhập mật khẩu'
    setErrors(next)
    if (Object.keys(next).length) return

    setSubmitting(true)
    try {
      await login(form)
      showToast('Đăng nhập thành công', 'success')
      navigate(returnPath, { replace: true })
    } catch (error) {
      showToast(errorMessage(error, 'Email hoặc mật khẩu không đúng'), 'error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthShell title="Chào mừng bạn quay lại" copy="Tiếp tục hành trình đọc từ nơi bạn đã dừng.">
      <form onSubmit={submit} className="space-y-5" noValidate>
        <InputField
          label="Email"
          name="email"
          type="email"
          autoComplete="email"
          value={form.email}
          error={errors.email}
          onChange={(event) => setForm({ ...form, email: event.target.value })}
        />
        <div className="relative">
          <InputField
            label="Mật khẩu"
            name="password"
            type={showPassword ? 'text' : 'password'}
            autoComplete="current-password"
            value={form.password}
            error={errors.password}
            className="pr-12"
            onChange={(event) => setForm({ ...form, password: event.target.value })}
          />
          <button
            type="button"
            className="absolute right-3 top-[2.15rem] rounded p-1.5 text-muted hover:text-heading focus-visible:focus-ring"
            onClick={() => setShowPassword((value) => !value)}
            aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          >
            {showPassword ? <EyeSlash size={18} /> : <Eye size={18} />}
          </button>
        </div>
        <Button type="submit" size="lg" loading={submitting} className="w-full">
          Đăng nhập
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-muted">
        Chưa có tài khoản?{' '}
        <Link
          to="/register"
          state={{ from: returnPath }}
          className="font-semibold text-accent-strong hover:underline"
        >
          Đăng ký miễn phí
        </Link>
      </p>
    </AuthShell>
  )
}

export function RegisterPage() {
  const [form, setForm] = useState({ displayName: '', email: '', password: '', confirmPassword: '' })
  const [errors, setErrors] = useState<FormErrors>({})
  const [submitting, setSubmitting] = useState(false)
  const { register, isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const navigate = useNavigate()
  const location = useLocation()
  const returnPath = safeReturnPath(location.state)

  if (isAuthenticated) return <Navigate to={returnPath} replace />

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next: FormErrors = {}
    if (form.displayName.trim().length < 2) next.displayName = 'Tên hiển thị cần ít nhất 2 ký tự'
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) next.email = 'Email chưa đúng định dạng'
    if (form.password.length < 8) next.password = 'Mật khẩu cần ít nhất 8 ký tự'
    if (form.password !== form.confirmPassword) next.confirmPassword = 'Mật khẩu xác nhận chưa khớp'
    setErrors(next)
    if (Object.keys(next).length) return

    setSubmitting(true)
    try {
      await register({
        displayName: form.displayName.trim(),
        email: form.email.trim(),
        password: form.password,
      })
      showToast('Tài khoản BookSpace đã sẵn sàng', 'success')
      navigate(returnPath, { replace: true })
    } catch (error) {
      showToast(errorMessage(error, 'Không thể tạo tài khoản'), 'error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthShell title="Tạo không gian đọc của bạn" copy="Một tài khoản độc lập cho thư viện, nhật ký và cộng đồng BookSpace.">
      <form onSubmit={submit} className="space-y-5" noValidate>
        <InputField
          label="Tên hiển thị"
          name="displayName"
          autoComplete="name"
          value={form.displayName}
          error={errors.displayName}
          onChange={(event) => setForm({ ...form, displayName: event.target.value })}
        />
        <InputField
          label="Email"
          name="email"
          type="email"
          autoComplete="email"
          value={form.email}
          error={errors.email}
          onChange={(event) => setForm({ ...form, email: event.target.value })}
        />
        <InputField
          label="Mật khẩu"
          name="password"
          type="password"
          autoComplete="new-password"
          value={form.password}
          error={errors.password}
          hint="Tối thiểu 8 ký tự."
          onChange={(event) => setForm({ ...form, password: event.target.value })}
        />
        <InputField
          label="Xác nhận mật khẩu"
          name="confirmPassword"
          type="password"
          autoComplete="new-password"
          value={form.confirmPassword}
          error={errors.confirmPassword}
          onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })}
        />
        <Button type="submit" size="lg" loading={submitting} className="w-full">
          Tạo tài khoản
        </Button>
      </form>
      <p className="mt-5 text-xs leading-5 text-muted">
        Khi đăng ký, bạn đồng ý sử dụng BookSpace như một nền tảng cộng đồng độc lập và tôn trọng nội dung của người đọc khác.
      </p>
      <p className="mt-5 text-center text-sm text-muted">
        Đã có tài khoản?{' '}
        <Link
          to="/login"
          state={{ from: returnPath }}
          className="font-semibold text-accent-strong hover:underline"
        >
          Đăng nhập
        </Link>
      </p>
    </AuthShell>
  )
}
