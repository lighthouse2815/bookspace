export type UserRole = 'USER' | 'ADMIN'
export type Shelf = 'WANT_TO_READ' | 'READING' | 'READ'
export type ReadingGoalMetric = 'BOOKS' | 'PAGES' | 'MINUTES'
export type ReadingGoalPeriod = 'WEEK' | 'MONTH' | 'YEAR' | 'CUSTOM'
export type ReadingGoalStatus = 'ACTIVE' | 'COMPLETED' | 'EXPIRED'

export interface User {
  id: string
  email?: string | null
  displayName: string
  bio?: string
  avatarUrl?: string
  role: UserRole
  followerCount?: number
  followingCount?: number
  booksReadCount?: number
  isFollowing?: boolean
  joinedAt?: string
}

export interface UserDiscoveryItem {
  id: string
  displayName: string
  bio?: string
  avatarUrl?: string
  followerCount: number
  booksReadCount: number
  isFollowing: boolean
  followsYou: boolean
  mutualFollowCount: number
  reason: string
  reasonText: string
}

export interface AuthTokens {
  accessToken: string
  refreshToken: string
}

export interface AuthSession extends AuthTokens {
  user: User
}

export interface Author {
  id: string
  name: string
  biography?: string
  avatarUrl?: string
  bookCount?: number
}

export interface Category {
  id: string
  name: string
  slug?: string
  bookCount?: number
}

export interface Book {
  id: string
  title: string
  description?: string
  isbn?: string
  coverImageUrl?: string
  pageCount?: number
  publishedYear?: number
  publisher?: string
  language?: string
  averageRating?: number
  reviewCount?: number
  author?: Author
  authorId?: string
  categories?: Category[]
  shelf?: Shelf | null
  externalOffer?: {
    providerName: string
    purchaseUrl: string
    price?: number
    currency?: string
  } | null
}

export interface LibraryEntry {
  id: string
  userId: string
  bookId: string
  book: Book
  shelf: Shelf
  currentPage: number
  progressPercent: number
  startedAt?: string
  finishedAt?: string
  updatedAt: string
}

export interface ReadingSession {
  id: string
  bookId: string
  book?: Book
  startedAt: string
  endedAt?: string
  durationMinutes: number
  pagesRead: number
  note?: string
  createdAt: string
}

export interface ReadingGoal {
  id: string
  metric: ReadingGoalMetric
  period: ReadingGoalPeriod
  targetValue: number
  currentValue: number
  progressPercent: number
  startDate: string
  endDate: string
  status: ReadingGoalStatus
  completedAt?: string
  createdAt: string
  updatedAt?: string
}

export interface ReadingNote {
  id: string
  bookId: string
  book?: Book
  pageNumber?: number
  quote?: string
  content?: string
  tags: string[]
  createdAt: string
  updatedAt?: string
}

export interface ReadingInsightGoalSummary {
  total: number
  active: number
  completed: number
  expired: number
}

export interface ReadingFinishForecast {
  libraryItemId: string
  bookId: string
  title: string
  coverImageUrl: string | null
  currentPage: number
  pageCount: number
  remainingPages: number
  averagePagesPerDay: number
  estimatedDaysRemaining: number | null
  estimatedFinishDate: string | null
}

export interface ReadingInsightComparisonValue {
  current: number
  previous: number
  changePercent: number | null
}

export interface ReadingInsightComparison {
  currentFromDate: string
  currentToDate: string
  previousFromDate: string
  previousToDate: string
  sessions: ReadingInsightComparisonValue
  pages: ReadingInsightComparisonValue
  minutes: ReadingInsightComparisonValue
  activeDays: ReadingInsightComparisonValue
  booksFinished: ReadingInsightComparisonValue
}

export interface ReadingGoalForecast {
  goalId: string
  metric: ReadingGoalMetric
  targetValue: number
  currentValue: number
  remainingValue: number
  startDate: string
  endDate: string
  averagePerDay: number
  estimatedFinishDate: string | null
  isOnTrack: boolean | null
}

export interface ReadingInsightsOverview {
  utcOffsetMinutes: number
  days: number
  fromDate: string
  toDate: string
  totalSessions: number
  totalPages: number
  totalMinutes: number
  booksFinished: number
  activeDays: number
  averagePagesPerActiveDay: number
  averageMinutesPerActiveDay: number
  averageSessionsPerActiveDay: number
  currentStreak: number
  longestStreak: number
  goals: ReadingInsightGoalSummary
  comparison: ReadingInsightComparison
  forecasts: ReadingFinishForecast[]
  goalForecasts: ReadingGoalForecast[]
}

export interface ReadingCalendarDay {
  date: string
  sessionCount: number
  pagesRead: number
  minutesRead: number
  isActive: boolean
}

export interface ReadingInsightsCalendar {
  utcOffsetMinutes: number
  year: number | null
  days: number
  fromDate: string
  toDate: string
  activeDays: number
  totalSessions: number
  totalPages: number
  totalMinutes: number
  daysData: ReadingCalendarDay[]
}

export interface ReadingInsightsWeek {
  weekStart: string
  weekEnd: string
  sessions: number
  pages: number
  minutes: number
  activeDays: number
  booksFinished: number
  averagePagesPerActiveDay: number
  averageMinutesPerActiveDay: number
}

export interface ReadingInsightsWeekly {
  utcOffsetMinutes: number
  weeks: number
  fromDate: string
  toDate: string
  items: ReadingInsightsWeek[]
}

export interface ReadingInsightsMonth {
  monthStart: string
  monthEnd: string
  sessions: number
  pages: number
  minutes: number
  activeDays: number
  booksFinished: number
  averagePagesPerActiveDay: number
  averageMinutesPerActiveDay: number
}

export interface ReadingInsightsMonthly {
  utcOffsetMinutes: number
  months: number
  fromDate: string
  toDate: string
  items: ReadingInsightsMonth[]
}

export interface ReviewComment {
  id: string
  reviewId: string
  user: User
  content: string
  createdAt: string
}

export interface Review {
  id: string
  bookId: string
  book?: Book
  user: User
  rating: number
  content: string
  containsSpoilers: boolean
  likeCount: number
  commentCount: number
  likedByCurrentUser: boolean
  comments?: ReviewComment[]
  createdAt: string
  updatedAt?: string
}

export interface FeedItem {
  id: string
  type: 'REVIEW' | 'READING_PROGRESS' | 'CHALLENGE' | 'CLUB_POST'
  actor: User
  review?: Review
  book?: Book
  club?: Club
  challenge?: Challenge
  content?: string
  progressPercent?: number
  createdAt: string
}

export interface ClubPost {
  id: string
  clubId: string
  author: User
  content: string
  likeCount?: number
  commentCount?: number
  createdAt: string
}

export interface ClubPostComment {
  id: string
  postId: string
  author: User
  content: string
  createdAt: string
}

export type ClubMemberRole = 'OWNER' | 'MODERATOR' | 'MEMBER'
export type ClubInvitationStatus = 'PENDING' | 'ACCEPTED' | 'DECLINED' | 'REVOKED' | 'EXPIRED'

export interface ClubPermissions {
  canEdit: boolean
  canInvite: boolean
  canManageMembers: boolean
  canManageCurrentBook: boolean
  canLeave: boolean
}

export interface Club {
  id: string
  name: string
  description: string | null
  coverImageUrl: string | null
  memberCount: number
  isPrivate: boolean
  isJoined: boolean
  currentBook: Book | null
  owner: User
  posts: ClubPost[] | null
  viewerRole: ClubMemberRole | null
  permissions: ClubPermissions
  createdAt: string
}

export interface ClubMember {
  id: string
  user: User
  role: ClubMemberRole
  joinedAt: string
}

export interface ClubInvitation {
  id: string
  club: Club
  inviter: User
  invitedUser: User
  status: ClubInvitationStatus
  expiresAt: string
  respondedAt: string | null
  createdAt: string
}

export type ReadingSprintTargetUnit = 'PAGES' | 'CHAPTERS'
export type ReadingSprintStatus =
  | 'PLANNED'
  | 'ACTIVE'
  | 'ENDED'
  | 'COMPLETED'
  | 'CANCELLED'

export interface ReadingSprintPermissions {
  canManage: boolean
  canJoin: boolean
  canLeave: boolean
  canCheckIn: boolean
  canDiscuss: boolean
  canSendReminder: boolean
}

export interface ReadingSprintParticipant {
  id: string
  user: User
  progressValue: number
  progressPercent: number
  rank: number
  joinedAt: string
  leftAt: string | null
  completedAt: string | null
  lastCheckInAt: string | null
  isActive: boolean
}

export interface ReadingSprintCheckIn {
  id: string
  user: User
  progressValue: number
  progressPercent: number
  note: string | null
  createdAt: string
}

export interface ReadingSprintMilestoneResponse {
  id: string
  milestoneId: string
  author: User
  content: string
  canDelete: boolean
  createdAt: string
}

export interface ReadingSprintMilestone {
  id: string
  title: string
  description: string | null
  targetValue: number
  reachedByViewer: boolean
  responseCount: number
  createdAt: string
}

export interface ReadingSprintSummary {
  id: string
  clubId: string
  title: string
  description: string | null
  book: Book
  startsAt: string
  endsAt: string
  targetUnit: ReadingSprintTargetUnit
  targetValue: number
  status: ReadingSprintStatus
  participantCount: number
  completedCount: number
  averageProgressPercent: number
  viewerParticipation: ReadingSprintParticipant | null
  permissions: ReadingSprintPermissions
  createdBy: User
  completedAt: string | null
  cancelledAt: string | null
  lastReminderAt: string | null
  createdAt: string
}

export interface ReadingSprintDetail extends ReadingSprintSummary {
  milestones: ReadingSprintMilestone[]
}

export interface Challenge {
  id: string
  title: string
  description: string
  startDate: string
  endDate: string
  goalBooks: number
  currentBooks: number
  participantCount: number
  isJoined: boolean
  coverImageUrl?: string
  isPublished: boolean
  completedAt?: string
}

export interface Notification {
  id: string
  type: 'FOLLOW' | 'REVIEW_LIKE' | 'COMMENT' | 'CLUB' | 'CHALLENGE' | 'SYSTEM'
  title: string
  message: string
  link?: string
  isRead: boolean
  createdAt: string
  actor?: User
}

export interface Dashboard {
  booksRead: number
  pagesRead: number
  readingMinutes: number
  currentStreak: number
  weeklyPages: Array<{ label: string; value: number }>
  currentlyReading: LibraryEntry[]
  recentSessions: ReadingSession[]
  activeChallenges: Challenge[]
}
