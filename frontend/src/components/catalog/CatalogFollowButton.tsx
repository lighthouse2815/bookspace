import { Check, Plus } from '@phosphor-icons/react'
import { Link } from 'react-router-dom'
import { Button } from '../ui/Button'
import { useAuth } from '../../contexts/AuthContext'
import { useToast } from '../../contexts/ToastContext'
import {
  useCatalogFollowing,
  useSetCatalogFollow,
  type CatalogFollowKind,
} from '../../hooks/useCatalog'
import { errorMessage } from '../../lib/api'

export function CatalogFollowButton({
  kind,
  id,
  compact = false,
}: {
  kind: CatalogFollowKind
  id: string
  compact?: boolean
}) {
  const { isAuthenticated } = useAuth()
  const { showToast } = useToast()
  const following = useCatalogFollowing()
  const mutation = useSetCatalogFollow()
  const isFollowing = kind === 'author'
    ? following.data?.authors.some((author) => author.id === id) ?? false
    : following.data?.categories.some((category) => category.id === id) ?? false
  const noun = kind === 'author' ? 'tác giả' : 'thể loại'

  if (!isAuthenticated) {
    return (
      <Link
        to="/login"
        state={{ from: window.location.pathname }}
        className={`button button-secondary ${compact ? 'button-sm' : 'button-md'}`}
      >
        <Plus size={16} aria-hidden />
        Theo dõi
      </Link>
    )
  }

  return (
    <Button
      type="button"
      size={compact ? 'sm' : 'md'}
      variant={isFollowing ? 'secondary' : 'primary'}
      loading={following.isLoading || mutation.isPending}
      icon={isFollowing ? <Check size={16} aria-hidden /> : <Plus size={16} aria-hidden />}
      aria-pressed={isFollowing}
      onClick={() =>
        mutation.mutate(
          { kind, id, following: !isFollowing },
          {
            onSuccess: () =>
              showToast(
                isFollowing ? `Đã bỏ theo dõi ${noun}` : `Đã theo dõi ${noun}`,
                'success',
              ),
            onError: (error) =>
              showToast(errorMessage(error, `Không thể cập nhật theo dõi ${noun}`), 'error'),
          },
        )
      }
    >
      {isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
    </Button>
  )
}
