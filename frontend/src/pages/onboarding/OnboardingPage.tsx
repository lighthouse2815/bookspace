import {
  ArrowLeft,
  ArrowRight,
  BookmarkSimple,
  Books,
  Check,
  CheckCircle,
  Flag,
  MagnifyingGlass,
  Sparkle,
  UserPlus,
  UsersThree,
  X,
} from '@phosphor-icons/react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { BookCover } from '../../components/books/BookCover'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingGrid, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import {
  useBook,
  useBookRecommendations,
  useBooks,
  useCategories,
} from '../../hooks/useCatalog'
import { useFollowUser, usePeopleSuggestions } from '../../hooks/useCommunity'
import {
  useCompleteOnboarding,
  useOnboarding,
  useSaveOnboardingPreferences,
  useSkipOnboarding,
} from '../../hooks/useOnboarding'
import { useAddToLibrary } from '../../hooks/useReading'
import { useCreateReadingGoal, useReadingGoals } from '../../hooks/useReadingProduct'
import { errorMessage } from '../../lib/api'
import { returnPathFromState } from '../../lib/navigation'
import type {
  Book,
  BookRecommendation,
  OnboardingState,
  UserDiscoveryItem,
} from '../../types/domain'

const MIN_SELECTION = 3
const MAX_SELECTION = 5
const BOOK_PAGE_SIZE = 8

type OnboardingStep = 'categories' | 'books' | 'activate'

const recommendationFallbacks: Record<BookRecommendation['reasonCode'], string> = {
  FOLLOWED_READER_LIKED: 'Một độc giả bạn theo dõi đã yêu thích cuốn này.',
  MATCHED_AUTHOR: 'Cùng tác giả với những cuốn sách bạn quan tâm.',
  MATCHED_CATEGORY: 'Phù hợp với chủ đề bạn vừa chọn.',
  POPULAR_FALLBACK: 'Đang được cộng đồng BookSpace quan tâm.',
}

function recommendationReason(recommendation: BookRecommendation) {
  return recommendation.reasonText.trim() || recommendationFallbacks[recommendation.reasonCode]
}

function validSelection(ids: string[]) {
  return ids.length >= MIN_SELECTION && ids.length <= MAX_SELECTION
}

function initialStep(state: OnboardingState, editing: boolean): OnboardingStep {
  if (editing) return 'categories'
  if (!validSelection(state.preferredCategoryIds)) return 'categories'
  if (!validSelection(state.referenceBookIds)) return 'books'
  return 'activate'
}

function OnboardingSkeleton() {
  return (
    <div className="container-page min-h-[70dvh] max-w-6xl py-8 sm:py-12" aria-label="Đang tải thiết lập sở thích">
      <div className="animate-pulse">
        <div className="h-4 w-40 rounded bg-surface-muted" />
        <div className="mt-5 h-11 max-w-xl rounded bg-surface-muted" />
        <div className="mt-3 h-5 max-w-2xl rounded bg-surface-muted" />
        <div className="mt-10 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 9 }, (_, index) => (
            <div key={index} className="h-20 rounded-2xl bg-surface-muted" />
          ))}
        </div>
      </div>
    </div>
  )
}

export function OnboardingPage() {
  const onboarding = useOnboarding()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const requestedEdit = searchParams.get('mode') === 'edit'
  const requestedReturnPath = returnPathFromState(location.state)
  const returnPath = requestedReturnPath.startsWith('/onboarding')
    ? '/dashboard'
    : requestedReturnPath

  if (onboarding.isLoading || onboarding.isPending) return <OnboardingSkeleton />

  if (onboarding.isError || !onboarding.data) {
    return (
      <div className="container-page min-h-[70dvh] max-w-3xl py-14">
        <ErrorState
          message={errorMessage(
            onboarding.error,
            'Không thể tải phần thiết lập sở thích. Vui lòng thử lại.',
          )}
          retry={() => void onboarding.refetch()}
        />
      </div>
    )
  }

  if (onboarding.data.status === 'COMPLETED' && !requestedEdit) {
    return <Navigate to={returnPath} replace />
  }

  return (
    <OnboardingExperience
      key={`${onboarding.data.status}-${requestedEdit ? 'edit' : 'setup'}`}
      initialState={onboarding.data}
      editing={requestedEdit && onboarding.data.status === 'COMPLETED'}
      returnPath={returnPath}
    />
  )
}

function OnboardingExperience({
  initialState,
  editing,
  returnPath,
}: {
  initialState: OnboardingState
  editing: boolean
  returnPath: string
}) {
  const navigate = useNavigate()
  const { showToast } = useToast()
  const categories = useCategories()
  const savePreferences = useSaveOnboardingPreferences()
  const completeOnboarding = useCompleteOnboarding()
  const skipOnboarding = useSkipOnboarding()
  const [step, setStep] = useState<OnboardingStep>(() => initialStep(initialState, editing))
  const previousStepRef = useRef(step)
  const stepHeadingRef = useRef<HTMLHeadingElement>(null)
  const [categoryIds, setCategoryIds] = useState(() => initialState.preferredCategoryIds)
  const [bookIds, setBookIds] = useState(() => initialState.referenceBookIds)
  const [searchInput, setSearchInput] = useState('')
  const [bookSearch, setBookSearch] = useState('')
  const [bookPage, setBookPage] = useState(1)
  const [pendingBookId, setPendingBookId] = useState<string | null>(null)
  const pendingBookIdRef = useRef<string | null>(null)
  const [savedBookIds, setSavedBookIds] = useState<string[]>([])
  const [goalTarget, setGoalTarget] = useState('12')
  const [goalCreated, setGoalCreated] = useState(false)

  const books = useBooks({
    search: bookSearch || undefined,
    sort: bookSearch ? undefined : 'popular',
    page: bookPage,
    pageSize: BOOK_PAGE_SIZE,
  })
  const recommendations = useBookRecommendations({ page: 1, pageSize: 6 })
  const suggestions = usePeopleSuggestions(1, 4)
  const readingGoals = useReadingGoals()
  const addToLibrary = useAddToLibrary()
  const createGoal = useCreateReadingGoal()
  const hasReadingGoal = goalCreated || Boolean(readingGoals.data?.items.length)

  useEffect(() => {
    const result = books.data
    if (!result || result.totalPages < 1 || bookPage <= result.totalPages) return
    setBookPage(result.totalPages)
  }, [bookPage, books.data])

  useEffect(() => {
    if (previousStepRef.current === step) return
    previousStepRef.current = step
    const focusTimer = window.setTimeout(() => stepHeadingRef.current?.focus({ preventScroll: true }), 0)
    return () => window.clearTimeout(focusTimer)
  }, [step])

  const selectedCategoryNames = useMemo(() => {
    const selected = new Set(categoryIds)
    return categories.data?.items.filter((category) => selected.has(category.id)) ?? []
  }, [categories.data?.items, categoryIds])

  const persist = async (nextStep: OnboardingStep) => {
    try {
      await savePreferences.mutateAsync({
        preferredCategoryIds: categoryIds,
        referenceBookIds: bookIds,
      })
      setStep(nextStep)
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu sở thích đọc'), 'error')
    }
  }

  const saveBooks = async () => {
    try {
      await savePreferences.mutateAsync({
        preferredCategoryIds: categoryIds,
        referenceBookIds: bookIds,
      })
      if (editing) {
        showToast('Đã cập nhật sở thích đọc', 'success')
        navigate(returnPath, { replace: true })
        return
      }
      setStep('activate')
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu các cuốn sách tham chiếu'), 'error')
    }
  }

  const skip = async () => {
    if (editing) {
      navigate(returnPath, { replace: true })
      return
    }
    try {
      await savePreferences.mutateAsync({
        preferredCategoryIds: categoryIds,
        referenceBookIds: bookIds,
      })
      await skipOnboarding.mutateAsync()
      showToast('Bạn có thể hoàn thiện sở thích đọc bất cứ lúc nào', 'success')
      navigate(returnPath, { replace: true })
    } catch (error) {
      showToast(errorMessage(error, 'Không thể lưu tiến độ và để sau lúc này'), 'error')
    }
  }

  const finish = async () => {
    try {
      await completeOnboarding.mutateAsync()
      showToast('Góc đọc dành riêng cho bạn đã sẵn sàng', 'success')
      navigate(returnPath, { replace: true })
    } catch (error) {
      showToast(errorMessage(error, 'Không thể hoàn tất thiết lập'), 'error')
    }
  }

  const toggleCategory = (id: string) => {
    setCategoryIds((current) =>
      current.includes(id)
        ? current.filter((item) => item !== id)
        : current.length < MAX_SELECTION
          ? [...current, id]
          : current,
    )
  }

  const toggleBook = (id: string) => {
    setBookIds((current) =>
      current.includes(id)
        ? current.filter((item) => item !== id)
        : current.length < MAX_SELECTION
          ? [...current, id]
          : current,
    )
  }

  const submitBookSearch = (event: FormEvent) => {
    event.preventDefault()
    setBookSearch(searchInput.trim())
    setBookPage(1)
  }

  const addRecommendation = async (recommendation: BookRecommendation) => {
    if (pendingBookIdRef.current || savedBookIds.includes(recommendation.book.id)) return
    pendingBookIdRef.current = recommendation.book.id
    setPendingBookId(recommendation.book.id)
    try {
      await addToLibrary.mutateAsync({ bookId: recommendation.book.id, shelf: 'WANT_TO_READ' })
      setSavedBookIds((current) => [...current, recommendation.book.id])
      showToast(`Đã thêm “${recommendation.book.title}” vào kệ Muốn đọc`, 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể thêm sách vào thư viện'), 'error')
    } finally {
      pendingBookIdRef.current = null
      setPendingBookId(null)
    }
  }

  const createFirstGoal = async () => {
    const targetValue = Number(goalTarget)
    if (!Number.isInteger(targetValue) || targetValue < 1 || targetValue > 1000) {
      showToast('Mục tiêu cần là số nguyên từ 1 đến 1000 cuốn', 'error')
      return
    }

    const year = new Date().getFullYear()
    try {
      await createGoal.mutateAsync({
        metric: 'BOOKS',
        period: 'YEAR',
        targetValue,
        startDate: new Date(`${year}-01-01T00:00:00`).toISOString(),
        endDate: new Date(`${year}-12-31T23:59:59`).toISOString(),
      })
      setGoalCreated(true)
      showToast('Đã tạo mục tiêu đọc đầu tiên', 'success')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể tạo mục tiêu đọc'), 'error')
    }
  }

  return (
    <div className="container-page min-h-[calc(100dvh-4rem)] max-w-6xl py-8 sm:py-12">
      <header className="flex flex-col gap-7 border-b border-border pb-7 sm:flex-row sm:items-start sm:justify-between">
        <div className="max-w-3xl">
          <div className="flex items-center gap-3 text-sm font-semibold text-accent-strong">
            <Books size={20} weight="duotone" aria-hidden />
            {editing ? 'Điều chỉnh sở thích đọc' : 'Cá nhân hóa BookSpace'}
          </div>
          <h1
            ref={stepHeadingRef}
            tabIndex={-1}
            className="mt-4 text-3xl font-bold leading-tight tracking-[-0.035em] text-heading outline-none sm:text-4xl"
          >
            {step === 'categories'
              ? 'Những chủ đề nào giữ bạn lại lâu hơn?'
              : step === 'books'
                ? 'Chọn vài cuốn bạn thật sự yêu thích'
                : 'Góc đọc của bạn đã có hình hài'}
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-muted sm:text-base">
            {step === 'categories'
              ? 'Chọn từ 3 đến 5 chủ đề. Bạn luôn có thể thay đổi lựa chọn trong Cài đặt.'
              : step === 'books'
                ? 'Các cuốn sách tham chiếu giúp gợi ý đầu tiên gần với gu đọc của bạn hơn.'
                : 'Lưu vài cuốn muốn đọc, kết nối với độc giả phù hợp hoặc đặt một mục tiêu nhỏ.'}
          </p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          loading={!editing && (savePreferences.isPending || skipOnboarding.isPending)}
          onClick={() => void skip()}
        >
          {editing ? 'Thoát' : 'Để sau'}
        </Button>
      </header>

      <ProgressSteps current={step} editing={editing} />

      {step === 'categories' ? (
        <section className="mt-9" aria-labelledby="category-selection-title">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <h2 id="category-selection-title" className="text-xl font-bold text-heading">
                Chủ đề bạn muốn gặp thường xuyên
              </h2>
              <p className="mt-2 text-sm text-muted" aria-live="polite">
                Đã chọn {categoryIds.length}/{MAX_SELECTION} chủ đề
              </p>
            </div>
            {selectedCategoryNames.length ? (
              <p className="max-w-xl text-right text-sm leading-6 text-muted">
                {selectedCategoryNames.map((category) => category.name).join(', ')}
              </p>
            ) : null}
          </div>

          <div className="mt-6">
            {categories.isLoading ? (
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" aria-label="Đang tải chủ đề">
                {Array.from({ length: 9 }, (_, index) => (
                  <div key={index} className="h-20 animate-pulse rounded-2xl bg-surface-muted" />
                ))}
              </div>
            ) : categories.isError ? (
              <ErrorState
                message="Không thể tải danh sách chủ đề."
                retry={() => void categories.refetch()}
              />
            ) : categories.data?.items.length ? (
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {categories.data.items.map((category) => {
                  const selected = categoryIds.includes(category.id)
                  const blocked = !selected && categoryIds.length >= MAX_SELECTION
                  return (
                    <button
                      key={category.id}
                      type="button"
                      className={`group flex min-h-20 items-center gap-4 rounded-2xl border p-4 text-left transition-[border-color,background-color,transform] focus-visible:focus-ring active:translate-y-px motion-reduce:transition-none ${
                        selected
                          ? 'border-accent bg-accent-soft text-accent-strong'
                          : 'border-border bg-surface text-heading hover:border-accent/60 hover:bg-surface-muted'
                      }`}
                      aria-pressed={selected}
                      disabled={blocked}
                      onClick={() => toggleCategory(category.id)}
                    >
                      <span
                        className={`grid h-9 w-9 shrink-0 place-items-center rounded-xl border ${
                          selected ? 'border-accent bg-accent text-white' : 'border-border bg-page text-muted'
                        }`}
                        aria-hidden
                      >
                        {selected ? <Check size={18} weight="bold" /> : <Books size={18} />}
                      </span>
                      <span className="min-w-0">
                        <strong className="block font-semibold">{category.name}</strong>
                        {typeof category.bookCount === 'number' ? (
                          <span className="mt-1 block text-xs text-muted">
                            {category.bookCount.toLocaleString('vi-VN')} cuốn sách
                          </span>
                        ) : null}
                      </span>
                    </button>
                  )
                })}
              </div>
            ) : (
              <EmptyState
                title="Chưa có chủ đề để lựa chọn"
                description="Quay lại sau khi catalog có thêm dữ liệu chủ đề."
              />
            )}
          </div>

          <StepActions
            back={editing ? () => navigate(returnPath) : undefined}
            next={() => void persist('books')}
            nextLabel="Chọn sách tham chiếu"
            nextDisabled={!validSelection(categoryIds)}
            loading={savePreferences.isPending}
            hint={
              validSelection(categoryIds)
                ? 'Lựa chọn sẽ được lưu để bạn có thể tiếp tục sau.'
                : `Chọn thêm ${Math.max(0, MIN_SELECTION - categoryIds.length)} chủ đề để tiếp tục.`
            }
          />
        </section>
      ) : null}

      {step === 'books' ? (
        <section className="mt-9" aria-labelledby="book-selection-title">
          <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_21rem] lg:items-end">
            <div>
              <h2 id="book-selection-title" className="text-xl font-bold text-heading">
                Sách định hình gu đọc của bạn
              </h2>
              <p className="mt-2 text-sm text-muted" aria-live="polite">
                Đã chọn {bookIds.length}/{MAX_SELECTION} cuốn
              </p>
            </div>
            <form onSubmit={submitBookSearch} role="search">
              <label htmlFor="onboarding-book-search" className="field-label">
                Tìm sách
              </label>
              <div className="flex gap-2">
                <div className="relative min-w-0 flex-1">
                  <MagnifyingGlass
                    size={18}
                    className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
                    aria-hidden
                  />
                  <input
                    id="onboarding-book-search"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    className="input pl-10"
                    placeholder="Tên sách, tác giả, ISBN"
                    maxLength={100}
                  />
                </div>
                <Button type="submit" variant="secondary">
                  Tìm
                </Button>
              </div>
            </form>
          </div>

          {bookIds.length ? <SelectedBooks bookIds={bookIds} onRemove={toggleBook} /> : null}

          <div className="mt-7">
            {books.isLoading ? (
              <LoadingGrid count={BOOK_PAGE_SIZE} />
            ) : books.isError ? (
              <ErrorState
                message="Không thể tải danh sách sách."
                retry={() => void books.refetch()}
              />
            ) : books.data?.items.length ? (
              <>
                <div className="grid grid-cols-2 gap-x-4 gap-y-7 sm:grid-cols-3 lg:grid-cols-4">
                  {books.data.items.map((book) => (
                    <BookChoice
                      key={book.id}
                      book={book}
                      selected={bookIds.includes(book.id)}
                      disabled={!bookIds.includes(book.id) && bookIds.length >= MAX_SELECTION}
                      onToggle={() => toggleBook(book.id)}
                    />
                  ))}
                </div>
                <Pagination
                  page={books.data.page}
                  totalPages={books.data.totalPages}
                  onPageChange={setBookPage}
                  disabled={books.isFetching}
                  className="mt-8"
                />
              </>
            ) : (
              <EmptyState
                title="Chưa tìm thấy cuốn sách phù hợp"
                description="Thử tên ngắn hơn, tên tác giả hoặc ISBN khác."
                icon={Books}
              />
            )}
          </div>

          <StepActions
            back={() => setStep('categories')}
            next={() => void saveBooks()}
            nextLabel={editing ? 'Lưu thay đổi' : 'Xem gợi ý của tôi'}
            nextDisabled={!validSelection(bookIds)}
            loading={savePreferences.isPending}
            hint={
              validSelection(bookIds)
                ? 'Bạn có thể đổi lựa chọn này trong Cài đặt.'
                : `Chọn thêm ${Math.max(0, MIN_SELECTION - bookIds.length)} cuốn để tiếp tục.`
            }
          />
        </section>
      ) : null}

      {step === 'activate' ? (
        <section className="mt-9" aria-labelledby="activation-title">
          <h2 id="activation-title" className="sr-only">
            Kích hoạt góc đọc cá nhân
          </h2>

          <section aria-labelledby="recommended-books-title">
            <div className="flex items-center gap-3">
              <span className="grid h-11 w-11 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                <Sparkle size={22} weight="fill" aria-hidden />
              </span>
              <div>
                <h3 id="recommended-books-title" className="text-xl font-bold text-heading">
                  Những cuốn nên bắt đầu
                </h3>
                <p className="mt-1 text-sm text-muted">Gợi ý mới dựa trên lựa chọn vừa lưu.</p>
              </div>
            </div>

            <div className="mt-6">
              {recommendations.isLoading ? <LoadingGrid count={6} /> : null}
              {recommendations.isError ? (
                <ErrorState
                  message="Không thể tải gợi ý sách lúc này."
                  retry={() => void recommendations.refetch()}
                />
              ) : null}
              {recommendations.data?.items.length ? (
                <div className="grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 lg:grid-cols-6">
                  {recommendations.data.items.map((recommendation) => (
                    <article key={recommendation.book.id} className="flex min-w-0 flex-col">
                      <Link
                        to={`/books/${recommendation.book.id}`}
                        className="group focus-visible:focus-ring"
                      >
                        <div className="aspect-[2/3] overflow-hidden rounded-2xl bg-surface-muted">
                          <BookCover
                            src={recommendation.book.coverImageUrl}
                            title={recommendation.book.title}
                            className="h-full w-full transition-transform duration-300 group-hover:scale-[1.025] motion-reduce:transition-none"
                          />
                        </div>
                        <h4 className="mt-3 line-clamp-2 font-semibold leading-snug text-heading group-hover:text-accent-strong">
                          {recommendation.book.title}
                        </h4>
                      </Link>
                      <p className="mt-2 line-clamp-3 text-xs leading-5 text-muted">
                        {recommendationReason(recommendation)}
                      </p>
                      <Button
                        type="button"
                        variant={savedBookIds.includes(recommendation.book.id) ? 'secondary' : 'primary'}
                        size="sm"
                        className="mt-3 w-full"
                        icon={
                          savedBookIds.includes(recommendation.book.id) ? (
                            <CheckCircle size={16} weight="fill" />
                          ) : (
                            <BookmarkSimple size={16} />
                          )
                        }
                        loading={pendingBookId === recommendation.book.id}
                        disabled={
                          savedBookIds.includes(recommendation.book.id) ||
                          (pendingBookId !== null && pendingBookId !== recommendation.book.id)
                        }
                        onClick={() => void addRecommendation(recommendation)}
                      >
                        {savedBookIds.includes(recommendation.book.id) ? 'Đã lưu' : 'Muốn đọc'}
                      </Button>
                    </article>
                  ))}
                </div>
              ) : null}
              {recommendations.data && recommendations.data.items.length === 0 ? (
                <EmptyState
                  title="Gợi ý đang được chuẩn bị"
                  description="Bạn vẫn có thể hoàn tất và quay lại mục Khám phá sau."
                  icon={Sparkle}
                />
              ) : null}
            </div>
          </section>

          <div className="mt-12 grid gap-8 border-t border-border pt-10 lg:grid-cols-[minmax(0,1.2fr)_minmax(18rem,0.8fr)]">
            <section aria-labelledby="reader-suggestions-title">
              <div className="flex items-center gap-3">
                <span className="grid h-11 w-11 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                  <UsersThree size={22} weight="duotone" aria-hidden />
                </span>
                <div>
                  <h3 id="reader-suggestions-title" className="text-xl font-bold text-heading">
                    Độc giả cùng nhịp
                  </h3>
                  <p className="mt-1 text-sm text-muted">Theo dõi để bảng tin có câu chuyện đầu tiên.</p>
                </div>
              </div>
              <div className="mt-5">
                {suggestions.isLoading ? <LoadingRows count={3} /> : null}
                {suggestions.isError ? (
                  <ErrorState
                    message="Không thể tải gợi ý độc giả."
                    retry={() => void suggestions.refetch()}
                  />
                ) : null}
                {suggestions.data?.items.length ? (
                  <div className="surface divide-y divide-border">
                    {suggestions.data.items.map((person) => (
                      <SuggestedReader key={person.id} person={person} />
                    ))}
                  </div>
                ) : null}
                {suggestions.data && suggestions.data.items.length === 0 ? (
                  <EmptyState
                    title="Bạn đã kết nối với mọi gợi ý"
                    description="Mục Độc giả luôn có thêm hồ sơ công khai để khám phá."
                    icon={UsersThree}
                  />
                ) : null}
              </div>
            </section>

            <section className="surface self-start p-5 sm:p-6" aria-labelledby="first-goal-title">
              <span className="grid h-11 w-11 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                <Flag size={22} weight="duotone" aria-hidden />
              </span>
              <h3 id="first-goal-title" className="mt-5 text-xl font-bold text-heading">
                Một cột mốc vừa sức
              </h3>
              <p className="mt-2 text-sm leading-6 text-muted">
                Tạo mục tiêu theo số cuốn cho năm nay. Đây là lựa chọn không bắt buộc.
              </p>
              {readingGoals.isLoading ? (
                <div className="mt-5 h-24 animate-pulse rounded-xl bg-surface-muted" />
              ) : readingGoals.isError ? (
                <p className="mt-5 text-sm text-red-700 dark:text-red-300" role="alert">
                  Chưa thể kiểm tra mục tiêu hiện có.
                </p>
              ) : hasReadingGoal ? (
                <div className="mt-5 rounded-xl bg-accent-soft p-4 text-sm text-accent-strong">
                  <p className="flex items-center gap-2 font-semibold">
                    <CheckCircle size={18} weight="fill" aria-hidden />
                    Bạn đã có mục tiêu đọc
                  </p>
                  <Link to="/goals" className="mt-2 inline-block font-semibold hover:underline">
                    Xem mục tiêu
                  </Link>
                </div>
              ) : (
                <div className="mt-5">
                  <label htmlFor="first-reading-goal" className="field-label">
                    Số cuốn muốn đọc
                  </label>
                  <div className="grid gap-3 sm:grid-cols-[7rem_minmax(0,1fr)] lg:grid-cols-1 xl:grid-cols-[7rem_minmax(0,1fr)]">
                    <input
                      id="first-reading-goal"
                      className="input"
                      type="number"
                      min={1}
                      max={1000}
                      step={1}
                      inputMode="numeric"
                      value={goalTarget}
                      onChange={(event) => setGoalTarget(event.target.value)}
                    />
                    <Button
                      type="button"
                      loading={createGoal.isPending}
                      icon={<Flag size={17} />}
                      onClick={() => void createFirstGoal()}
                    >
                      Tạo mục tiêu
                    </Button>
                  </div>
                </div>
              )}
            </section>
          </div>

          <div className="mt-12 flex flex-col-reverse gap-4 border-t border-border pt-7 sm:flex-row sm:items-center sm:justify-between">
            <Button type="button" variant="ghost" icon={<ArrowLeft size={17} />} onClick={() => setStep('books')}>
              Chỉnh lại lựa chọn
            </Button>
            <div className="flex flex-col items-stretch gap-3 sm:items-end">
              <p className="text-sm text-muted">Mọi lựa chọn bổ sung ở trên đều có thể bỏ qua.</p>
              <Button
                type="button"
                size="lg"
                loading={completeOnboarding.isPending}
                icon={<CheckCircle size={19} weight="fill" />}
                onClick={() => void finish()}
              >
                Hoàn tất thiết lập
              </Button>
            </div>
          </div>
        </section>
      ) : null}
    </div>
  )
}

function ProgressSteps({ current, editing }: { current: OnboardingStep; editing: boolean }) {
  const steps = editing
    ? [
        { id: 'categories' as const, label: 'Chủ đề' },
        { id: 'books' as const, label: 'Sách bạn thích' },
      ]
    : [
        { id: 'categories' as const, label: 'Chủ đề' },
        { id: 'books' as const, label: 'Sách bạn thích' },
        { id: 'activate' as const, label: 'Bắt đầu' },
      ]
  const currentIndex = steps.findIndex((item) => item.id === current)

  return (
    <ol
      className={`mt-7 grid gap-2 ${editing ? 'sm:grid-cols-2' : 'sm:grid-cols-3'}`}
      aria-label="Tiến trình thiết lập"
    >
      {steps.map((item, index) => (
        <li
          key={item.id}
          className={`flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold ${
            index === currentIndex
              ? 'bg-accent-soft text-accent-strong'
              : index < currentIndex
                ? 'text-heading'
                : 'text-muted'
          }`}
          aria-current={index === currentIndex ? 'step' : undefined}
        >
          <span
            className={`grid h-7 w-7 shrink-0 place-items-center rounded-lg border text-xs ${
              index <= currentIndex ? 'border-accent bg-accent text-white' : 'border-border bg-surface'
            }`}
            aria-hidden
          >
            {index < currentIndex ? <Check size={14} weight="bold" /> : index + 1}
          </span>
          {item.label}
        </li>
      ))}
    </ol>
  )
}

function StepActions({
  back,
  next,
  nextLabel,
  nextDisabled,
  loading,
  hint,
}: {
  back?: () => void
  next: () => void
  nextLabel: string
  nextDisabled: boolean
  loading: boolean
  hint: string
}) {
  return (
    <div className="mt-10 flex flex-col-reverse gap-4 border-t border-border pt-7 sm:flex-row sm:items-center sm:justify-between">
      {back ? (
        <Button type="button" variant="ghost" icon={<ArrowLeft size={17} />} onClick={back}>
          Quay lại
        </Button>
      ) : (
        <span />
      )}
      <div className="flex flex-col items-stretch gap-3 sm:items-end">
        <p className="text-sm text-muted" aria-live="polite">
          {hint}
        </p>
        <Button
          type="button"
          size="lg"
          icon={<ArrowRight size={18} />}
          loading={loading}
          disabled={nextDisabled}
          onClick={next}
        >
          {nextLabel}
        </Button>
      </div>
    </div>
  )
}

function BookChoice({
  book,
  selected,
  disabled,
  onToggle,
}: {
  book: Book
  selected: boolean
  disabled: boolean
  onToggle: () => void
}) {
  return (
    <button
      type="button"
      className="group min-w-0 text-left focus-visible:focus-ring disabled:cursor-not-allowed disabled:opacity-45"
      aria-pressed={selected}
      aria-label={`${selected ? 'Bỏ chọn' : 'Chọn'} ${book.title}`}
      disabled={disabled}
      onClick={onToggle}
    >
      <span
        className={`relative block aspect-[2/3] overflow-hidden rounded-2xl border-2 bg-surface-muted transition-[border-color,transform] group-active:translate-y-px motion-reduce:transition-none ${
          selected ? 'border-accent' : 'border-transparent group-hover:border-accent/50'
        }`}
      >
        <BookCover src={book.coverImageUrl} title={book.title} className="h-full w-full" />
        <span
          className={`absolute right-3 top-3 grid h-8 w-8 place-items-center rounded-xl border shadow-sm ${
            selected
              ? 'border-accent bg-accent text-white'
              : 'border-white/80 bg-slate-950/65 text-white backdrop-blur-sm'
          }`}
          aria-hidden
        >
          {selected ? <Check size={17} weight="bold" /> : <BookmarkSimple size={16} />}
        </span>
      </span>
      <strong className="mt-3 line-clamp-2 block font-semibold leading-snug text-heading group-hover:text-accent-strong">
        {book.title}
      </strong>
      <span className="mt-1 block truncate text-sm text-muted">
        {book.author?.name || 'Tác giả đang cập nhật'}
      </span>
    </button>
  )
}

function SelectedBooks({ bookIds, onRemove }: { bookIds: string[]; onRemove: (id: string) => void }) {
  return (
    <div className="mt-6 flex gap-3 overflow-x-auto pb-2" aria-label="Các sách đã chọn">
      {bookIds.map((bookId) => (
        <SelectedBook key={bookId} bookId={bookId} onRemove={() => onRemove(bookId)} />
      ))}
    </div>
  )
}

function SelectedBook({ bookId, onRemove }: { bookId: string; onRemove: () => void }) {
  const book = useBook(bookId)
  return (
    <div className="flex min-w-56 items-center gap-3 rounded-xl border border-accent/30 bg-accent-soft p-2.5">
      {book.data ? (
        <BookCover
          src={book.data.coverImageUrl}
          title={book.data.title}
          className="h-14 w-10 shrink-0 rounded-lg"
        />
      ) : (
        <span className="h-14 w-10 shrink-0 animate-pulse rounded-lg bg-surface-muted" aria-hidden />
      )}
      <span className="min-w-0 flex-1">
        <span className="line-clamp-2 text-sm font-semibold leading-5 text-heading">
          {book.data?.title || 'Đang tải tên sách'}
        </span>
      </span>
      <button
        type="button"
        className="icon-button h-8 w-8"
        aria-label={`Bỏ chọn ${book.data?.title || 'sách này'}`}
        onClick={onRemove}
      >
        <X size={16} weight="bold" />
      </button>
    </div>
  )
}

function SuggestedReader({ person }: { person: UserDiscoveryItem }) {
  const { showToast } = useToast()
  const follow = useFollowUser(person.id, person.isFollowing)

  return (
    <article className="grid gap-4 p-4 sm:grid-cols-[auto_minmax(0,1fr)_auto] sm:items-center">
      <Avatar src={person.avatarUrl} name={person.displayName} size="md" />
      <div className="min-w-0">
        <Link to={`/users/${person.id}`} className="font-semibold text-heading hover:text-accent-strong">
          {person.displayName}
        </Link>
        <p className="mt-1 line-clamp-2 text-xs leading-5 text-muted">
          {person.reasonText || 'Một hành trình đọc công khai đáng để khám phá.'}
        </p>
      </div>
      <Button
        type="button"
        variant={person.isFollowing ? 'secondary' : 'primary'}
        size="sm"
        loading={follow.isPending}
        icon={<UserPlus size={16} />}
        aria-label={`${person.isFollowing ? 'Bỏ theo dõi' : 'Theo dõi'} ${person.displayName}`}
        onClick={() =>
          follow.mutate(undefined, {
            onSuccess: () =>
              showToast(person.isFollowing ? 'Đã bỏ theo dõi' : 'Đã theo dõi độc giả này', 'success'),
            onError: (error) => showToast(errorMessage(error), 'error'),
          })
        }
      >
        {person.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
      </Button>
    </article>
  )
}
