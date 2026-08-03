import {
  Bell,
  Books,
  ClockCounterClockwise,
  Eye,
  EyeSlash,
  Heart,
  LockKey,
  Prohibit,
  ShieldCheck,
  Trophy,
  UserCircle,
  UsersThree,
  type Icon as PhosphorIcon,
} from '@phosphor-icons/react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { InputField, TextareaField } from '../../components/ui/FormField'
import { useAuth } from '../../contexts/AuthContext'
import { useTheme } from '../../contexts/ThemeContext'
import { themeOptions } from '../../contexts/theme-options'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage } from '../../lib/api'
import {
  useMuteUser,
  useUnblockUser,
  useUpdateProfilePrivacy,
  useUser,
  useUserSafetyList,
} from '../../hooks/useCommunity'
import {
  useNotificationPreferences,
  useUpdateNotificationPreferences,
} from '../../hooks/useNotifications'
import { useOnboarding } from '../../hooks/useOnboarding'
import { communityService } from '../../services/community.service'
import type { UserSafetyEntry } from '../../types/domain'

export function SettingsPage() {
  const { user, refreshUser } = useAuth()
  const { theme, setTheme } = useTheme()
  const { showToast } = useToast()
  const profile = useUser(user?.id)
  const updatePrivacy = useUpdateProfilePrivacy(user?.id)
  const notificationPreferences = useNotificationPreferences()
  const updateNotificationPreferences = useUpdateNotificationPreferences()
  const safetyList = useUserSafetyList(1, 100)
  const onboarding = useOnboarding()
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState({
    displayName: user?.displayName ?? '',
    bio: user?.bio ?? '',
    avatarUrl: user?.avatarUrl ?? '',
  })
  const [privacy, setPrivacy] = useState({
    isReadingShelfPublic: false,
    isReadingActivityPublic: false,
  })
  const isReadingShelfPublic = profile.data?.privacy?.isReadingShelfPublic
  const isReadingActivityPublic = profile.data?.privacy?.isReadingActivityPublic
  const [notificationSettings, setNotificationSettings] = useState({
    isFollowNotificationEnabled: true,
    isReviewNotificationEnabled: true,
    isClubNotificationEnabled: true,
    isChallengeNotificationEnabled: true,
  })
  const followNotifications = notificationPreferences.data?.isFollowNotificationEnabled
  const reviewNotifications = notificationPreferences.data?.isReviewNotificationEnabled
  const clubNotifications = notificationPreferences.data?.isClubNotificationEnabled
  const challengeNotifications = notificationPreferences.data?.isChallengeNotificationEnabled

  useEffect(() => {
    if (isReadingShelfPublic !== undefined && isReadingActivityPublic !== undefined) {
      setPrivacy((current) => {
        if (
          current.isReadingShelfPublic === isReadingShelfPublic &&
          current.isReadingActivityPublic === isReadingActivityPublic
        ) {
          return current
        }

        return { isReadingShelfPublic, isReadingActivityPublic }
      })
    }
  }, [isReadingActivityPublic, isReadingShelfPublic])

  useEffect(() => {
    if (
      followNotifications === undefined ||
      reviewNotifications === undefined ||
      clubNotifications === undefined ||
      challengeNotifications === undefined
    ) {
      return
    }

    setNotificationSettings((current) => {
      if (
        current.isFollowNotificationEnabled === followNotifications &&
        current.isReviewNotificationEnabled === reviewNotifications &&
        current.isClubNotificationEnabled === clubNotifications &&
        current.isChallengeNotificationEnabled === challengeNotifications
      ) {
        return current
      }

      return {
        isFollowNotificationEnabled: followNotifications,
        isReviewNotificationEnabled: reviewNotifications,
        isClubNotificationEnabled: clubNotifications,
        isChallengeNotificationEnabled: challengeNotifications,
      }
    })
  }, [challengeNotifications, clubNotifications, followNotifications, reviewNotifications])

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

  const submitPrivacy = async (event: FormEvent) => {
    event.preventDefault()
    try {
      await updatePrivacy.mutateAsync(privacy)
      showToast('Quyền riêng tư hồ sơ đã được cập nhật', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật quyền riêng tư'), 'error')
    }
  }

  const submitNotificationPreferences = async (event: FormEvent) => {
    event.preventDefault()
    try {
      await updateNotificationPreferences.mutateAsync(notificationSettings)
      showToast('Tùy chọn thông báo đã được cập nhật', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể cập nhật tùy chọn thông báo'), 'error')
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
        <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-4">
            <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
              <Books size={23} weight="duotone" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-heading">Sở thích đọc</h2>
              <p className="mt-1 max-w-2xl text-sm leading-6 text-muted">
                Chọn chủ đề và các cuốn sách tham chiếu để gợi ý dành cho bạn chính xác hơn.
              </p>
              {onboarding.data ? (
                <p className="mt-2 text-xs font-semibold text-accent-strong">
                  {onboarding.data.status === 'COMPLETED'
                    ? `${onboarding.data.preferredCategoryIds.length} chủ đề, ${onboarding.data.referenceBookIds.length} sách tham chiếu`
                    : onboarding.data.status === 'SKIPPED'
                      ? 'Bạn đã để phần thiết lập này lại sau.'
                      : 'Thiết lập đang chờ hoàn thiện.'}
                </p>
              ) : null}
            </div>
          </div>
          <Link
            to="/onboarding?mode=edit"
            state={{ from: '/settings' }}
            className="button button-secondary button-md"
          >
            {onboarding.data?.status === 'COMPLETED' ? 'Chỉnh sở thích' : 'Thiết lập sở thích'}
          </Link>
        </div>
      </section>

      <section className="mt-6 surface p-5 sm:p-7">
        <div className="flex items-start gap-4 border-b border-border pb-6">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
            <Prohibit size={23} weight="duotone" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-heading">An toàn và kiểm soát nội dung</h2>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-muted">
              Quản lý người bạn đã chặn hoặc ẩn. Bỏ chặn không tự khôi phục quan hệ theo dõi trước đó.
            </p>
          </div>
        </div>

        {safetyList.isLoading ? (
          <div className="mt-6 h-28 animate-pulse rounded-2xl bg-surface-muted" />
        ) : safetyList.isError ? (
          <p className="mt-6 text-sm text-red-600" role="alert">
            Không thể tải danh sách an toàn. Hãy tải lại trang.
          </p>
        ) : safetyList.data?.items.length ? (
          <div className="mt-6 divide-y divide-border rounded-2xl border border-border">
            {safetyList.data.items.map((entry) => (
              <SafetyRow key={entry.user.id} entry={entry} />
            ))}
          </div>
        ) : (
          <div className="mt-6 rounded-2xl bg-surface-muted p-5 text-sm leading-6 text-muted">
            Bạn chưa chặn hoặc ẩn nội dung từ người đọc nào.
          </div>
        )}
      </section>

      <section className="mt-6 surface p-5 sm:p-7">
        <div className="flex items-start gap-4 border-b border-border pb-6">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
            <Bell size={23} weight="duotone" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-heading">Thông báo bạn muốn nhận</h2>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-muted">
              Tắt một nhóm sẽ ngăn các sự kiện mới thuộc nhóm đó tạo thông báo. Những thông báo cũ vẫn được giữ trong lịch sử.
            </p>
          </div>
        </div>

        {notificationPreferences.isLoading ? (
          <div className="mt-6 h-56 animate-pulse rounded-2xl bg-surface-muted" />
        ) : notificationPreferences.isError ? (
          <p className="mt-6 text-sm text-red-600" role="alert">
            Không thể tải tùy chọn thông báo. Hãy tải lại trang.
          </p>
        ) : (
          <form onSubmit={submitNotificationPreferences} className="mt-6 space-y-3">
            <PrivacyChoice
              checked={notificationSettings.isFollowNotificationEnabled}
              icon={UserCircle}
              title="Người theo dõi mới"
              description="Nhận thông báo khi một độc giả bắt đầu theo dõi bạn."
              onChange={(checked) =>
                setNotificationSettings((value) => ({
                  ...value,
                  isFollowNotificationEnabled: checked,
                }))
              }
            />
            <PrivacyChoice
              checked={notificationSettings.isReviewNotificationEnabled}
              icon={Heart}
              title="Tương tác với đánh giá"
              description="Nhận thông báo khi đánh giá của bạn có lượt thích hoặc bình luận mới."
              onChange={(checked) =>
                setNotificationSettings((value) => ({
                  ...value,
                  isReviewNotificationEnabled: checked,
                }))
              }
            />
            <PrivacyChoice
              checked={notificationSettings.isClubNotificationEnabled}
              icon={UsersThree}
              title="Câu lạc bộ và đợt đọc chung"
              description="Nhận lời mời, cập nhật thành viên, thảo luận và lời nhắc đọc chung."
              onChange={(checked) =>
                setNotificationSettings((value) => ({
                  ...value,
                  isClubNotificationEnabled: checked,
                }))
              }
            />
            <PrivacyChoice
              checked={notificationSettings.isChallengeNotificationEnabled}
              icon={Trophy}
              title="Thử thách đọc"
              description="Nhận thông báo khi tiến độ thử thách đạt cột mốc hoàn thành."
              onChange={(checked) =>
                setNotificationSettings((value) => ({
                  ...value,
                  isChallengeNotificationEnabled: checked,
                }))
              }
            />
            <div className="flex flex-col gap-3 pt-3 sm:flex-row sm:items-center sm:justify-between">
              <p className="inline-flex items-center gap-2 text-xs text-muted">
                <LockKey size={15} /> Thông báo hệ thống luôn bật để giữ các cập nhật quan trọng.
              </p>
              <Button type="submit" loading={updateNotificationPreferences.isPending}>
                Lưu tùy chọn thông báo
              </Button>
            </div>
          </form>
        )}
      </section>

      <section className="mt-6 surface p-5 sm:p-7">
        <div className="flex items-start gap-4 border-b border-border pb-6">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
            <ShieldCheck size={23} weight="duotone" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-heading">Quyền riêng tư hành trình đọc</h2>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-muted">
              Bạn kiểm soát dữ liệu đọc chi tiết xuất hiện trên hồ sơ. Email, ghi chú riêng và nội dung phiên đọc không bao giờ được công khai.
            </p>
          </div>
        </div>

        {profile.isLoading ? (
          <div className="mt-6 h-36 animate-pulse rounded-2xl bg-surface-muted" />
        ) : profile.isError ? (
          <p className="mt-6 text-sm text-red-600" role="alert">
            Không thể tải cài đặt quyền riêng tư. Hãy tải lại trang.
          </p>
        ) : (
          <form onSubmit={submitPrivacy} className="mt-6 space-y-3">
            <PrivacyChoice
              checked={privacy.isReadingShelfPublic}
              icon={Eye}
              title="Hiển thị kệ sách chi tiết"
              description="Cho phép mọi người xem sách đang đọc, đã đọc, muốn đọc và phần trăm tiến độ."
              onChange={(checked) => setPrivacy((value) => ({ ...value, isReadingShelfPublic: checked }))}
            />
            <PrivacyChoice
              checked={privacy.isReadingActivityPublic}
              icon={ClockCounterClockwise}
              title="Hiển thị dòng hoạt động trên hồ sơ"
              description="Cho phép mọi người xem các mốc tiến độ, review, bài đăng công khai và thử thách đã hoàn thành."
              onChange={(checked) => setPrivacy((value) => ({ ...value, isReadingActivityPublic: checked }))}
            />
            <div className="flex items-center justify-between gap-4 pt-3">
              <p className="inline-flex items-center gap-2 text-xs text-muted">
                <LockKey size={15} /> Tài khoản mới mặc định giữ riêng tư hai phần này.
              </p>
              <Button type="submit" loading={updatePrivacy.isPending}>
                Lưu quyền riêng tư
              </Button>
            </div>
          </form>
        )}
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

function SafetyRow({ entry }: { entry: UserSafetyEntry }) {
  const { showToast } = useToast()
  const unmute = useMuteUser(entry.user.id, true)
  const unblock = useUnblockUser(entry.user.id)

  return (
    <div className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center">
      <Avatar src={entry.user.avatarUrl} name={entry.user.displayName} size="sm" />
      <div className="min-w-0 flex-1">
        <p className="truncate font-semibold text-heading">{entry.user.displayName}</p>
        <div className="mt-1 flex flex-wrap gap-2 text-xs font-semibold">
          {entry.isBlocked ? (
            <span className="rounded-full bg-red-500/10 px-2.5 py-1 text-red-700 dark:text-red-300">
              Đã chặn
            </span>
          ) : null}
          {entry.isMuted ? (
            <span className="rounded-full bg-surface-muted px-2.5 py-1 text-muted">
              Đã ẩn nội dung
            </span>
          ) : null}
        </div>
      </div>
      <div className="flex flex-wrap gap-2 sm:justify-end">
        {entry.isMuted ? (
          <Button
            type="button"
            variant="secondary"
            size="sm"
            loading={unmute.isPending}
            icon={<EyeSlash size={16} />}
            onClick={() =>
              unmute.mutate(undefined, {
                onSuccess: () => showToast('Đã hiển thị lại nội dung', 'success'),
                onError: (error) => showToast(errorMessage(error), 'error'),
              })
            }
          >
            Bỏ ẩn
          </Button>
        ) : null}
        {entry.isBlocked ? (
          <Button
            type="button"
            variant="secondary"
            size="sm"
            loading={unblock.isPending}
            icon={<Prohibit size={16} />}
            onClick={() =>
              unblock.mutate(undefined, {
                onSuccess: () => showToast('Đã bỏ chặn người đọc', 'success'),
                onError: (error) => showToast(errorMessage(error), 'error'),
              })
            }
          >
            Bỏ chặn
          </Button>
        ) : null}
      </div>
    </div>
  )
}

function PrivacyChoice({
  checked,
  icon: Icon,
  title,
  description,
  onChange,
}: {
  checked: boolean
  icon: PhosphorIcon
  title: string
  description: string
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer items-start gap-4 rounded-2xl border border-border p-4 transition-colors hover:bg-surface-muted">
      <span className="mt-0.5 grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-surface-muted text-accent-strong">
        <Icon size={20} />
      </span>
      <span className="min-w-0 flex-1">
        <strong className="block text-sm text-heading">{title}</strong>
        <span className="mt-1 block text-sm leading-6 text-muted">{description}</span>
      </span>
      <input
        type="checkbox"
        className="mt-2 h-5 w-5 accent-[var(--accent)]"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
    </label>
  )
}
