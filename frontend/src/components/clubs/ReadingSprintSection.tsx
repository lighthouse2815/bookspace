import {
  Books,
  CalendarBlank,
  CheckCircle,
  FlagBanner,
  MagnifyingGlass,
  Plus,
  UsersThree,
  X,
} from '@phosphor-icons/react'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useBooks } from '../../hooks/useCatalog'
import {
  useCreateReadingSprint,
  useReadingSprints,
} from '../../hooks/useReadingSprints'
import { errorMessage } from '../../lib/api'
import {
  createDefaultSprintRange,
  formatReadingSprintDateTime,
  readingSprintStatusClass,
  readingSprintStatusLabels,
  readingSprintUnitLabels,
  readingSprintUnitNames,
} from '../../lib/reading-sprint'
import type {
  Book,
  Club,
  ReadingSprintStatus,
  ReadingSprintSummary,
  ReadingSprintTargetUnit,
} from '../../types/domain'
import { useToast } from '../../contexts/ToastContext'
import { BookCover } from '../books/BookCover'
import { Button } from '../ui/Button'
import { InputField, SelectField, TextareaField } from '../ui/FormField'
import { Pagination } from '../ui/Pagination'
import { Progress } from '../ui/Progress'
import { EmptyState, ErrorState, LoadingRows } from '../ui/States'

type SprintFilter = 'ALL' | ReadingSprintStatus

interface SprintFormState {
  title: string
  description: string
  startsAt: string
  endsAt: string
  targetUnit: ReadingSprintTargetUnit
  targetValue: string
}

function createFormState(): SprintFormState {
  return {
    title: '',
    description: '',
    targetUnit: 'PAGES',
    targetValue: '',
    ...createDefaultSprintRange(),
  }
}

const filters: ReadonlyArray<[SprintFilter, string]> = [
  ['ALL', 'Tất cả'],
  ['PLANNED', 'Sắp diễn ra'],
  ['ACTIVE', 'Đang đọc'],
  ['ENDED', 'Đã kết thúc'],
  ['COMPLETED', 'Đã tổng kết'],
  ['CANCELLED', 'Đã hủy'],
]

const SPRINT_PAGE_SIZE = 8

function SprintCard({ sprint }: { sprint: ReadingSprintSummary }) {
  const progress =
    sprint.viewerParticipation?.progressPercent ?? sprint.averageProgressPercent
  const progressLabel = sprint.viewerParticipation
    ? `Tiến độ của bạn · ${sprint.viewerParticipation.progressValue}/${sprint.targetValue} ${readingSprintUnitNames[sprint.targetUnit]}`
    : `Tiến độ trung bình · ${Math.round(sprint.averageProgressPercent)}%`

  return (
    <Link
      to={`/clubs/${sprint.clubId}/sprints/${sprint.id}`}
      className="surface group grid min-w-0 gap-5 p-5 transition-[border-color,transform] hover:-translate-y-0.5 hover:border-accent/50 motion-reduce:transform-none sm:grid-cols-[5.25rem_minmax(0,1fr)]"
    >
      <BookCover
        src={sprint.book.coverImageUrl}
        title={sprint.book.title}
        className="aspect-[2/3] w-20 rounded-xl shadow-cover sm:w-full"
      />
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span
            className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${readingSprintStatusClass(sprint.status)}`}
          >
            {readingSprintStatusLabels[sprint.status]}
          </span>
          <span className="text-xs text-muted">
            {readingSprintUnitLabels[sprint.targetUnit]} · mục tiêu {sprint.targetValue}
          </span>
        </div>
        <h4 className="mt-3 line-clamp-2 text-lg font-bold text-heading group-hover:text-accent-strong">
          {sprint.title}
        </h4>
        <p className="mt-1 line-clamp-1 text-sm text-muted">{sprint.book.title}</p>
        <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted">
          <span className="inline-flex items-center gap-1.5">
            <CalendarBlank size={14} />
            {formatReadingSprintDateTime(sprint.startsAt)}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <UsersThree size={14} />
            {sprint.participantCount} người
          </span>
        </div>
        <Progress value={progress} label={progressLabel} className="mt-4" />
      </div>
    </Link>
  )
}

function SprintGroup({
  title,
  description,
  items,
}: {
  title: string
  description: string
  items: ReadingSprintSummary[]
}) {
  if (!items.length) return null
  return (
    <div>
      <div>
        <h3 className="text-lg font-bold text-heading">{title}</h3>
        <p className="mt-1 text-sm text-muted">{description}</p>
      </div>
      <div className="mt-4 grid gap-4 xl:grid-cols-2">
        {items.map((sprint) => (
          <SprintCard key={sprint.id} sprint={sprint} />
        ))}
      </div>
    </div>
  )
}

function BookPicker({
  selected,
  onSelect,
}: {
  selected: Book | null
  onSelect: (book: Book) => void
}) {
  const [search, setSearch] = useState('')
  const [submittedSearch, setSubmittedSearch] = useState('')
  const books = useBooks({
    search: submittedSearch || undefined,
    sort: submittedSearch ? undefined : 'popular',
    page: 1,
    pageSize: 8,
  })

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    setSubmittedSearch(search.trim())
  }

  return (
    <fieldset className="md:col-span-2">
      <legend className="field-label">Sách đọc chung</legend>
      <form onSubmit={submitSearch} className="relative">
        <label htmlFor="sprint-book-search" className="sr-only">
          Tìm sách trong BookSpace
        </label>
        <MagnifyingGlass
          size={17}
          className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
        />
        <input
          id="sprint-book-search"
          className="input pl-10 pr-24"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Tìm theo tên sách hoặc tác giả"
        />
        <button
          type="submit"
          className="absolute right-1.5 top-1/2 -translate-y-1/2 rounded-lg px-3 py-1.5 text-xs font-bold text-accent-strong hover:bg-accent-soft focus-visible:focus-ring"
        >
          Tìm
        </button>
      </form>
      <p className="field-hint">
        Chọn sách từ catalog nội bộ BookSpace. Thông tin mua sách chỉ xuất hiện ở trang sách.
      </p>
      <div className="mt-3">
        {books.isLoading ? (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {Array.from({ length: 4 }, (_, index) => (
              <div key={index} className="h-28 animate-pulse rounded-xl bg-surface-muted" />
            ))}
          </div>
        ) : books.isError ? (
          <ErrorState
            message="Không thể tải danh sách sách."
            retry={() => void books.refetch()}
          />
        ) : books.data?.items.length ? (
          <div className="grid max-h-72 grid-cols-2 gap-3 overflow-y-auto pr-1 sm:grid-cols-4">
            {books.data.items.map((book) => {
              const isSelected = selected?.id === book.id
              return (
                <button
                  key={book.id}
                  type="button"
                  onClick={() => onSelect(book)}
                  className={`min-w-0 rounded-xl border p-2 text-left transition-colors focus-visible:focus-ring ${
                    isSelected
                      ? 'border-accent bg-accent-soft'
                      : 'border-border bg-surface hover:border-accent/50'
                  }`}
                  aria-pressed={isSelected}
                >
                  <div className="flex min-w-0 gap-2">
                    <BookCover
                      src={book.coverImageUrl}
                      title={book.title}
                      className="h-16 w-11 shrink-0 rounded-md"
                    />
                    <span className="min-w-0">
                      <strong className="line-clamp-2 text-xs leading-5 text-heading">
                        {book.title}
                      </strong>
                      <small className="mt-1 line-clamp-1 block text-[11px] text-muted">
                        {book.author?.name ?? 'Chưa rõ tác giả'}
                      </small>
                    </span>
                  </div>
                </button>
              )
            })}
          </div>
        ) : (
          <p className="rounded-xl bg-surface-muted p-4 text-sm text-muted">
            Không tìm thấy sách phù hợp. Hãy thử từ khóa khác.
          </p>
        )}
      </div>
      {selected ? (
        <p className="mt-3 inline-flex items-center gap-2 rounded-lg bg-accent-soft px-3 py-2 text-xs font-semibold text-accent-strong">
          <CheckCircle size={16} weight="fill" />
          Đã chọn: {selected.title}
        </p>
      ) : null}
    </fieldset>
  )
}

export function ReadingSprintSection({ club }: { club: Club }) {
  const [filter, setFilter] = useState<SprintFilter>('ALL')
  const [page, setPage] = useState(1)
  const sprintsQuery = useReadingSprints(
    club.id,
    filter === 'ALL' ? undefined : filter,
    page,
    SPRINT_PAGE_SIZE,
  )
  const createSprint = useCreateReadingSprint(club.id)
  const { showToast } = useToast()
  const navigate = useNavigate()
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<SprintFormState>(() => createFormState())
  const [selectedBook, setSelectedBook] = useState<Book | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const canManage = club.viewerRole === 'OWNER' || club.viewerRole === 'MODERATOR'
  const totalPages = Math.max(sprintsQuery.data?.totalPages ?? 1, 1)

  useEffect(() => {
    if (sprintsQuery.data && page > totalPages) setPage(totalPages)
  }, [page, sprintsQuery.data, totalPages])

  const sprints = useMemo(
    () =>
      [...(sprintsQuery.data?.items ?? [])].sort(
        (left, right) =>
          new Date(right.startsAt).getTime() - new Date(left.startsAt).getTime(),
      ),
    [sprintsQuery.data],
  )
  const visibleSprints = sprints.filter(
    (sprint) => filter === 'ALL' || sprint.status === filter,
  )
  const currentSprints = visibleSprints.filter(
    (sprint) => sprint.status === 'PLANNED' || sprint.status === 'ACTIVE',
  )
  const historySprints = visibleSprints.filter(
    (sprint) => sprint.status !== 'PLANNED' && sprint.status !== 'ACTIVE',
  )

  const closeForm = () => {
    setShowForm(false)
    setForm(createFormState())
    setSelectedBook(null)
    setErrors({})
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const targetValue = Number(form.targetValue)
    const nextErrors: Record<string, string> = {}

    if (!selectedBook) nextErrors.bookId = 'Chọn một cuốn sách cho đợt đọc.'
    if (!form.title.trim()) {
      nextErrors.title = 'Tên đợt đọc không được để trống.'
    } else if (form.title.trim().length > 200) {
      nextErrors.title = 'Tiêu đề không được vượt quá 200 ký tự.'
    }
    if (form.description.trim().length > 2000) {
      nextErrors.description = 'Mô tả không được vượt quá 2.000 ký tự.'
    }
    if (!form.startsAt) nextErrors.startsAt = 'Chọn thời gian bắt đầu.'
    if (!form.endsAt) nextErrors.endsAt = 'Chọn thời gian kết thúc.'
    if (form.startsAt && form.endsAt && form.endsAt <= form.startsAt) {
      nextErrors.endsAt = 'Thời gian kết thúc phải sau thời gian bắt đầu.'
    } else if (form.endsAt && new Date(form.endsAt).getTime() <= Date.now()) {
      nextErrors.endsAt = 'Thời gian kết thúc phải ở tương lai.'
    }
    if (!Number.isInteger(targetValue) || targetValue < 1) {
      nextErrors.targetValue = 'Mục tiêu phải là số nguyên lớn hơn 0.'
    } else if (form.targetUnit === 'CHAPTERS' && targetValue > 500) {
      nextErrors.targetValue = 'Mục tiêu theo chương không được vượt quá 500.'
    } else if (
      form.targetUnit === 'PAGES' &&
      selectedBook?.pageCount &&
      targetValue > selectedBook.pageCount
    ) {
      nextErrors.targetValue = `Sách này có ${selectedBook.pageCount} trang.`
    }

    setErrors(nextErrors)
    if (Object.keys(nextErrors).length || !selectedBook) return

    try {
      const sprint = await createSprint.mutateAsync({
        bookId: selectedBook.id,
        title: form.title.trim(),
        description: form.description.trim() || null,
        startsAt: new Date(form.startsAt).toISOString(),
        endsAt: new Date(form.endsAt).toISOString(),
        targetUnit: form.targetUnit,
        targetValue,
      })
      showToast('Đã tạo đợt đọc chung', 'success')
      closeForm()
      navigate(`/clubs/${club.id}/sprints/${sprint.id}`)
    } catch (error) {
      showToast(errorMessage(error, 'Không thể tạo đợt đọc chung'), 'error')
    }
  }

  return (
    <section className="mt-8" aria-labelledby="reading-sprints-heading">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="eyebrow">Cùng giữ nhịp</p>
          <h2 id="reading-sprints-heading" className="mt-2 text-2xl font-bold text-heading">
            Đợt đọc chung
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
            Chọn một cuốn sách, cùng tiến đến từng cột mốc và lưu lại cuộc trò chuyện của nhóm.
          </p>
        </div>
        {canManage ? (
          <Button
            icon={showForm ? <X size={18} /> : <Plus size={18} />}
            variant={showForm ? 'secondary' : 'primary'}
            onClick={() => {
              if (showForm) closeForm()
              else setShowForm(true)
            }}
          >
            {showForm ? 'Đóng biểu mẫu' : 'Tạo đợt đọc'}
          </Button>
        ) : null}
      </div>

      {showForm ? (
        <form onSubmit={submit} className="mt-6 surface p-5 sm:p-7" noValidate>
          <div className="flex items-start gap-3">
            <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent-strong">
              <FlagBanner size={22} weight="duotone" />
            </div>
            <div>
              <h3 className="text-lg font-bold text-heading">Thiết kế nhịp đọc mới</h3>
              <p className="mt-1 text-sm text-muted">
                Thành viên có thể tham gia, check-in tiến độ tuyệt đối và thảo luận theo từng mốc.
              </p>
            </div>
          </div>

          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <BookPicker
              selected={selectedBook}
              onSelect={(book) => {
                setSelectedBook(book)
                setErrors((current) => ({ ...current, bookId: '' }))
                if (!form.targetValue && book.pageCount) {
                  setForm((current) => ({
                    ...current,
                    targetUnit: 'PAGES',
                    targetValue: String(book.pageCount),
                  }))
                }
              }}
            />
            {errors.bookId ? (
              <p className="-mt-3 field-error md:col-span-2">{errors.bookId}</p>
            ) : null}
            <InputField
              label="Tên đợt đọc"
              name="sprint-title"
              value={form.title}
              maxLength={200}
              error={errors.title}
              placeholder="Ví dụ: Hai tuần cùng đọc Rừng Na Uy"
              onChange={(event) => {
                setForm({ ...form, title: event.target.value })
                setErrors({ ...errors, title: '' })
              }}
              required
            />
            <div className="grid grid-cols-[minmax(0,1fr)_minmax(7rem,0.65fr)] gap-3">
              <SelectField
                label="Đơn vị mục tiêu"
                name="sprint-target-unit"
                value={form.targetUnit}
                onChange={(event) =>
                  setForm({
                    ...form,
                    targetUnit: event.target.value as ReadingSprintTargetUnit,
                  })
                }
              >
                <option value="PAGES">Trang</option>
                <option value="CHAPTERS">Chương</option>
              </SelectField>
              <InputField
                label="Mục tiêu"
                name="sprint-target"
                type="number"
                min={1}
                step={1}
                max={
                  form.targetUnit === 'CHAPTERS'
                    ? 500
                    : selectedBook?.pageCount
                }
                inputMode="numeric"
                value={form.targetValue}
                error={errors.targetValue}
                onChange={(event) => {
                  setForm({ ...form, targetValue: event.target.value })
                  setErrors({ ...errors, targetValue: '' })
                }}
                required
              />
            </div>
            <InputField
              label="Bắt đầu"
              name="sprint-start"
              type="datetime-local"
              value={form.startsAt}
              error={errors.startsAt}
              onChange={(event) => {
                setForm({ ...form, startsAt: event.target.value })
                setErrors({ ...errors, startsAt: '' })
              }}
              required
            />
            <InputField
              label="Kết thúc"
              name="sprint-end"
              type="datetime-local"
              value={form.endsAt}
              error={errors.endsAt}
              onChange={(event) => {
                setForm({ ...form, endsAt: event.target.value })
                setErrors({ ...errors, endsAt: '' })
              }}
              required
            />
            <TextareaField
              label="Mô tả"
              name="sprint-description"
              className="md:min-h-24"
              value={form.description}
              maxLength={2000}
              error={errors.description}
              hint={`${form.description.length}/2.000 ký tự`}
              placeholder="Nhịp đọc, quy ước thảo luận hoặc điều nhóm muốn đạt được."
              onChange={(event) => {
                setForm({ ...form, description: event.target.value })
                setErrors({ ...errors, description: '' })
              }}
            />
            <div className="flex items-end gap-3">
              <Button
                type="submit"
                loading={createSprint.isPending}
                icon={<FlagBanner size={18} />}
              >
                Tạo đợt đọc
              </Button>
              <Button type="button" variant="ghost" onClick={closeForm}>
                Hủy
              </Button>
            </div>
          </div>
        </form>
      ) : null}

      <div
        className="mt-7 flex gap-2 overflow-x-auto pb-2"
        aria-label="Lọc đợt đọc theo trạng thái"
      >
        {filters.map(([value, label]) => (
          <button
            key={value}
            type="button"
            className={`filter-tab ${filter === value ? 'filter-active' : ''}`}
            onClick={() => {
              setFilter(value)
              setPage(1)
            }}
            aria-pressed={filter === value}
          >
            {label}
          </button>
        ))}
      </div>

      <div className="mt-5">
        {sprintsQuery.isLoading ? (
          <LoadingRows count={4} />
        ) : sprintsQuery.isError ? (
          <ErrorState
            message="Không thể tải các đợt đọc chung."
            retry={() => void sprintsQuery.refetch()}
          />
        ) : visibleSprints.length ? (
          <div className="space-y-9">
            {filter === 'ALL' ? (
              <>
                <SprintGroup
                  title="Đang diễn ra và sắp tới"
                  description="Những hành trình nhóm đang cùng chuẩn bị hoặc tiếp tục."
                  items={currentSprints}
                />
                <SprintGroup
                  title="Lịch sử đợt đọc"
                  description="Các đợt đã kết thúc, tổng kết hoặc hủy."
                  items={historySprints}
                />
              </>
            ) : (
              <SprintGroup
                title={readingSprintStatusLabels[filter]}
                description={`${sprintsQuery.data?.totalItems ?? visibleSprints.length} đợt đọc ở trạng thái này.`}
                items={visibleSprints}
              />
            )}
          </div>
        ) : (
          <EmptyState
            icon={filter === 'ALL' ? Books : CalendarBlank}
            title={
              filter === 'ALL'
                ? 'Chưa có đợt đọc chung'
                : 'Không có đợt đọc ở trạng thái này'
            }
            description={
              canManage && filter === 'ALL'
                ? 'Tạo hành trình đầu tiên để cả nhóm có một mục tiêu và nhịp thảo luận chung.'
                : 'Hãy đổi bộ lọc để xem những đợt đọc khác của câu lạc bộ.'
            }
            action={
              canManage && filter === 'ALL' ? (
                <Button
                  icon={<Plus size={18} />}
                  onClick={() => setShowForm(true)}
                >
                  Tạo đợt đọc đầu tiên
                </Button>
              ) : undefined
            }
          />
        )}
      </div>
      <Pagination
        page={page}
        totalPages={totalPages}
        disabled={sprintsQuery.isFetching}
        onPageChange={setPage}
        className="mt-5 border-t border-border pt-5"
      />
    </section>
  )
}
