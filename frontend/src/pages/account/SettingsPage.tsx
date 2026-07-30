import { UserCircle } from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { InputField, TextareaField } from '../../components/ui/FormField'
import { useAuth } from '../../contexts/AuthContext'
import { useTheme } from '../../contexts/ThemeContext'
import { themeOptions } from '../../contexts/theme-options'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage } from '../../lib/api'
import { communityService } from '../../services/community.service'

export function SettingsPage() {
  const { user, refreshUser } = useAuth()
  const { theme, setTheme } = useTheme()
  const { showToast } = useToast()
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState({
    displayName: user?.displayName ?? '',
    bio: user?.bio ?? '',
    avatarUrl: user?.avatarUrl ?? '',
  })

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (form.displayName.trim().length < 2) {
      showToast('Tên hiển thị cần ít nhất 2 ký tự', 'error')
      return
    }
    setSaving(true)
    try {
      await communityService.updateProfile({
        displayName: form.displayName.trim(),
        bio: form.bio.trim() || undefined,
        avatarUrl: form.avatarUrl.trim() || undefined,
      })
      await refreshUser()
      showToast('Hồ sơ đã được cập nhật', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật hồ sơ'), 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="container-page section-space max-w-4xl">
      <p className="eyebrow">Tài khoản của bạn</p>
      <h1 className="page-title mt-4">Cài đặt</h1>

      <section className="mt-9 surface p-5 sm:p-7">
        <div className="flex items-center gap-4 border-b border-border pb-6">
          <Avatar src={form.avatarUrl || undefined} name={form.displayName} size="lg" />
          <div>
            <h2 className="text-xl font-bold text-heading">Hồ sơ công khai</h2>
            <p className="mt-1 text-sm text-muted">Thông tin hiển thị với những người đọc khác.</p>
          </div>
        </div>
        <form onSubmit={submit} className="mt-6 grid gap-5">
          <InputField
            label="Tên hiển thị"
            name="displayName"
            value={form.displayName}
            maxLength={80}
            onChange={(event) => setForm({ ...form, displayName: event.target.value })}
          />
          <InputField
            label="URL ảnh đại diện"
            name="avatarUrl"
            type="url"
            value={form.avatarUrl}
            placeholder="https://..."
            onChange={(event) => setForm({ ...form, avatarUrl: event.target.value })}
          />
          <TextareaField
            label="Giới thiệu"
            name="bio"
            value={form.bio}
            maxLength={500}
            hint={`${form.bio.length}/500 ký tự`}
            onChange={(event) => setForm({ ...form, bio: event.target.value })}
          />
          <div className="flex justify-end">
            <Button type="submit" loading={saving} icon={<UserCircle size={18} />}>
              Lưu hồ sơ
            </Button>
          </div>
        </form>
      </section>

      <section className="mt-6 surface p-5 sm:p-7">
        <h2 className="text-xl font-bold text-heading">Giao diện</h2>
        <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
          Mỗi theme thay đổi nền, bề mặt và điểm nhấn trên toàn BookSpace. Lựa chọn được lưu trên trình duyệt này.
        </p>
        <div className="mt-5 grid gap-3 md:grid-cols-2">
          {themeOptions.map((option) => {
            const isActive = theme === option.id

            return (
              <button
                key={option.id}
                type="button"
                data-theme-option={option.id}
                className={`theme-choice ${isActive ? 'theme-choice-active' : ''}`}
                onClick={() => setTheme(option.id)}
                aria-pressed={isActive}
              >
                <span className="theme-choice-preview" aria-hidden />
                <span className="theme-choice-copy">
                  <strong>{option.name}</strong>
                  <small>{option.description}</small>
                  {isActive ? <em>Đang dùng</em> : null}
                </span>
              </button>
            )
          })}
        </div>
      </section>

      <section className="mt-6 surface p-5 sm:p-7">
        <h2 className="text-xl font-bold text-heading">Tài khoản đăng nhập</h2>
        <dl className="mt-5 grid gap-4 sm:grid-cols-2">
          <div>
            <dt className="text-xs font-semibold uppercase tracking-wider text-muted">Email</dt>
            <dd className="mt-2 font-medium text-heading">{user?.email}</dd>
          </div>
          <div>
            <dt className="text-xs font-semibold uppercase tracking-wider text-muted">Vai trò</dt>
            <dd className="mt-2 font-medium text-heading">
              {user?.role === 'ADMIN' ? 'Quản trị viên' : 'Thành viên'}
            </dd>
          </div>
        </dl>
      </section>
    </div>
  )
}
