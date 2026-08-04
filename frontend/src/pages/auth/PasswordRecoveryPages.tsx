import { CheckCircle, EnvelopeSimple, Key, WarningCircle } from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { InputField } from '../../components/ui/FormField'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage, storeTokens } from '../../lib/api'
import { authService } from '../../services/auth.service'
import { AuthShell } from './AuthPages'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const strongPasswordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,100}$/

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const { showToast } = useToast()

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalizedEmail = email.trim()
    if (!emailPattern.test(normalizedEmail)) {
      setEmailError('Email chưa đúng định dạng')
      return
    }

    setEmailError(undefined)
    setSubmitting(true)
    try {
      await authService.requestPasswordReset(normalizedEmail)
      setSubmitted(true)
    } catch (error) {
      showToast(errorMessage(error, 'Chưa thể gửi hướng dẫn đặt lại mật khẩu'), 'error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthShell
      title="Khôi phục tài khoản"
      copy="Nhập email đã dùng với BookSpace. Chúng tôi sẽ gửi một liên kết bảo mật nếu tài khoản tồn tại."
    >
      {submitted ? (
        <div className="rounded-2xl border border-border bg-surface p-6" role="status">
          <CheckCircle size={34} weight="duotone" className="text-emerald-600 dark:text-emerald-400" />
          <h2 className="mt-4 text-lg font-bold text-heading">Kiểm tra hộp thư của bạn</h2>
          <p className="mt-2 text-sm leading-6 text-muted">
            Nếu <span className="font-semibold text-heading">{email.trim()}</span> thuộc một tài khoản
            BookSpace, hướng dẫn đặt lại mật khẩu đã được gửi. Liên kết chỉ dùng được một lần và hết hạn
            sau 15 phút.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Button type="button" onClick={() => setSubmitted(false)}>
              Dùng email khác
            </Button>
            <Link to="/login" className="button button-secondary button-md">
              Về đăng nhập
            </Link>
          </div>
        </div>
      ) : (
        <form onSubmit={submit} className="space-y-5" noValidate>
          <InputField
            label="Email"
            name="email"
            type="email"
            autoComplete="email"
            value={email}
            error={emailError}
            onChange={(event) => setEmail(event.target.value)}
          />
          <Button
            type="submit"
            size="lg"
            loading={submitting}
            icon={<EnvelopeSimple size={19} />}
            className="w-full"
          >
            Gửi hướng dẫn
          </Button>
          <p className="text-center text-sm text-muted">
            Đã nhớ mật khẩu?{' '}
            <Link to="/login" className="font-semibold text-accent-strong hover:underline">
              Đăng nhập
            </Link>
          </p>
        </form>
      )}
    </AuthShell>
  )
}

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')?.trim() ?? ''
  const [form, setForm] = useState({ password: '', confirmPassword: '' })
  const [errors, setErrors] = useState<{ password?: string; confirmPassword?: string }>({})
  const [submitting, setSubmitting] = useState(false)
  const [completed, setCompleted] = useState(false)
  const { showToast } = useToast()

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const nextErrors: typeof errors = {}
    if (!strongPasswordPattern.test(form.password)) {
      nextErrors.password = 'Dùng ít nhất 8 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt'
    }
    if (form.password !== form.confirmPassword) {
      nextErrors.confirmPassword = 'Mật khẩu xác nhận chưa khớp'
    }
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return

    setSubmitting(true)
    try {
      await authService.resetPassword({ token, password: form.password })
      storeTokens(null)
      window.dispatchEvent(new Event('bookspace:session-expired'))
      setCompleted(true)
    } catch (error) {
      showToast(errorMessage(error, 'Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn'), 'error')
    } finally {
      setSubmitting(false)
    }
  }

  if (!token) {
    return (
      <AuthShell
        title="Liên kết chưa hợp lệ"
        copy="BookSpace cần mã bảo mật trong liên kết email để xác nhận yêu cầu."
      >
        <div className="rounded-2xl border border-border bg-surface p-6">
          <WarningCircle size={34} weight="duotone" className="text-amber-600 dark:text-amber-400" />
          <p className="mt-4 text-sm leading-6 text-muted">
            Liên kết đang thiếu mã đặt lại mật khẩu. Hãy mở đúng liên kết trong email hoặc tạo yêu cầu mới.
          </p>
          <Link to="/forgot-password" className="button button-primary button-md mt-6 inline-flex">
            Gửi yêu cầu mới
          </Link>
        </div>
      </AuthShell>
    )
  }

  if (completed) {
    return (
      <AuthShell
        title="Mật khẩu đã được cập nhật"
        copy="Tất cả phiên đăng nhập cũ đã hết hiệu lực để bảo vệ tài khoản của bạn."
      >
        <div className="rounded-2xl border border-border bg-surface p-6" role="status">
          <CheckCircle size={34} weight="duotone" className="text-emerald-600 dark:text-emerald-400" />
          <p className="mt-4 text-sm leading-6 text-muted">
            Bạn có thể đăng nhập lại bằng mật khẩu mới ngay bây giờ.
          </p>
          <Link to="/login" className="button button-primary button-md mt-6 inline-flex">
            Đăng nhập lại
          </Link>
        </div>
      </AuthShell>
    )
  }

  return (
    <AuthShell
      title="Đặt mật khẩu mới"
      copy="Chọn mật khẩu riêng cho BookSpace và không dùng lại mật khẩu từ dịch vụ khác."
    >
      <form onSubmit={submit} className="space-y-5" noValidate>
        <InputField
          label="Mật khẩu mới"
          name="password"
          type="password"
          autoComplete="new-password"
          value={form.password}
          error={errors.password}
          hint="Ít nhất 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt."
          onChange={(event) => setForm({ ...form, password: event.target.value })}
        />
        <InputField
          label="Xác nhận mật khẩu mới"
          name="confirmPassword"
          type="password"
          autoComplete="new-password"
          value={form.confirmPassword}
          error={errors.confirmPassword}
          onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })}
        />
        <Button
          type="submit"
          size="lg"
          loading={submitting}
          icon={<Key size={19} />}
          className="w-full"
        >
          Đặt lại mật khẩu
        </Button>
      </form>
    </AuthShell>
  )
}
