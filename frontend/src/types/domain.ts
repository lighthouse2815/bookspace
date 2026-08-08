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
  followsYou?: boolean
  mutualFollowCount?: number
  isMuted?: boolean
  privacy?: ProfilePrivacy
  joinedAt?: string
}

export interface ProfilePrivacy {
  isReadingShelfPublic: boolean
  isReadingActivityPublic: boolean
}

export interface UserSafetyEntry {
  user: User
  isBlocked: boolean
  isMuted: boolean
  blockedAt?: string | null
  mutedAt?: string | null
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
  description?: string
  slug?: string
  bookCount?: number
}

export interface CatalogFollowing {
  authors: Author[]
  categories: Category[]
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

export type BookListVisibility = 'PUBLIC' | 'PRIVATE'

export interface BookListSummary {
  id: string
  name: string
  description?: string | null
  visibility: BookListVisibility
  owner: User
  bookCount: number
  previewBooks: Book[]
  isOwner: boolean
  containsBook?: boolean | null
  createdAt: string
  updatedAt?: string | null
}

export interface BookListItem {
  id: string
  book: Book
  position: number
  addedAt: string
}

export interface BookListDetail {
  id: string
  name: string
  description?: string | null
  visibility: BookListVisibility
  owner: User
  isOwner: boolean
  items: BookListItem[]
  createdAt: string
  updatedAt?: string | null
}

export interface ExternalBook {
  externalId: string
  title: string
  authors: string[]
  coverImageUrl?: string | null
  isbn?: string | null
  description?: string | null
  pageCount?: number | null
  publishedYear?: number | null
  language?: string | null
  categories: string[]
  price?: number | null
  purchaseUrl?: string | null
}

export interface ExternalBookSearchResult {
  available: boolean
  provider: string
  message: string
  items: ExternalBook[]
}

export type ExternalBookImportStatus =
  | 'IMPORTED'
  | 'LINKED_EXISTING'
  | 'ALREADY_IMPORTED'

export interface ExternalBookImportResult {
  status: ExternalBookImportStatus
  provider: string
  externalId: string
  book: Book
}

export type BookRecommendationReason =
  | 'FOLLOWED_READER_LIKED'
  | 'MATCHED_AUTHOR'
  | 'MATCHED_CATEGORY'
  | 'POPULAR_FALLBACK'

export interface BookRecommendation {
  book: Book
  reasonCode: BookRecommendationReason
  reasonText: string
}

export type OnboardingStatus = 'PENDING' | 'COMPLETED' | 'SKIPPED'

export interface OnboardingState {
  status: OnboardingStatus
  finishedAt?: string | null
  preferredCategoryIds: string[]
  referenceBookIds: string[]
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

export interface PublicLibraryEntry {
  bookId: string
  book: Book
  shelf: Shelf
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

export type ActiveReadingSessionStatus = 'RUNNING' | 'PAUSED'

export interface ActiveReadingSession {
  id: string
  bookId: string
  book: Book | null
  status: ActiveReadingSessionStatus
  startPage: number
  startedAt: string
  elapsedSeconds: number
  updatedAt: string
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

export type FeedFilter = 'REVIEW' | 'READING' | 'CLUB' | 'CHALLENGE'

export interface FeedItem {
  id: string
  type: 'REVIEW' | 'READING_PROGRESS' | 'BOOK_FINISHED' | 'CHALLENGE' | 'CLUB_POST'
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

export interface ClubChatMessage {
  id: string
  clubId: string
  sender: User
  content: string
  createdAt: string
}

export interface ClubChatMessagePage {
  items: ClubChatMessage[]
  nextCursor: string | null
  hasMore: boolean
}

export interface ClubChatReadState {
  clubId: string
  count: number
  lastReadMessageId: string | null
  lastReadAt: string | null
}

export interface DirectMessage {
  id: string
  conversationId: string
  sender: User
  content: string
  createdAt: string
}

export interface DirectMessagePage {
  items: DirectMessage[]
  nextCursor: string | null
  hasMore: boolean
}

export interface Conversation {
  id: string
  otherParticipant: User
  lastMessage: DirectMessage | null
  unreadCount: number
  canSend: boolean
  lastActivityAt: string
  createdAt: string
}

export interface ConversationPage {
  items: Conversation[]
  nextCursor: string | null
  hasMore: boolean
}

export interface DirectMessageReadState {
  conversationId: string
  count: number
  lastReadMessageId: string | null
  lastReadAt: string | null
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

export interface ChallengeLeaderboardItem {
  rank: number
  user: User
  currentBooks: number
  targetBooks: number
  progressPercent: number
  completedAt: string | null
  isCurrentUser: boolean
}

export interface Notification {
  id: string
  type: 'FOLLOW' | 'CATALOG' | 'REVIEW_LIKE' | 'COMMENT' | 'CLUB' | 'CHALLENGE' | 'DIRECT_MESSAGE' | 'SYSTEM'
  title: string
  message: string
  link?: string
  isRead: boolean
  createdAt: string
  actor?: User
}

export type NotificationCategory =
  | 'FOLLOW'
  | 'CATALOG'
  | 'REVIEW'
  | 'CLUB'
  | 'CHALLENGE'
  | 'DIRECT_MESSAGE'
  | 'SYSTEM'

export interface NotificationPreferences {
  isFollowNotificationEnabled: boolean
  isCatalogNotificationEnabled: boolean
  isReviewNotificationEnabled: boolean
  isClubNotificationEnabled: boolean
  isChallengeNotificationEnabled: boolean
  isDirectMessageNotificationEnabled: boolean
}

export type ContentReportTargetType =
  | 'USER'
  | 'REVIEW'
  | 'REVIEW_COMMENT'
  | 'CLUB_POST'
  | 'CLUB_POST_COMMENT'
  | 'CLUB_CHAT_MESSAGE'
  | 'DIRECT_MESSAGE'

export type ContentReportReason =
  | 'SPAM'
  | 'HARASSMENT'
  | 'HATEFUL_CONTENT'
  | 'INAPPROPRIATE_CONTENT'
  | 'MISINFORMATION'
  | 'OTHER'

export type ContentReportStatus = 'PENDING' | 'RESOLVED' | 'DISMISSED'
export type ModerationAction = 'NONE' | 'CONTENT_REMOVED' | 'USER_LOCKED'

export interface ContentReport {
  id: string
  reporter: User
  targetType: ContentReportTargetType
  targetId: string
  targetOwner: User
  reason: ContentReportReason
  details: string | null
  targetPreview: string
  targetLink: string
  status: ContentReportStatus
  action: ModerationAction
  moderator: User | null
  resolutionNote: string | null
  resolvedAt: string | null
  createdAt: string
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
