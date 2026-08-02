import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Club } from '../../types/domain'
import { ClubDetailPage } from './ClubsPages'

const mocks = vi.hoisted(() => ({
  isAuthenticated: true,
  isJoined: true,
}))

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({
    isAuthenticated: mocks.isAuthenticated,
    isLoading: false,
    user: mocks.isAuthenticated ? { id: 'reader-1' } : null,
  }),
}))

vi.mock('../../contexts/ToastContext', () => ({
  useToast: () => ({ showToast: vi.fn() }),
}))

vi.mock('../../components/clubs/ClubChatPanel', () => ({
  ClubChatPanel: ({ clubId }: { clubId: string }) => (
    <div data-testid="club-chat">Chat {clubId}</div>
  ),
}))

vi.mock('../../components/clubs/ClubManagementPanel', () => ({
  ClubManagementPanel: () => null,
}))

vi.mock('../../components/clubs/ClubRoster', () => ({
  ClubRoster: () => null,
}))

vi.mock('../../components/clubs/ReadingSprintSection', () => ({
  ReadingSprintSection: () => null,
}))

vi.mock('../../hooks/useSocialProduct', () => ({
  useClub: () => ({
    data: {
      id: 'club-1',
      name: 'Cau lac bo doc sach',
      description: 'Doc cung nhau',
      coverImageUrl: null,
      memberCount: 12,
      isPrivate: false,
      isJoined: mocks.isJoined,
      currentBook: null,
      owner: {
        id: 'owner-1',
        displayName: 'Chu nhiem',
        role: 'USER',
      },
      posts: [],
      viewerRole: mocks.isJoined ? 'MEMBER' : null,
      permissions: {
        canEdit: false,
        canInvite: false,
        canManageMembers: false,
        canManageCurrentBook: false,
        canLeave: mocks.isJoined,
      },
      createdAt: '2026-08-01T00:00:00Z',
    } satisfies Club,
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  }),
  useClubMembership: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCreateClubPost: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useClubPostComments: () => ({
    data: { items: [] },
    isLoading: false,
    isError: false,
  }),
  useCreateClubPostComment: () => ({ mutateAsync: vi.fn(), isPending: false }),
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/clubs/club-1']}>
      <Routes>
        <Route path="/clubs/:id" element={<ClubDetailPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ClubDetailPage chat access', () => {
  beforeEach(() => {
    mocks.isAuthenticated = true
    mocks.isJoined = true
  })

  it('mounts chat for an authenticated club member', () => {
    renderPage()

    expect(screen.getByTestId('club-chat')).toHaveTextContent('Chat club-1')
  })

  it.each([
    { label: 'guest', authenticated: false, joined: false },
    { label: 'non-member', authenticated: true, joined: false },
  ])('does not mount chat for a $label', ({ authenticated, joined }) => {
    mocks.isAuthenticated = authenticated
    mocks.isJoined = joined

    renderPage()

    expect(screen.queryByTestId('club-chat')).not.toBeInTheDocument()
  })
})
