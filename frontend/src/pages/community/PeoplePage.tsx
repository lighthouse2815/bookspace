import {
  BookOpenText,
  MagnifyingGlass,
  Sparkle,
  UserPlus,
  Users,
  UsersThree,
} from '@phosphor-icons/react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useFollowUser,
  usePeopleSearch,
  usePeopleSuggestions,
} from '../../hooks/useCommunity'
import { errorMessage } from '../../lib/api'
import type { UserDiscoveryItem } from '../../types/domain'

const PAGE_SIZE = 12
const SUGGESTION_PAGE_SIZE = 4

function positivePage(value: string | null) {
  const page = Number(value)
  return Number.isInteger(page) && page > 0 ? page : 1
}

function PeopleRow({
  person,
  showReason = false,
}: {
  person: UserDiscoveryItem
  showReason?: boolean
}) {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  const { showToast } = useToast()
  const follow = useFollowUser(person.id, person.isFollowing)

  const toggleFollow = () => {
    follow.mutate(undefined, {
      onSuccess: () =>
        showToast(
          person.isFollowing ? 'Đã bỏ theo dõi' : 'Đã theo dõi người đọc này',
          'success',
        ),
      onError: (error) => showToast(errorMessage(error), 'error'),
    })
  }

  return (
    <article className="grid gap-4 p-4 sm:grid-cols-[auto_minmax(0,1fr)_auto] sm:items-center sm:p-5">
      <Avatar src={person.avatarUrl} name={person.displayName} size="lg" />
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <Link
            to={`/users/${person.id}`}
            className="font-bold text-heading transition-colors hover:text-accent-strong focus-visible:focus-ring"
          >
            {person.displayName}
          </Link>
          {person.followsYou ? (
            <span className="text-xs font-semibold text-accent-strong">Đang theo dõi bạn</span>
          ) : null}
        </div>
        {person.bio ? (
          <p className="mt-1 line-clamp-2 max-w-2xl text-sm leading-6 text-muted">
            {person.bio}
          </p>
        ) : (
          <p className="mt-1 text-sm text-muted">Chưa có lời giới thiệu công khai.</p>
        )}
        <div className="mt-2.5 flex flex-wrap gap-x-5 gap-y-1 text-xs font-medium text-muted">
          <span className="inline-flex items-center gap-1.5">
            <Users size={15} aria-hidden />
            {person.followerCount} người theo dõi
          </span>
          <span className="inline-flex items-center gap-1.5">
            <BookOpenText size={15} aria-hidden />
            {person.booksReadCount} cuốn đã đọc
          </span>
        </div>
        {showReason ? (
          <p className="mt-3 inline-flex items-start gap-2 text-sm font-medium text-accent-strong">
            <Sparkle className="mt-0.5 shrink-0" size={16} weight="fill" aria-hidden />
            {person.reasonText}
          </p>
        ) : null}
      </div>
      <div className="flex sm:justify-end">
        {isAuthenticated ? (
          <Button
            variant={person.isFollowing ? 'secondary' : 'primary'}
            size="sm"
            loading={follow.isPending}
            disabled={follow.isPending}
            icon={<UserPlus size={17} aria-hidden />}
            onClick={toggleFollow}
          >
            {person.isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
          </Button>
        ) : (
          <Link
            to="/login"
            state={{ from: `${location.pathname}${location.search}` }}
            className="button button-secondary button-sm"
          >
            Đăng nhập để theo dõi
          </Link>
        )}
      </div>
    </article>
  )
}

export function PeoplePage() {
  const { isAuthenticated } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const searchParam = searchParams.get('search') ?? ''
  const page = positivePage(searchParams.get('page'))
  const normalizedSearch = searchParam.trim()
  const [searchInput, setSearchInput] = useState(searchParam)
  const searchIsValid =
    normalizedSearch.length === 0 ||
    (normalizedSearch.length >= 2 && normalizedSearch.length <= 100)
  const people = usePeopleSearch(normalizedSearch, page, PAGE_SIZE, searchIsValid)
  const suggestions = usePeopleSuggestions(1, SUGGESTION_PAGE_SIZE)

  useEffect(() => {
    setSearchInput(searchParam)
  }, [searchParam])

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const nextSearch = searchInput.trim()
    const next = new URLSearchParams()
    if (nextSearch) next.set('search', nextSearch)
    setSearchParams(next)
  }

  const changePage = (nextPage: number) => {
    const next = new URLSearchParams(searchParams)
    if (nextPage <= 1) next.delete('page')
    else next.set('page', String(nextPage))
    setSearchParams(next)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <div className="container-page section-space">
      <header className="max-w-3xl">
        <p className="eyebrow">Cộng đồng đọc</p>
        <h1 className="page-title mt-3">Tìm người cùng nhịp đọc</h1>
        <p className="section-copy mt-5">
          Khám phá hồ sơ công khai, theo dõi những hành trình thú vị và làm mới bảng tin
          của bạn.
        </p>
      </header>

      {isAuthenticated ? (
        <section className="mt-10" aria-labelledby="people-suggestions-title">
          <div>
            <h2 id="people-suggestions-title" className="text-2xl font-bold text-heading">
              Dành cho bạn
            </h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Gợi ý dựa trên mạng lưới theo dõi và hoạt động đọc công khai.
            </p>
          </div>
          <div className="mt-5">
            {suggestions.isLoading ? <LoadingRows count={3} /> : null}
            {suggestions.isError ? (
              <ErrorState
                message={errorMessage(
                  suggestions.error,
                  'Không thể tải gợi ý độc giả. Vui lòng thử lại.',
                )}
                retry={() => void suggestions.refetch()}
              />
            ) : null}
            {suggestions.data?.items.length ? (
              <div className="surface divide-y divide-border">
                {suggestions.data.items.map((person) => (
                  <PeopleRow key={person.id} person={person} showReason />
                ))}
              </div>
            ) : null}
            {suggestions.data && suggestions.data.items.length === 0 ? (
              <EmptyState
                title="Bạn đã kết nối với mọi gợi ý hiện có"
                description="Hãy tìm theo tên để khám phá thêm độc giả trong cộng đồng."
                icon={UsersThree}
              />
            ) : null}
          </div>
        </section>
      ) : null}

      <section className="mt-12" aria-labelledby="people-directory-title">
        <div>
          <h2 id="people-directory-title" className="text-2xl font-bold text-heading">
            Danh sách độc giả
          </h2>
          <p className="mt-2 text-sm leading-6 text-muted">
            Chỉ tìm theo tên hiển thị công khai. Email và dữ liệu thư viện riêng tư không được
            sử dụng.
          </p>
        </div>

        <form className="mt-6 max-w-2xl" onSubmit={submitSearch} role="search">
          <label htmlFor="people-search" className="field-label">
            Tên độc giả
          </label>
          <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]">
            <div>
              <input
                id="people-search"
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                className="input"
                placeholder="Ví dụ: Minh Anh"
                maxLength={101}
                aria-describedby="people-search-hint people-search-error"
              />
              <p id="people-search-hint" className="field-hint">
                Để trống để xem toàn bộ danh sách, hoặc nhập từ 2 đến 100 ký tự.
              </p>
              {!searchIsValid ? (
                <p id="people-search-error" className="field-error" role="alert">
                  Từ khóa tìm kiếm độc giả phải có từ 2 đến 100 ký tự.
                </p>
              ) : null}
            </div>
            <Button
              type="submit"
              className="sm:self-start"
              icon={<MagnifyingGlass size={18} aria-hidden />}
            >
              Tìm độc giả
            </Button>
          </div>
        </form>

        <div className="mt-7">
          {people.isLoading ? <LoadingRows count={5} /> : null}
          {people.isError ? (
            <ErrorState
              message={errorMessage(
                people.error,
                'Không thể tải danh sách độc giả. Vui lòng thử lại.',
              )}
              retry={() => void people.refetch()}
            />
          ) : null}
          {people.data?.items.length ? (
            <>
              <p className="mb-3 text-sm font-medium text-muted" aria-live="polite">
                {people.data.totalItems} độc giả phù hợp
              </p>
              <div className="surface divide-y divide-border">
                {people.data.items.map((person) => (
                  <PeopleRow key={person.id} person={person} />
                ))}
              </div>
              <Pagination
                page={people.data.page}
                totalPages={people.data.totalPages}
                onPageChange={changePage}
                disabled={people.isFetching}
                className="mt-6"
              />
            </>
          ) : null}
          {people.data && people.data.items.length === 0 ? (
            <EmptyState
              title={normalizedSearch ? 'Chưa tìm thấy độc giả phù hợp' : 'Chưa có độc giả để khám phá'}
              description={
                normalizedSearch
                  ? 'Hãy kiểm tra tên hiển thị hoặc thử một từ khóa khác.'
                  : 'Các hồ sơ công khai đang hoạt động sẽ xuất hiện tại đây.'
              }
              icon={UsersThree}
            />
          ) : null}
        </div>
      </section>
    </div>
  )
}
