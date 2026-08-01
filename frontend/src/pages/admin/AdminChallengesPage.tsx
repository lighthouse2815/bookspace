import { Eye, EyeSlash, NotePencil, Plus, Trash, X } from '@phosphor-icons/react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { AdminNav } from '../../components/admin/AdminNav'
import { Button } from '../../components/ui/Button'
import { InputField, TextareaField } from '../../components/ui/FormField'
import { ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { useAdminChallenges } from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import {
  adminService,
  type ChallengeAdminInput,
} from '../../services/admin.service'
import type { Challenge } from '../../types/domain'

function dateInput(value: string) {
  return value ? new Date(value).toISOString().slice(0, 10) : ''
}

function defaultChallenge(): ChallengeAdminInput {
  const now = new Date()
  const end = new Date(now)
  end.setMonth(end.getMonth() + 1)
  return {
    title: '',
    description: '',
    startDate: now.toISOString().slice(0, 10),
    endDate: end.toISOString().slice(0, 10),
    goalBooks: 3,
    coverImageUrl: '',
  }
}

export function AdminChallengesPage() {
  const challenges = useAdminChallenges()
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [editing, setEditing] = useState<Challenge | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<ChallengeAdminInput>(defaultChallenge)

  const closeForm = () => {
    setEditing(null)
    setForm(defaultChallenge())
    setShowForm(false)
  }

  const invalidateChallenges = () => {
    void queryClient.invalidateQueries({ queryKey: ['challenges'] })
    void queryClient.invalidateQueries({ queryKey: ['admin', 'challenges'] })
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
  }

  const save = useMutation({
    mutationFn: (input: ChallengeAdminInput) =>
      editing
        ? adminService.updateChallenge(editing.id, input)
        : adminService.createChallenge(input),
    onSuccess: () => {
      invalidateChallenges()
      showToast(editing ? 'Đã cập nhật thử thách' : 'Đã lưu bản nháp thử thách', 'success')
      closeForm()
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const remove = useMutation({
    mutationFn: adminService.deleteChallenge,
    onSuccess: () => {
      invalidateChallenges()
      showToast('Đã xóa thử thách', 'success')
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const publish = useMutation({
    mutationFn: ({ id, isPublished }: { id: string; isPublished: boolean }) =>
      adminService.publishChallenge(id, isPublished),
    onSuccess: (_, variables) => {
      invalidateChallenges()
      showToast(
        variables.isPublished ? 'Đã xuất bản thử thách' : 'Đã chuyển thử thách về bản nháp',
        'success',
      )
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const editChallenge = (challenge: Challenge) => {
    setEditing(challenge)
    setForm({
      title: challenge.title,
      description: challenge.description,
      startDate: dateInput(challenge.startDate),
      endDate: dateInput(challenge.endDate),
      goalBooks: challenge.goalBooks,
      coverImageUrl: challenge.coverImageUrl ?? '',
    })
    setShowForm(true)
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!form.title.trim() || !form.description.trim() || form.goalBooks < 1) {
      showToast('Điền đầy đủ tên, mô tả và mục tiêu hợp lệ', 'error')
      return
    }
    if (new Date(form.endDate) <= new Date(form.startDate)) {
      showToast('Ngày kết thúc phải sau ngày bắt đầu', 'error')
      return
    }
    save.mutate({
      ...form,
      title: form.title.trim(),
      description: form.description.trim(),
      coverImageUrl: form.coverImageUrl?.trim() || undefined,
    })
  }

  return (
    <div className="container-page section-space">
      <p className="eyebrow">Quản trị BookSpace</p>
      <div className="mt-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="page-title">Thử thách đọc</h1>
          <p className="mt-3 text-muted">Tạo và quản lý các mục tiêu dành cho cộng đồng.</p>
        </div>
        <Button
          icon={showForm ? <X size={18} /> : <Plus size={18} />}
          onClick={() => (showForm ? closeForm() : setShowForm(true))}
        >
          {showForm ? 'Đóng biểu mẫu' : 'Tạo thử thách'}
        </Button>
      </div>
      <AdminNav />

      {showForm ? (
        <form onSubmit={submit} className="mb-8 surface p-5 sm:p-7">
          <h2 className="text-xl font-bold text-heading">
            {editing ? (editing.isPublished ? 'Cập nhật nội dung đã xuất bản' : 'Chỉnh sửa bản nháp') : 'Thử thách mới'}
          </h2>
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <InputField
              label="Tên thử thách"
              name="title"
              value={form.title}
              onChange={(event) => setForm({ ...form, title: event.target.value })}
              required
            />
            <InputField
              label="URL ảnh bìa"
              name="coverImageUrl"
              type="url"
              value={form.coverImageUrl}
              onChange={(event) => setForm({ ...form, coverImageUrl: event.target.value })}
            />
            <InputField
              label="Ngày bắt đầu"
              name="startDate"
              type="date"
              value={form.startDate}
              disabled={editing?.isPublished}
              hint={editing?.isPublished ? 'Mục tiêu và lịch được khóa sau khi xuất bản.' : undefined}
              onChange={(event) => setForm({ ...form, startDate: event.target.value })}
              required
            />
            <InputField
              label="Ngày kết thúc"
              name="endDate"
              type="date"
              value={form.endDate}
              disabled={editing?.isPublished}
              onChange={(event) => setForm({ ...form, endDate: event.target.value })}
              required
            />
            <InputField
              label="Mục tiêu số cuốn"
              name="goalBooks"
              type="number"
              min={1}
              max={1000}
              value={form.goalBooks}
              disabled={editing?.isPublished}
              onChange={(event) => setForm({ ...form, goalBooks: Number(event.target.value) })}
              required
            />
            <TextareaField
              label="Mô tả"
              name="description"
              value={form.description}
              className="md:col-span-2"
              onChange={(event) => setForm({ ...form, description: event.target.value })}
              required
            />
          </div>
          <div className="mt-6 flex justify-end">
            <Button type="submit" loading={save.isPending}>
              {editing ? 'Lưu thay đổi' : 'Lưu bản nháp'}
            </Button>
          </div>
        </form>
      ) : null}

      {challenges.isLoading ? (
        <LoadingRows count={5} />
      ) : challenges.isError ? (
        <ErrorState
          message="Không thể tải danh sách thử thách."
          retry={() => void challenges.refetch()}
        />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {challenges.data?.items.map((challenge) => (
            <article key={challenge.id} className="surface p-5">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-semibold text-heading">{challenge.title}</h2>
                    <span
                      className={`rounded-full px-2.5 py-1 text-[11px] font-bold uppercase tracking-[0.12em] ${
                        challenge.isPublished
                          ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300'
                          : 'bg-amber-500/15 text-amber-700 dark:text-amber-300'
                      }`}
                    >
                      {challenge.isPublished ? 'Đã xuất bản' : 'Bản nháp'}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-muted">
                    {formatDate(challenge.startDate)} đến {formatDate(challenge.endDate)}
                  </p>
                </div>
                <div className="flex gap-1">
                  <button
                    type="button"
                    className="icon-button"
                    onClick={() => editChallenge(challenge)}
                    aria-label={`Chỉnh sửa ${challenge.title}`}
                  >
                    <NotePencil size={18} />
                  </button>
                  <button
                    type="button"
                    className="icon-button text-red-600"
                    disabled={remove.isPending || challenge.isPublished || challenge.participantCount > 0}
                    onClick={() => {
                      if (window.confirm(`Xóa thử thách "${challenge.title}"?`)) {
                        remove.mutate(challenge.id)
                      }
                    }}
                    aria-label={`Xóa ${challenge.title}`}
                  >
                    <Trash size={18} />
                  </button>
                </div>
              </div>
              <p className="mt-4 line-clamp-2 text-sm leading-6 text-muted">{challenge.description}</p>
              <div className="mt-5 flex gap-5 text-sm">
                <span>
                  <strong className="text-heading">{challenge.goalBooks}</strong> cuốn
                </span>
                <span className="text-muted">{challenge.participantCount} người tham gia</span>
              </div>
              <div className="mt-5 flex flex-wrap items-center gap-3">
                {challenge.isPublished ? (
                  challenge.participantCount === 0 ? (
                    <Button
                      size="sm"
                      variant="secondary"
                      icon={<EyeSlash size={16} />}
                      loading={publish.isPending && publish.variables?.id === challenge.id}
                      onClick={() => publish.mutate({ id: challenge.id, isPublished: false })}
                    >
                      Chuyển về nháp
                    </Button>
                  ) : (
                    <span className="text-xs text-muted">Đã có thành viên tham gia nên không thể ẩn.</span>
                  )
                ) : (
                  <Button
                    size="sm"
                    icon={<Eye size={16} />}
                    loading={publish.isPending && publish.variables?.id === challenge.id}
                    onClick={() => publish.mutate({ id: challenge.id, isPublished: true })}
                  >
                    Xuất bản
                  </Button>
                )}
                {!challenge.isPublished && challenge.participantCount === 0 ? (
                  <span className="text-xs text-muted">Có thể chỉnh sửa, xuất bản hoặc xóa bản nháp.</span>
                ) : null}
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
