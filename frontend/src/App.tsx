import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { AdminRoute, ProtectedRoute } from './components/routing/ProtectedRoute'

const HomePage = lazy(() =>
  import('./pages/public/HomePage').then((module) => ({ default: module.HomePage })),
)
const ExplorePage = lazy(() =>
  import('./pages/public/ExplorePage').then((module) => ({ default: module.ExplorePage })),
)
const BooksPage = lazy(() =>
  import('./pages/catalog/BooksPage').then((module) => ({ default: module.BooksPage })),
)
const BookDetailPage = lazy(() =>
  import('./pages/catalog/BookDetailPage').then((module) => ({ default: module.BookDetailPage })),
)
const BookListsPage = lazy(() =>
  import('./pages/book-lists/BookListsPage').then((module) => ({ default: module.BookListsPage })),
)
const BookListDetailPage = lazy(() =>
  import('./pages/book-lists/BookListDetailPage').then((module) => ({ default: module.BookListDetailPage })),
)
const LoginPage = lazy(() =>
  import('./pages/auth/AuthPages').then((module) => ({ default: module.LoginPage })),
)
const RegisterPage = lazy(() =>
  import('./pages/auth/AuthPages').then((module) => ({ default: module.RegisterPage })),
)
const ForgotPasswordPage = lazy(() =>
  import('./pages/auth/PasswordRecoveryPages').then((module) => ({
    default: module.ForgotPasswordPage,
  })),
)
const ResetPasswordPage = lazy(() =>
  import('./pages/auth/PasswordRecoveryPages').then((module) => ({
    default: module.ResetPasswordPage,
  })),
)
const DashboardPage = lazy(() =>
  import('./pages/account/DashboardPage').then((module) => ({ default: module.DashboardPage })),
)
const LibraryPage = lazy(() =>
  import('./pages/reading/LibraryPage').then((module) => ({ default: module.LibraryPage })),
)
const JournalPage = lazy(() =>
  import('./pages/reading/JournalPage').then((module) => ({ default: module.JournalPage })),
)
const GoalsPage = lazy(() =>
  import('./pages/reading/GoalsPage').then((module) => ({ default: module.GoalsPage })),
)
const NotesPage = lazy(() =>
  import('./pages/reading/NotesPage').then((module) => ({ default: module.NotesPage })),
)
const InsightsPage = lazy(() =>
  import('./pages/reading/InsightsPage').then((module) => ({ default: module.InsightsPage })),
)
const FeedPage = lazy(() =>
  import('./pages/community/FeedPage').then((module) => ({ default: module.FeedPage })),
)
const ProfilePage = lazy(() =>
  import('./pages/community/ProfilePage').then((module) => ({ default: module.ProfilePage })),
)
const PeoplePage = lazy(() =>
  import('./pages/community/PeoplePage').then((module) => ({ default: module.PeoplePage })),
)
const CurrentProfileRedirect = lazy(() =>
  import('./pages/community/ProfilePage').then((module) => ({
    default: module.CurrentProfileRedirect,
  })),
)
const ClubsPage = lazy(() =>
  import('./pages/clubs/ClubsPages').then((module) => ({ default: module.ClubsPage })),
)
const ClubDetailPage = lazy(() =>
  import('./pages/clubs/ClubsPages').then((module) => ({ default: module.ClubDetailPage })),
)
const CreateClubPage = lazy(() =>
  import('./pages/clubs/CreateClubPage').then((module) => ({ default: module.CreateClubPage })),
)
const ClubInvitationsPage = lazy(() =>
  import('./pages/clubs/ClubInvitationsPage').then((module) => ({
    default: module.ClubInvitationsPage,
  })),
)
const ReadingSprintPage = lazy(() =>
  import('./pages/clubs/ReadingSprintPage').then((module) => ({
    default: module.ReadingSprintPage,
  })),
)
const ChallengesPage = lazy(() =>
  import('./pages/challenges/ChallengesPage').then((module) => ({
    default: module.ChallengesPage,
  })),
)
const ChallengeDetailPage = lazy(() =>
  import('./pages/challenges/ChallengeDetailPage').then((module) => ({
    default: module.ChallengeDetailPage,
  })),
)
const NotificationsPage = lazy(() =>
  import('./pages/account/NotificationsPage').then((module) => ({
    default: module.NotificationsPage,
  })),
)
const MessagesPage = lazy(() =>
  import('./pages/messages/MessagesPage').then((module) => ({
    default: module.MessagesPage,
  })),
)
const SettingsPage = lazy(() =>
  import('./pages/account/SettingsPage').then((module) => ({ default: module.SettingsPage })),
)
const OnboardingPage = lazy(() =>
  import('./pages/onboarding/OnboardingPage').then((module) => ({
    default: module.OnboardingPage,
  })),
)
const AdminBooksPage = lazy(() =>
  import('./pages/admin/AdminBooksPage').then((module) => ({ default: module.AdminBooksPage })),
)
const AdminChallengesPage = lazy(() =>
  import('./pages/admin/AdminChallengesPage').then((module) => ({
    default: module.AdminChallengesPage,
  })),
)
const AdminModerationPage = lazy(() =>
  import('./pages/admin/AdminModerationPage').then((module) => ({
    default: module.AdminModerationPage,
  })),
)
const NotFoundPage = lazy(() =>
  import('./pages/system/NotFoundPage').then((module) => ({ default: module.NotFoundPage })),
)

function RouteLoader() {
  return (
    <div className="container-page grid min-h-[60dvh] place-items-center" aria-label="Đang tải trang">
      <div className="w-64 animate-pulse space-y-3">
        <div className="h-5 rounded bg-surface-muted" />
        <div className="mx-auto h-5 w-2/3 rounded bg-surface-muted" />
      </div>
    </div>
  )
}

export default function App() {
  return (
    <Suspense fallback={<RouteLoader />}>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<HomePage />} />
          <Route path="explore" element={<ExplorePage />} />
          <Route path="books" element={<BooksPage />} />
          <Route path="books/:id" element={<BookDetailPage />} />
          <Route path="lists/:listId" element={<BookListDetailPage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route path="forgot-password" element={<ForgotPasswordPage />} />
          <Route path="reset-password" element={<ResetPasswordPage />} />
          <Route path="users/:id" element={<ProfilePage />} />
          <Route path="people" element={<PeoplePage />} />
          <Route path="clubs" element={<ClubsPage />} />
          <Route path="clubs/:id" element={<ClubDetailPage />} />
          <Route path="clubs/:clubId/sprints/:sprintId" element={<ReadingSprintPage />} />
          <Route path="challenges" element={<ChallengesPage />} />
          <Route path="challenges/:id" element={<ChallengeDetailPage />} />

          <Route element={<ProtectedRoute />}>
            <Route path="onboarding" element={<OnboardingPage />} />
            <Route path="dashboard" element={<DashboardPage />} />
            <Route path="library" element={<LibraryPage />} />
            <Route path="lists" element={<BookListsPage />} />
            <Route path="journal" element={<JournalPage />} />
            <Route path="goals" element={<GoalsPage />} />
            <Route path="notes" element={<NotesPage />} />
            <Route path="insights" element={<InsightsPage />} />
            <Route path="clubs/new" element={<CreateClubPage />} />
            <Route path="clubs/invitations" element={<ClubInvitationsPage />} />
            <Route path="feed" element={<FeedPage />} />
            <Route path="profile" element={<CurrentProfileRedirect />} />
            <Route path="notifications" element={<NotificationsPage />} />
            <Route path="messages" element={<MessagesPage />} />
            <Route path="messages/:conversationId" element={<MessagesPage />} />
            <Route path="settings" element={<SettingsPage />} />
            <Route element={<AdminRoute />}>
              <Route path="admin" element={<Navigate to="/admin/books" replace />} />
              <Route path="admin/books" element={<AdminBooksPage />} />
              <Route path="admin/challenges" element={<AdminChallengesPage />} />
              <Route path="admin/moderation" element={<AdminModerationPage />} />
            </Route>
          </Route>

          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
