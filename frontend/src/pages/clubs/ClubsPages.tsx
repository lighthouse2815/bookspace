import {
  ChatCircle,
  EnvelopeSimple,
  GearSix,
  LockSimple,
  MagnifyingGlass,
  PaperPlaneTilt,
  Plus,
  UsersThree,
} from '@phosphor-icons/react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ClubChatPanel } from '../../components/clubs/ClubChatPanel'
import { ReportContentButton } from '../../components/moderation/ReportContentButton'
import { ClubManagementPanel } from '../../components/clubs/ClubManagementPanel'
import { ClubRoster } from '../../components/clubs/ClubRoster'
import { ReadingSprintSection } from '../../components/clubs/ReadingSprintSection'
import { Avatar } from '../../components/ui/Avatar'
import { Button } from '../../components/ui/Button'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useClub,
  useClubMembership,
  useClubs,
  useCreateClubPost,
  useClubPostComments,
  useCreateClubPostComment,
} from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'
import { formatRelativeTime } from '../../lib/format'
import type { ClubPost } from '../../types/domain'

export function ClubsPage() {
  const [draft, setDraft] = useState('')
  const [search, setSearch] = useState('')
  const clubs = useClubs(search)
  const { isAuthenticated } = useAuth()

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSearch(draft.trim())
  }

  return (
    <div className="container-page section-space">
      <div className="grid gap-7 lg:grid-cols-[1fr_24rem] lg:items-end">
        <div>
          <p className="eyebrow">Cùng đọc, cùng đi xa</p>
          <h1 className="page-title mt-4">Câu lạc bộ sách</h1>
          <p className="mt-3 max-w-2xl leading-7 text-muted">
            Tìm nhóm phù hợp với thể loại, nhịp đọc và kiểu trò chuyện bạn yêu thích.
          </p>
        </div>
        <div>
          <form onSubmit={submit} className="relative">
            <label htmlFor="club-search" className="sr-only">
              Tìm câu lạc bộ
            </label>
            <MagnifyingGlass
              size={18}
              className="absolute left-3.5 top-1/2 -translate-y-1/2 text-muted"
            />
            <input
              id="club-search"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              className="input pl-11"
              placeholder="Tìm câu lạc bộ"
            />
          </form>
          {isAuthenticated ? (
            <div className="mt-3 grid grid-cols-2 gap-2">
              <Link
                to="/clubs/invitations"
                className="button button-secondary button-sm"
              >
                <EnvelopeSimple size={17} />
                Lời mời
              </Link>
              <Link to="/clubs/new" className="button button-primary button-sm">
                <Plus size={17} />
                Tạo câu lạc bộ
              </Link>
            </div>
          ) : null}
        </div>
      </div>

      <div className="mt-10">
        {clubs.isLoading ? (
          <LoadingRows count={6} />
        ) : clubs.isError ? (
          <ErrorState message="Không thể tải câu lạc bộ." retry={() => void clubs.refetch()} />
        ) : clubs.data?.items.length ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {clubs.data.items.map((club) => (
              <Link
                key={club.id}
                to={`/clubs/${club.id}`}
                className="surface group flex min-h-64 flex-col p-6 hover:border-accent/50"
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="grid h-12 w-12 place-items-center rounded-xl bg-accent-soft text-accent-strong">
                    <UsersThree size={24} weight="duotone" />
                  </div>
                  {club.isPrivate ? (
                    <span className="inline-flex items-center gap-1 text-xs font-medium text-muted">
                      <LockSimple size={14} />
                      Riêng tư
                    </span>
                  ) : null}
                </div>
                <h2 className="mt-6 text-xl font-bold text-heading group-hover:text-accent-strong">{club.name}</h2>
                <p className="mt-2 line-clamp-3 text-sm leading-6 text-muted">{club.description}</p>
                <div className="mt-auto pt-6 text-sm text-muted">
                  <strong className="text-heading">{club.memberCount}</strong> thành viên
                  {club.currentBook ? ` · Đang đọc ${club.currentBook.title}` : ''}
                </div>
              </Link>
            ))}
          </div>
        ) : (
          <EmptyState
            icon={UsersThree}
            title="Không tìm thấy câu lạc bộ"
            description="Thử một từ khóa khác để tìm nhóm phù hợp."
            action={
              search ? (
              <Button
                variant="secondary"
                onClick={() => {
                  setDraft('')
                  setSearch('')
                }}
              >
                Xóa tìm kiếm
              </Button>
              ) : isAuthenticated ? (
                <Link to="/clubs/new" className="button button-primary button-md">
                  Tạo câu lạc bộ đầu tiên
                </Link>
              ) : (
                <Link to="/login" className="button button-primary button-md">
                  Đăng nhập để tạo câu lạc bộ
                </Link>
              )
            }
          />
        )}
      </div>
    </div>
  )
}

export function ClubDetailPage() {
  const { id = '' } = useParams()
  const { isAuthenticated } = useAuth()
  const club = useClub(id)
  const membership = useClubMembership(id, club.data?.isJoined ?? false)
  const createPost = useCreateClubPost(id)
  const { showToast } = useToast()
  const navigate = useNavigate()
  const [content, setContent] = useState('')

  const toggleMembership = async () => {
    const wasJoined = club.data?.isJoined ?? false
    try {
      await membership.mutateAsync()
      showToast(wasJoined ? 'Đã rời câu lạc bộ' : 'Đã tham gia câu lạc bộ', 'success')
      if (wasJoined) navigate('/clubs', { replace: true })
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  const submitPost = async (event: FormEvent) => {
    event.preventDefault()
    if (content.trim().length < 3) return
    try {
      await createPost.mutateAsync(content.trim())
      setContent('')
      showToast('Bài viết đã được đăng', 'success')
    } catch (error) {
      showToast(errorMessage(error), 'error')
    }
  }

  if (club.isLoading) {
    return (
      <div className="container-page section-space">
        <LoadingRows count={4} />
      </div>
    )
  }
  if (club.isError || !club.data) {
    return (
      <div className="container-page section-space">
        <ErrorState message="Không thể tải câu lạc bộ." retry={() => void club.refetch()} />
      </div>
    )
  }

  return (
    <div className="container-page section-space">
      <section className="surface overflow-hidden">
        <div
          className="h-44 bg-cover bg-center"
          style={
            club.data.coverImageUrl
              ? { backgroundImage: `linear-gradient(90deg,rgba(2,6,23,.45),rgba(2,6,23,.1)),url("${club.data.coverImageUrl}")` }
              : {
                  backgroundImage:
                    'radial-gradient(circle at 20% 20%,rgba(16,185,129,.34),transparent 35%),linear-gradient(135deg,var(--surface-muted),var(--surface))',
                }
          }
        />
        <div className="p-6 sm:p-8">
          <div className="flex flex-wrap items-start justify-between gap-5">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm text-muted">
                <UsersThree size={18} />
                {club.data.memberCount} thành viên
                {club.data.isPrivate ? (
                  <>
                    <span>·</span>
                    <LockSimple size={16} />
                    Nhóm riêng tư
                  </>
                ) : null}
              </div>
              <h1 className="mt-3 text-3xl font-bold tracking-tight text-heading sm:text-4xl">{club.data.name}</h1>
              <p className="mt-4 whitespace-pre-line leading-7 text-muted">{club.data.description}</p>
            </div>
            {isAuthenticated && !club.data.isJoined && club.data.isPrivate ? (
              <Link to="/clubs/invitations" className="button button-secondary button-md">
                <EnvelopeSimple size={17} />
                Xem lời mời
              </Link>
            ) : isAuthenticated && !club.data.isJoined ? (
              <Button
                variant="primary"
                loading={membership.isPending}
                onClick={() => void toggleMembership()}
              >
                Tham gia
              </Button>
            ) : isAuthenticated && club.data.isJoined ? (
              <div className="flex flex-wrap gap-2">
                {club.data.permissions.canInvite ||
                club.data.permissions.canManageMembers ||
                club.data.permissions.canManageCurrentBook ? (
                  <a href="#club-management" className="button button-secondary button-md">
                    <GearSix size={17} />
                    Quản lý
                  </a>
                ) : null}
                {club.data.permissions.canLeave ? (
                  <Button
                    variant="secondary"
                    loading={membership.isPending}
                    onClick={() => void toggleMembership()}
                  >
                    Rời câu lạc bộ
                  </Button>
                ) : null}
              </div>
            ) : (
              <Link to="/login" className="button button-primary button-md">
                Đăng nhập để tham gia
              </Link>
            )}
          </div>
          {club.data.currentBook ? (
            <Link
              to={`/books/${club.data.currentBook.id}`}
              className="mt-7 inline-flex rounded-xl bg-accent-soft px-4 py-3 text-sm font-semibold text-accent-strong"
            >
              Đang cùng đọc: {club.data.currentBook.title}
            </Link>
          ) : null}
        </div>
      </section>

      <ClubManagementPanel club={club.data} />
      <ReadingSprintSection club={club.data} />

      <div className="mt-8 grid gap-7 lg:grid-cols-[minmax(0,1fr)_23rem] lg:items-start">
        <div className="space-y-7">
          {isAuthenticated && club.data.isJoined ? <ClubChatPanel clubId={id} /> : null}
          <section>
            <h2 className="text-xl font-bold text-heading">Thảo luận</h2>
            {club.data.isJoined ? (
              <form onSubmit={submitPost} className="mt-4 surface p-4">
                <label htmlFor="club-post" className="sr-only">
                  Viết bài trong câu lạc bộ
                </label>
                <textarea
                  id="club-post"
                  value={content}
                  onChange={(event) => setContent(event.target.value)}
                  className="input min-h-28 resize-y"
                  maxLength={3000}
                  placeholder="Bạn muốn chia sẻ điều gì với câu lạc bộ?"
                />
                <div className="mt-3 flex justify-end">
                  <Button
                    type="submit"
                    size="sm"
                    loading={createPost.isPending}
                    icon={<PaperPlaneTilt size={16} />}
                  >
                    Đăng bài
                  </Button>
                </div>
              </form>
            ) : null}
            <div className="mt-5 space-y-4">
              {club.data.posts?.length ? (
                club.data.posts.map((post) => (
                  <ClubPostDiscussion
                    key={post.id}
                    clubId={id}
                    post={post}
                    canComment={isAuthenticated && club.data.isJoined}
                  />
                ))
              ) : (
                <EmptyState
                  icon={UsersThree}
                  title="Chưa có bài thảo luận"
                  description={
                    club.data.isJoined
                      ? 'Hãy mở đầu cuộc trò chuyện bằng một câu hỏi hoặc cảm nhận.'
                      : club.data.isPrivate
                        ? 'Bạn cần chấp nhận lời mời để tham gia cuộc trò chuyện.'
                        : 'Tham gia câu lạc bộ để bắt đầu cuộc trò chuyện.'
                  }
                />
              )}
            </div>
          </section>
        </div>
        <ClubRoster club={club.data} />
      </div>
    </div>
  )
}

interface ClubPostDiscussionProps {
  clubId: string
  post: ClubPost
  canComment: boolean
}

function ClubPostDiscussion({ clubId, post, canComment }: ClubPostDiscussionProps) {
  const [showComments, setShowComments] = useState(false)
  const [draft, setDraft] = useState('')
  const comments = useClubPostComments(post.id, showComments)
  const createComment = useCreateClubPostComment(clubId, post.id)
  const { showToast } = useToast()

  const submitComment = async (event: FormEvent) => {
    event.preventDefault()
    const content = draft.trim()
    if (!content) return

    try {
      await createComment.mutateAsync(content)
      setDraft('')
    } catch (error) {
      showToast(errorMessage(error, 'Không thể đăng bình luận.'), 'error')
    }
  }

  return (
    <article className="surface p-5">
      <div className="flex gap-3">
        <Avatar src={post.author.avatarUrl} name={post.author.displayName} />
        <div>
          <Link
            to={`/users/${post.author.id}`}
            className="font-semibold text-heading hover:text-accent-strong"
          >
            {post.author.displayName}
          </Link>
          <p className="mt-0.5 text-xs text-muted">{formatRelativeTime(post.createdAt)}</p>
        </div>
      </div>
      <p className="mt-4 whitespace-pre-line text-sm leading-6 text-body">{post.content}</p>
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          icon={<ChatCircle size={17} />}
          aria-expanded={showComments}
          onClick={() => setShowComments((value) => !value)}
        >
          {post.commentCount ?? 0} bình luận
        </Button>
        <ReportContentButton
          targetType="CLUB_POST"
          targetId={post.id}
          ownerId={post.author.id}
          label="Báo cáo bài viết"
        />
      </div>

      {showComments ? (
        <div className="mt-4 border-t border-line pt-4">
          {comments.isLoading ? <p className="text-sm text-muted">Đang tải bình luận...</p> : null}
          {comments.isError ? (
            <p className="text-sm text-danger">Không thể tải bình luận. Hãy thử lại.</p>
          ) : null}
          {comments.data?.items.map((comment) => (
            <div key={comment.id} className="mb-3 flex gap-2.5 text-sm">
              <Avatar src={comment.author.avatarUrl} name={comment.author.displayName} size="sm" />
              <div className="flex min-w-0 flex-1 items-start gap-1">
                <p className="min-w-0 flex-1 leading-6 text-body">
                  <Link to={`/users/${comment.author.id}`} className="font-semibold text-heading hover:text-accent-strong">
                    {comment.author.displayName}
                  </Link>{' '}
                  {comment.content}
                </p>
                <ReportContentButton
                  targetType="CLUB_POST_COMMENT"
                  targetId={comment.id}
                  ownerId={comment.author.id}
                  label="Báo cáo bình luận"
                  compact
                />
              </div>
            </div>
          ))}
          {!comments.isLoading && !comments.isError && comments.data?.items.length === 0 ? (
            <p className="text-sm text-muted">Chưa có bình luận. Hãy mở đầu cuộc trò chuyện.</p>
          ) : null}

          {canComment ? (
            <form onSubmit={submitComment} className="mt-4 flex items-end gap-2">
              <label className="sr-only" htmlFor={`club-comment-${post.id}`}>
                Viết bình luận
              </label>
              <textarea
                id={`club-comment-${post.id}`}
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                maxLength={2000}
                className="input min-h-20 flex-1 resize-y"
                placeholder="Viết bình luận..."
              />
              <Button
                type="submit"
                size="sm"
                loading={createComment.isPending}
                icon={<PaperPlaneTilt size={16} />}
                aria-label="Đăng bình luận"
              >
                Gửi
              </Button>
            </form>
          ) : (
            <p className="mt-4 text-sm text-muted">Tham gia câu lạc bộ để bình luận.</p>
          )}
        </div>
      ) : null}
    </article>
  )
}
