# BookSpace — Hợp đồng REST API

> Base URL local: `http://localhost:5080/api`<br>
> Content type: `application/json; charset=utf-8`<br>
> Authentication: `Authorization: Bearer <accessToken>`

## 1. Quy ước bắt buộc

### 1.1 `ApiResponse<T>`

Mọi endpoint trả đúng một envelope:

```json
{
  "success": true,
  "message": "Thành công",
  "data": {},
  "code": null,
  "timestamp": "2026-07-29T10:00:00Z"
}
```

Lỗi:

```json
{
  "success": false,
  "message": "Email đã được sử dụng",
  "data": null,
  "code": "EMAIL_ALREADY_EXISTS",
  "timestamp": "2026-07-29T10:00:00Z"
}
```

Validation nhiều field:

```json
{
  "success": false,
  "message": "Dữ liệu không hợp lệ",
  "data": {
    "errors": {
      "email": [
        "Email không hợp lệ"
      ],
      "password": [
        "Mật khẩu phải có ít nhất 8 ký tự"
      ]
    }
  },
  "code": "VALIDATION_ERROR",
  "timestamp": "2026-07-29T10:00:00Z"
}
```

Frontend không hỗ trợ response trực tiếp `T` hoặc envelope rút gọn.

### 1.2 `PageResult<T>`

Query mặc định: `page=1`, `pageSize=20`. Server normalize `page < 1` thành 1 và clamp `pageSize` vào khoảng 1..100.

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

Danh sách rỗng trả `items: []`. Metadata nằm trong body, không nằm trong header.

### 1.3 Serialization

- ID là UUID string.
- Datetime là ISO 8601 UTC.
- Enum là chuỗi in hoa.
- Collection không bao giờ là `null`.
- Optional field không có giá trị trả `null`.
- JSON request/response dùng chính xác tên camelCase trong tài liệu này.
- Message hiển thị cho người dùng dùng tiếng Việt.

### 1.4 HTTP status

| Status | Dùng cho |
|---:|---|
| 200 | đọc, cập nhật, xóa thành công |
| 201 | tạo resource thành công |
| 400 | validation hoặc invariant sai |
| 401 | authentication sai/hết hạn |
| 403 | không đủ role/quyền sở hữu |
| 404 | resource không tồn tại hoặc không được phép nhìn thấy |
| 409 | uniqueness hoặc state conflict |
| 429 | rate limit |
| 502 | upstream provider lỗi |
| 503 | integration bị tắt/chưa sẵn sàng |
| 500 | lỗi nội bộ đã che chi tiết |

## 2. Response models

### 2.1 User và auth

`UserResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `email` | string; chỉ trả cho chính user/auth response |
| `displayName` | string |
| `bio` | string hoặc null |
| `avatarUrl` | string hoặc null |
| `role` | `USER` hoặc `ADMIN` |
| `followerCount` | integer |
| `followingCount` | integer |
| `booksReadCount` | integer |
| `isFollowing` | boolean |
| `joinedAt` | datetime |

Với `GET /users/{id}`, `email` phải là `null` hoặc bị loại khỏi DTO công khai; frontend không được hiển thị email người khác.

`UserDiscoveryItem` là DTO công khai riêng:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `displayName` | string |
| `bio` | string hoặc null |
| `avatarUrl` | string hoặc null |
| `followerCount` | integer |
| `booksReadCount` | integer |
| `isFollowing` | boolean; guest luôn `false` |
| `followsYou` | boolean; guest luôn `false` |
| `mutualFollowCount` | integer; guest luôn `0` |
| `reason` | stable string code |
| `reasonText` | chuỗi tiếng Việt ổn định |

DTO này không có email, password hash, token, role hoặc chi tiết library.

`AuthSessionResponse`:

| Field | Kiểu |
|---|---|
| `accessToken` | string |
| `refreshToken` | string |
| `expiresAt` | datetime |
| `user` | `UserResponse` |

`AuthTokensResponse`: `accessToken`, `refreshToken`, `expiresAt`.

### 2.2 Catalog

`AuthorResponse`: `id`, `name`, `biography`, `avatarUrl`, `bookCount`.

`CategoryResponse`: `id`, `name`, `description`, `bookCount`.

`BookResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `title` | string |
| `description` | string hoặc null |
| `isbn` | string hoặc null |
| `coverImageUrl` | string hoặc null |
| `pageCount` | integer |
| `publishedYear` | integer hoặc null |
| `language` | string |
| `averageRating` | number |
| `reviewCount` | integer |
| `author` | `AuthorResponse` hoặc null |
| `categories` | `CategoryResponse[]` |
| `shelf` | `WANT_TO_READ`, `READING`, `READ` hoặc null |

`shelf` chỉ khác `null` khi request có access token hợp lệ và sách nằm trong thư viện principal.

### 2.3 Reading

`LibraryEntryResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `userId` | UUID |
| `bookId` | UUID |
| `book` | `BookResponse` |
| `shelf` | `WANT_TO_READ`, `READING`, `READ` |
| `currentPage` | integer |
| `progressPercent` | number từ 0 đến 100 |
| `startedAt` | datetime hoặc null |
| `finishedAt` | datetime hoặc null |
| `updatedAt` | datetime |

`ReadingSessionResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `bookId` | UUID |
| `book` | `BookResponse` hoặc null |
| `startedAt` | datetime |
| `endedAt` | datetime hoặc null |
| `durationMinutes` | integer |
| `pagesRead` | integer |
| `note` | string hoặc null |
| `createdAt` | datetime |

`ReadingGoalResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `metric` | `BOOKS`, `PAGES`, `MINUTES` |
| `period` | `WEEK`, `MONTH`, `YEAR`, `CUSTOM` |
| `targetValue` | integer 1..1.000.000 |
| `currentValue` | integer, server tính từ dữ liệu đọc |
| `progressPercent` | integer 0..100, làm tròn gần nhất |
| `startDate`, `endDate` | datetime |
| `status` | `ACTIVE`, `COMPLETED`, `EXPIRED` |
| `completedAt` | datetime hoặc null |
| `createdAt`, `updatedAt` | datetime, datetime hoặc null |

`ReadingNoteResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `bookId` | UUID |
| `book` | `BookResponse` hoặc null |
| `pageNumber` | integer hoặc null |
| `quote` | string hoặc null |
| `content` | string hoặc null |
| `tags` | `string[]` |
| `createdAt`, `updatedAt` | datetime, datetime hoặc null |

`ReadingInsightsOverviewResponse`:

| Field | Kiểu |
|---|---|
| `utcOffsetMinutes` | integer -840..840 |
| `days` | `30`, `90` hoặc `365` |
| `fromDate`, `toDate` | local date `yyyy-MM-dd`, gồm cả hai đầu |
| `totalSessions`, `totalPages`, `totalMinutes` | integer |
| `booksFinished`, `activeDays` | integer |
| `averagePagesPerActiveDay`, `averageMinutesPerActiveDay`, `averageSessionsPerActiveDay` | number; bằng 0 khi không có ngày active |
| `currentStreak`, `longestStreak` | integer ngày |
| `goals` | `{ total, active, completed, expired }` |
| `comparison` | `ReadingInsightComparisonResponse` |
| `forecasts` | `ReadingFinishForecastResponse[]` |
| `goalForecasts` | `ReadingGoalForecastResponse[]` |

`ReadingInsightComparisonResponse` có `currentFromDate`, `currentToDate`, `previousFromDate`, `previousToDate` và năm metric `sessions`, `pages`, `minutes`, `activeDays`, `booksFinished`. Mỗi metric có:

```json
{
  "current": 120,
  "previous": 80,
  "changePercent": 50
}
```

`changePercent` là `null` khi kỳ trước bằng 0 nhưng kỳ hiện tại dương; bằng 0 khi cả hai kỳ bằng 0.

`ReadingFinishForecastResponse`:

| Field | Kiểu |
|---|---|
| `libraryItemId`, `bookId` | UUID |
| `title` | string |
| `coverImageUrl` | string hoặc null |
| `currentPage`, `pageCount`, `remainingPages` | integer |
| `averagePagesPerDay` | number |
| `estimatedDaysRemaining` | integer hoặc null |
| `estimatedFinishDate` | local date hoặc null |

`ReadingGoalForecastResponse`:

| Field | Kiểu |
|---|---|
| `goalId` | UUID |
| `metric` | `BOOKS`, `PAGES`, `MINUTES` |
| `targetValue`, `currentValue`, `remainingValue` | integer |
| `startDate`, `endDate` | datetime |
| `averagePerDay` | number |
| `estimatedFinishDate` | local date hoặc null |
| `isOnTrack` | boolean hoặc null |

`ReadingInsightsCalendarResponse` gồm `utcOffsetMinutes`, `year` hoặc null, `days`, `fromDate`, `toDate`, `activeDays`, `totalSessions`, `totalPages`, `totalMinutes` và `daysData`. Mỗi item của `daysData` có `date`, `sessionCount`, `pagesRead`, `minutesRead`, `isActive`; API luôn trả cả ngày không hoạt động.

`ReadingInsightsWeeklyResponse` gồm `utcOffsetMinutes`, `weeks`, `fromDate`, `toDate`, `items`. Mỗi item có `weekStart`, `weekEnd`, `sessions`, `pages`, `minutes`, `activeDays`, `booksFinished`, `averagePagesPerActiveDay`, `averageMinutesPerActiveDay`.

`ReadingInsightsMonthlyResponse` có cấu trúc tương tự weekly với `months` và mỗi item dùng `monthStart`, `monthEnd`.

### 2.4 Community

`ReviewResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `bookId` | UUID |
| `book` | `BookResponse` hoặc null |
| `user` | `UserResponse` |
| `rating` | integer 1..5 |
| `content` | string |
| `containsSpoilers` | boolean |
| `likeCount` | integer |
| `commentCount` | integer |
| `likedByCurrentUser` | boolean |
| `comments` | `ReviewCommentResponse[]` hoặc mảng rỗng |
| `createdAt` | datetime |
| `updatedAt` | datetime hoặc null |

`ReviewCommentResponse`: `id`, `reviewId`, `user`, `content`, `createdAt`.

`FeedItemResponse`:

| Field | Kiểu |
|---|---|
| `id` | string ổn định |
| `type` | `REVIEW`, `READING_PROGRESS`, `CHALLENGE`, `CLUB_POST` |
| `actor` | `UserResponse` |
| `review` | `ReviewResponse` hoặc null |
| `book` | `BookResponse` hoặc null |
| `club` | `ClubResponse` hoặc null |
| `challenge` | `ChallengeResponse` hoặc null |
| `content` | string hoặc null |
| `progressPercent` | number hoặc null |
| `createdAt` | datetime |

### 2.5 Clubs

`ClubResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `name` | string |
| `description` | string |
| `coverImageUrl` | string hoặc null |
| `memberCount` | integer |
| `isPrivate` | boolean |
| `isJoined` | boolean |
| `currentBook` | `BookResponse` hoặc null |
| `owner` | `UserResponse` |
| `posts` | `ClubPostResponse[]` hoặc null |
| `createdAt` | datetime |
| `viewerRole` | `OWNER`, `MODERATOR`, `MEMBER` hoặc null |
| `permissions` | `ClubPermissionsResponse` |

`ClubPermissionsResponse`: `canEdit`, `canInvite`, `canManageMembers`,
`canManageCurrentBook`, `canLeave`.

`ClubMemberResponse`: `id`, `user`, `role`, `joinedAt`.

`ClubInvitationResponse`: `id`, `club`, `inviter`, `invitedUser`, `status`,
`expiresAt`, `respondedAt`, `createdAt`. `status` là `PENDING`, `ACCEPTED`,
`DECLINED`, `REVOKED` hoặc `EXPIRED`.

`ClubPostResponse`: `id`, `clubId`, `author`, `content`, `commentCount`, `createdAt`.

`ClubPostCommentResponse`: `id`, `postId`, `author`, `content`, `createdAt`.

`ReadingSprintSummaryResponse`:

| Field | Kiểu |
|---|---|
| `id`, `clubId` | UUID |
| `title` | string |
| `description` | string hoặc null |
| `book` | `BookResponse` |
| `startsAt`, `endsAt` | datetime UTC |
| `targetUnit` | `PAGES` hoặc `CHAPTERS` |
| `targetValue` | integer |
| `status` | `PLANNED`, `ACTIVE`, `ENDED`, `COMPLETED` hoặc `CANCELLED` |
| `participantCount`, `completedCount` | integer |
| `averageProgressPercent` | integer trong 0..100 |
| `viewerParticipation` | `ReadingSprintParticipantResponse` hoặc null |
| `permissions` | `ReadingSprintPermissionsResponse` |
| `createdBy` | `UserResponse` |
| `completedAt`, `cancelledAt`, `lastReminderAt` | datetime UTC hoặc null |
| `createdAt` | datetime UTC |

`ReadingSprintDetailResponse` có toàn bộ field của summary và thêm
`milestones: ReadingSprintMilestone[]`.

`ReadingSprintPermissionsResponse`: `canManage`, `canJoin`, `canLeave`,
`canCheckIn`, `canDiscuss`, `canSendReminder`. `canJoin`/`canLeave` chỉ bật ở
`PLANNED`/`ACTIVE`; `canCheckIn` và `canDiscuss` yêu cầu active participant và
sprint `ACTIVE`; `canSendReminder` yêu cầu manager, sprint `ACTIVE` và chưa gửi
trong ngày UTC hiện tại.

`ReadingSprintParticipantResponse`: `id`, `user`, `progressValue`,
`progressPercent`, `rank`, `joinedAt`, `leftAt`, `completedAt`,
`lastCheckInAt`, `isActive`.

`ReadingSprintCheckInResponse`: `id`, `user`, `progressValue`,
`progressPercent`, `note`, `createdAt`.

`ReadingSprintMilestone`: `id`, `title`, `description`, `targetValue`,
`reachedByViewer`, `responseCount`, `createdAt`.

`ReadingSprintMilestoneResponse`: `id`, `milestoneId`, `author`,
`content`, `canDelete`, `createdAt`. `canDelete=true` chỉ với author hoặc club
manager hiện tại.

### 2.6 Challenges

`ChallengeResponse`:

| Field | Kiểu |
|---|---|
| `id` | UUID |
| `title` | string |
| `description` | string |
| `startDate` | datetime |
| `endDate` | datetime |
| `goalBooks` | integer |
| `currentBooks` | integer |
| `participantCount` | integer |
| `isJoined` | boolean |
| `isPublished` | boolean |
| `coverImageUrl` | string hoặc null |
| `completedAt` | datetime hoặc null |

`currentBooks` và `completedAt` chỉ do server suy ra. Nguồn đếm là `LibraryItem`
shelf `READ` có `FinishedAt` trong khoảng UTC đóng `[startDate, endDate]`; không
giới hạn bởi thời điểm join. Giá trị đã ghi nhận không giảm và bị chặn tại
`goalBooks`.

### 2.7 Notification và dashboard

`NotificationResponse`: `id`, `type`, `title`, `message`, `link`, `isRead`, `createdAt`.

Notification type: `FOLLOW`, `REVIEW_LIKE`, `COMMENT`, `CLUB`, `CHALLENGE`, `SYSTEM`.

`DashboardResponse`:

| Field | Kiểu |
|---|---|
| `booksRead` | integer |
| `pagesRead` | integer |
| `readingMinutes` | integer |
| `currentStreak` | integer ngày |
| `weeklyPages` | `{ label: string, value: integer }[]` gồm 7 ngày |
| `currentlyReading` | `LibraryEntryResponse[]` |
| `recentSessions` | `ReadingSessionResponse[]`, tối đa 5 |
| `activeChallenges` | `ChallengeResponse[]` |

## 3. Auth API

### `POST /api/auth/register` — Public

Request:

```json
{
  "displayName": "Nguyễn An",
  "email": "reader@example.com",
  "password": "Reader123!"
}
```

Validation: display name 2–100 ký tự; email hợp lệ; password 8–100 ký tự.

Response `201`: `ApiResponse<AuthSessionResponse>`.

Errors: `VALIDATION_ERROR` 400, `EMAIL_ALREADY_EXISTS` 409.

### `POST /api/auth/login` — Public

Request:

```json
{
  "email": "reader@example.com",
  "password": "Reader123!"
}
```

Response `200`: `ApiResponse<AuthSessionResponse>`.

Sai email hoặc mật khẩu đều trả `INVALID_CREDENTIALS` 401.

### `POST /api/auth/refresh` — Public với refresh token

Request:

```json
{
  "refreshToken": "opaque-refresh-token"
}
```

Response `200`: `ApiResponse<AuthTokensResponse>`. Token cũ bị thu hồi và token mới được tạo trong cùng transaction.

### `POST /api/auth/logout` — Authenticated

Request:

```json
{
  "refreshToken": "opaque-refresh-token"
}
```

Response `200`: `ApiResponse<null>`. Logout lặp lại là idempotent.

### `GET /api/auth/me` — Authenticated

Response `200`: `ApiResponse<UserResponse>`.

## 4. User API

### `GET /api/users?search=&page=1&pageSize=20` — Public

Search rỗng trả danh bạ mặc định. Search khác rỗng được trim, dài 2-100 ký tự và
chỉ so khớp `DisplayName`; không tìm email. SQLite `NOCASE` hỗ trợ
case-insensitive cho ASCII và vẫn accent-sensitive. Kết quả loại user locked,
soft-deleted và loại principal nếu request có JWT hợp lệ. Response:
`ApiResponse<PageResult<UserDiscoveryItem>>`, xếp `displayName`, rồi `id`.

Search ngoài giới hạn trả 400:

```json
{
  "success": false,
  "message": "Từ khóa tìm kiếm độc giả phải có từ 2 đến 100 ký tự.",
  "data": null,
  "code": "INVALID_USER_SEARCH",
  "timestamp": "2026-07-31T00:00:00Z"
}
```

### `GET /api/users/suggestions?page=1&pageSize=20` — USER, ADMIN

Loại principal, user đã follow, locked và soft-deleted. Xếp
`mutualFollowCount DESC`, `followerCount DESC`, `booksReadCount DESC`,
`displayName ASC`, `id ASC`; vẫn trả fallback có mutual bằng 0. `reason` là một
trong `MUTUAL_FOLLOWS`, `FOLLOWS_YOU`, `POPULAR_READER`, `ACTIVE_READER`,
`NEW_READER`. Response:
`ApiResponse<PageResult<UserDiscoveryItem>>`.

### `PATCH /api/users/me` — Authenticated

Request:

```json
{
  "displayName": "Nguyễn An",
  "bio": "Đọc văn học và khoa học.",
  "avatarUrl": "https://images.example.com/users/nguyen-an.jpg"
}
```

Response `200`: `ApiResponse<UserResponse>`.

### `GET /api/users/{userId}` — Public

Response `200`: `ApiResponse<UserResponse>` với email không công khai.

### `POST /api/users/{userId}/follow` — USER, ADMIN

Body rỗng. Response `200`: `ApiResponse<UserResponse>`.

Errors: `CANNOT_FOLLOW_SELF` 400, `ALREADY_FOLLOWING` 409, `USER_NOT_FOUND` 404.

### `DELETE /api/users/{userId}/follow` — USER, ADMIN

Response `200`: `ApiResponse<UserResponse>`. Unfollow lặp lại là idempotent.

### `GET /api/users/{userId}/followers?page=1&pageSize=20` — Public

Response `200`: `ApiResponse<PageResult<UserResponse>>`.

### `GET /api/users/{userId}/following?page=1&pageSize=20` — Public

Response `200`: `ApiResponse<PageResult<UserResponse>>`.

## 5. Catalog API

### `GET /api/books` — Public

Query:

| Field | Kiểu | Giá trị |
|---|---|---|
| `search` | string optional | title, ISBN, author |
| `categoryId` | UUID optional | lọc category |
| `authorId` | UUID optional | lọc author |
| `sort` | string optional | `title`, `popular`, `rating`, `newest` |
| `page` | integer | pagination |
| `pageSize` | integer | pagination |

Response `200`: `ApiResponse<PageResult<BookResponse>>`.

### `GET /api/books/{bookId}` — Public

Response `200`: `ApiResponse<BookResponse>`.

### `GET /api/authors?page=1&pageSize=100` — Public

Response `200`: `ApiResponse<PageResult<AuthorResponse>>`.

### `GET /api/categories?page=1&pageSize=100` — Public

Response `200`: `ApiResponse<PageResult<CategoryResponse>>`.

## 6. Admin catalog API

Tất cả endpoint yêu cầu role `ADMIN`.

### `POST /api/admin/books`

Request:

```json
{
  "title": "Dế Mèn Phiêu Lưu Ký",
  "authorId": "11111111-1111-1111-1111-111111111111",
  "categoryIds": [
    "22222222-2222-2222-2222-222222222222"
  ],
  "description": "Tác phẩm văn học thiếu nhi.",
  "isbn": "9786040000001",
  "coverImageUrl": "https://images.example.com/books/de-men.jpg",
  "pageCount": 180,
  "publishedYear": 2025
}
```

`language` mặc định là `vi` trong Goal 1.

Response `201`: `ApiResponse<BookResponse>`.

### `PATCH /api/admin/books/{bookId}`

Request dùng đủ payload như create; cập nhật thay thế các quan hệ author/category hiện có.

Response `200`: `ApiResponse<BookResponse>`.

### `DELETE /api/admin/books/{bookId}`

Soft delete. Response `200`: `ApiResponse<null>`.

### `POST /api/admin/authors`

Request: `name`, `biography?`, `avatarUrl?`.

Response `201`: `ApiResponse<AuthorResponse>`.

### `PATCH /api/admin/authors/{authorId}`

Request dùng đủ payload `name`, `biography?`, `avatarUrl?`.

Response `200`: `ApiResponse<AuthorResponse>`.

### `DELETE /api/admin/authors/{authorId}`

Chỉ soft-delete khi author chưa được gắn với book; nếu đang được dùng trả `409 AUTHOR_IN_USE`. Response `200`: `ApiResponse<null>`.

### `POST /api/admin/categories`

Request: `name`, `description?`.

Response `201`: `ApiResponse<CategoryResponse>`.

### `PATCH /api/admin/categories/{categoryId}`

Request dùng đủ payload `name`, `description?`.

Response `200`: `ApiResponse<CategoryResponse>`.

### `DELETE /api/admin/categories/{categoryId}`

Chỉ soft-delete khi category chưa được gắn với book; nếu đang được dùng trả `409 CATEGORY_IN_USE`. Response `200`: `ApiResponse<null>`.

Catalog errors: `BOOK_NOT_FOUND`, `ISBN_ALREADY_EXISTS`, `AUTHOR_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `AUTHOR_ALREADY_EXISTS`, `CATEGORY_ALREADY_EXISTS`, `AUTHOR_IN_USE`, `CATEGORY_IN_USE`.

## 7. Library API

Tất cả endpoint yêu cầu authentication và chỉ thao tác library của principal.

### `GET /api/library?shelf=&page=1&pageSize=20`

`shelf`: `WANT_TO_READ`, `READING`, `READ` hoặc bỏ trống.

Response `200`: `ApiResponse<PageResult<LibraryEntryResponse>>`, sắp xếp `updatedAt desc`.

### `POST /api/library`

Request:

```json
{
  "bookId": "33333333-3333-3333-3333-333333333333",
  "shelf": "WANT_TO_READ"
}
```

Response `201`: `ApiResponse<LibraryEntryResponse>`.

### `PATCH /api/library/{libraryItemId}`

Request cập nhật shelf:

```json
{
  "shelf": "READING"
}
```

Request cập nhật tiến độ:

```json
{
  "currentPage": 75
}
```

Hai field có thể gửi cùng request. `progressPercent` là response projection và không nhận từ client.

Response `200`: `ApiResponse<LibraryEntryResponse>`.

### `DELETE /api/library/{libraryItemId}`

Soft delete. Response `200`: `ApiResponse<null>`.

Errors: `LIBRARY_ITEM_NOT_FOUND`, `BOOK_ALREADY_IN_LIBRARY`, `INVALID_READING_PROGRESS`, `READING_PROGRESS_CANNOT_DECREASE`.

## 8. Reading session API

Reading session là nhật ký append-only trong Goal 1.

### `GET /api/reading-sessions?page=1&pageSize=20` — Authenticated

Response `200`: `ApiResponse<PageResult<ReadingSessionResponse>>`, chỉ trả session của principal, sắp xếp `startedAt desc`.

### `POST /api/reading-sessions` — Authenticated

Request:

```json
{
  "bookId": "33333333-3333-3333-3333-333333333333",
  "startedAt": "2026-07-29T09:00:00Z",
  "endedAt": "2026-07-29T09:40:00Z",
  "durationMinutes": 40,
  "pagesRead": 25,
  "note": "Đọc chương 3 và 4."
}
```

Invariant:

- `startedAt` không ở tương lai quá 5 phút.
- `endedAt >= startedAt` khi có.
- `durationMinutes` 1..1.440.
- Nếu có `endedAt`, chênh lệch cho phép lệch tối đa một phút so với `durationMinutes`.
- `pagesRead` dương và không vượt số trang sách.

Response `201`: `ApiResponse<ReadingSessionResponse>`.

Side effect: tạo library entry `READING` nếu chưa có và tăng tiến độ, tối đa `pageCount`.

## 9. Reading goal API

Mọi endpoint dưới đây yêu cầu authentication và chỉ thao tác mục tiêu của principal. Mục tiêu là dữ liệu cá nhân; không có endpoint admin hoặc public cho feature này.

### `GET /api/reading-goals?status=&page=1&pageSize=20`

`status` tùy chọn: `ACTIVE`, `COMPLETED`, `EXPIRED`. Response `200`: `ApiResponse<PageResult<ReadingGoalResponse>>`.

`currentValue`, `progressPercent`, `status` và `completedAt` luôn do server suy ra. Trước khi áp filter `status` và phân trang, server đồng bộ completion của mọi goal pending thuộc principal. Khi một goal được đánh giá lần đầu đạt target, response có trạng thái `COMPLETED` và hệ thống ghi đúng một notification `SYSTEM` với link `/goals`.

### `GET /api/reading-goals/{id}`

Response `200`: `ApiResponse<ReadingGoalResponse>`. ID không thuộc principal hoặc đã soft-delete trả `404 READING_GOAL_NOT_FOUND`.

### `POST /api/reading-goals`

Request:

```json
{
  "metric": "PAGES",
  "period": "CUSTOM",
  "targetValue": 240,
  "startDate": "2026-07-01T00:00:00Z",
  "endDate": "2026-07-31T23:59:59Z"
}
```

Response `201`: `ApiResponse<ReadingGoalResponse>`.

Rules:

- `metric`: `BOOKS`, `PAGES`, `MINUTES`; `period`: `WEEK`, `MONTH`, `YEAR`, `CUSTOM`.
- Giá trị metric/period ngoài enum bị từ chối với `400 INVALID_READING_GOAL_METRIC` hoặc `400 INVALID_READING_GOAL_PERIOD`.
- `period` chỉ là classification; client luôn phải gửi cả `startDate` và `endDate`.
- `targetValue` là 1..1.000.000; `endDate` phải ở tương lai, sau `startDate`; khoảng mục tiêu tối đa 366 ngày.
- Cùng principal không được có mục tiêu chưa completed/chưa expired cùng metric bị chồng khoảng thời gian.
- `BOOKS` đếm library item `READ` có `finishedAt` trong kỳ; `PAGES` và `MINUTES` cộng reading session có `startedAt` trong kỳ.

### `PATCH /api/reading-goals/{id}`

Gửi đầy đủ payload writable giống create. Response `200`: `ApiResponse<ReadingGoalResponse>`. Mục tiêu đã `COMPLETED` hoặc `EXPIRED` không thể update. Client không gửi `currentValue`, `progressPercent`, `status` hoặc `completedAt`.

### `DELETE /api/reading-goals/{id}`

Soft delete. Response `200`: `ApiResponse<null>`.

Errors: `READING_GOAL_NOT_FOUND`, `INVALID_READING_GOAL_METRIC`, `INVALID_READING_GOAL_PERIOD`, `INVALID_READING_GOAL_TARGET`, `INVALID_READING_GOAL_DATE`, `READING_GOAL_OVERLAPS`, `READING_GOAL_ALREADY_COMPLETED`, `READING_GOAL_ALREADY_EXPIRED`.

## 10. Reading note API

Mọi endpoint dưới đây yêu cầu authentication và chỉ thao tác ghi chú của principal. Ghi chú không phải review và không xuất hiện ở feed, club hoặc notification.

### `GET /api/reading-notes?bookId=&tag=&search=&page=1&pageSize=20`

Response `200`: `ApiResponse<PageResult<ReadingNoteResponse>>`.

- `bookId`: UUID tùy chọn; book không tồn tại trả `BOOK_NOT_FOUND`.
- `tag`: match một tag đầy đủ, không phân biệt hoa/thường; input được trim và áp dụng rule tag.
- `search`: tìm không phân biệt hoa/thường trong quote, content hoặc tags; tối đa 200 ký tự.
- Kết quả sắp xếp `updatedAt ?? createdAt` giảm dần.

### `GET /api/reading-notes/{id}`

Response `200`: `ApiResponse<ReadingNoteResponse>`. ID không thuộc principal hoặc đã soft-delete trả `404 READING_NOTE_NOT_FOUND`.

### `POST /api/reading-notes`

Request:

```json
{
  "bookId": "33333333-3333-3333-3333-333333333333",
  "pageNumber": 42,
  "quote": "Một câu cần lưu lại.",
  "content": "Suy nghĩ của tôi sau chương này.",
  "tags": ["Kinh điển", "Đọc lại"]
}
```

Response `201`: `ApiResponse<ReadingNoteResponse>`.

Rules:

- Book phải tồn tại, nhưng không bắt buộc ở library của principal.
- Có ít nhất một trong `quote` hoặc `content` sau khi trim; quote tối đa 500 ký tự, content tối đa 5.000 ký tự.
- `pageNumber` tùy chọn; nếu có phải là 1..`pageCount` của book.
- Tag rỗng bị bỏ; tag còn lại được trim, deduplicate không phân biệt hoa/thường; tối đa 10 tag, mỗi tag tối đa 30 ký tự, tổng dài tối đa 500 ký tự và không chứa `|`.

### `PATCH /api/reading-notes/{id}`

Request chỉ gồm `pageNumber`, `quote`, `content`, `tags` và áp dụng cùng rule create. **Không gửi `bookId`:** book của ghi chú là bất biến trong update contract. Response `200`: `ApiResponse<ReadingNoteResponse>`.

### `DELETE /api/reading-notes/{id}`

Soft delete. Response `200`: `ApiResponse<null>`.

Errors: `READING_NOTE_NOT_FOUND`, `READING_NOTE_CONTENT_REQUIRED`, `INVALID_NOTE_PAGE_NUMBER`, `INVALID_READING_NOTE_TAG`, `INVALID_READING_NOTE_SEARCH`, `BOOK_NOT_FOUND`.

## 11. Reading insights API

Mọi endpoint Insights yêu cầu authentication, chỉ đọc dữ liệu của principal và không nhận `userId`. `utcOffsetMinutes` mặc định `0`, hợp lệ từ -840 đến 840; `420` nghĩa là UTC+7. Ngày local được đổi thành khoảng UTC nửa mở `[startUtc, endUtc)`. Toàn bộ session xuyên nửa đêm thuộc ngày chứa `startedAt`.

### `GET /api/insights/overview?days=30&utcOffsetMinutes=0`

Response `200`: `ApiResponse<ReadingInsightsOverviewResponse>`.

- `days` chỉ nhận `30`, `90`, `365`.
- Khoảng rolling gồm ngày local hiện tại và `days - 1` ngày trước đó.
- `currentStreak` được phép kết thúc hôm qua nếu hôm nay chưa đọc; `longestStreak` xét toàn lịch sử.
- Comparison dùng hai giai đoạn liền kề cùng độ dài.
- Forecast sách chỉ chứa library item `READING`; `averagePagesPerDay` dùng tối đa 30 ngày phiên của đúng book và chia cho số ngày lịch từ hoạt động đầu tiên trong cửa sổ đến hôm nay.
- Goal summary và forecast phải dùng tiến độ tính từ dữ liệu đọc thật. Việc gọi endpoint đồng bộ goal vừa đạt target sang `COMPLETED` và notification completion vẫn idempotent.

### `GET /api/insights/calendar?days=365&utcOffsetMinutes=0`

Response `200`: `ApiResponse<ReadingInsightsCalendarResponse>`.

- Rolling `days` chỉ nhận `30`, `90`, `365`; mặc định `365`.
- Có thể gửi `year=YYYY` thay cho rolling range. `year` nhận 1900 đến năm local hiện tại và override `days`.
- `daysData` có đúng một item cho mỗi ngày của khoảng, sắp tăng dần và điền item 0 cho ngày không đọc.

### `GET /api/insights/weekly?weeks=12&utcOffsetMinutes=0`

Response `200`: `ApiResponse<ReadingInsightsWeeklyResponse>`. `weeks` nhận 4..52; tuần bắt đầu thứ Hai theo ngày local. Response trả đủ item kể cả tuần rỗng.

### `GET /api/insights/monthly?months=12&utcOffsetMinutes=0`

Response `200`: `ApiResponse<ReadingInsightsMonthlyResponse>`. `months` chỉ nhận `6`, `12`, `24`; gồm tháng local hiện tại và trả đủ tháng rỗng.

Errors: `INVALID_INSIGHTS_RANGE`, `INVALID_INSIGHTS_YEAR`, `INVALID_INSIGHTS_WEEKS`, `INVALID_INSIGHTS_MONTHS`, `INVALID_UTC_OFFSET`.

## 12. Review API

### `GET /api/reviews?bookId={bookId}&page=1&pageSize=20` — Public

`bookId` bắt buộc. Response `200`: `ApiResponse<PageResult<ReviewResponse>>`.

### `POST /api/reviews` — Authenticated

Request:

```json
{
  "bookId": "33333333-3333-3333-3333-333333333333",
  "rating": 5,
  "content": "Một câu chuyện giàu trí tưởng tượng.",
  "containsSpoilers": false
}
```

Response `201`: `ApiResponse<ReviewResponse>`.

### `PUT /api/reviews/{reviewId}` — Owner

Request dùng đủ payload `rating`, `content`, `containsSpoilers`.

Response `200`: `ApiResponse<ReviewResponse>`.

### `DELETE /api/reviews/{reviewId}` — Owner hoặc ADMIN

Soft delete. Response `200`: `ApiResponse<null>`.

### `POST /api/reviews/{reviewId}/like` — Authenticated

Idempotent. Response `200`: `ApiResponse<ReviewResponse>`.

### `DELETE /api/reviews/{reviewId}/like` — Authenticated

Idempotent. Response `200`: `ApiResponse<ReviewResponse>`.

### `GET /api/reviews/{reviewId}/comments?page=1&pageSize=20` — Public

Response `200`: `ApiResponse<PageResult<ReviewCommentResponse>>`.

### `POST /api/reviews/{reviewId}/comments` — Authenticated

Request:

```json
{
  "content": "Mình cũng rất thích phần kết."
}
```

Response `201`: `ApiResponse<ReviewCommentResponse>`.

### `DELETE /api/review-comments/{commentId}` — Owner hoặc ADMIN

Soft delete. Response `200`: `ApiResponse<null>`.

## 13. Feed API

### `GET /api/feed?page=1&pageSize=20` — Authenticated

Trả hoạt động của principal và các user đang follow.

Response `200`: `ApiResponse<PageResult<FeedItemResponse>>`, sắp xếp `createdAt desc, id desc`.

## 14. Club API

### `GET /api/clubs?search=&page=1&pageSize=20` — Public

Chỉ liệt kê club public. Response `200`: `ApiResponse<PageResult<ClubResponse>>`.

### `POST /api/clubs` — Authenticated

Request:

```json
{
  "name": "Đọc sách mỗi tuần",
  "description": "Cùng nhau đọc và thảo luận.",
  "coverImageUrl": "https://images.example.com/clubs/weekly.jpg",
  "isPrivate": false
}
```

Response `201`: `ApiResponse<ClubResponse>`. Principal trở thành `OWNER`.

### `PATCH /api/clubs/{clubId}` — Club OWNER

Request có cùng bốn field như lúc tạo club. Chỉ `OWNER` được đổi tên, mô tả,
ảnh bìa và quyền riêng tư.

Response `200`: `ApiResponse<ClubResponse>`.

### `GET /api/clubs/{clubId}` — Public/member

Public club cho phép đọc công khai. Người ngoài private club nhận 404.

Response `200`: `ApiResponse<ClubResponse>`.

### `POST /api/clubs/{clubId}/join` — Authenticated

Chỉ club public. Response `200`: `ApiResponse<ClubResponse>`.

### `DELETE /api/clubs/{clubId}/join` — Member

Owner không thể leave. Trong cùng transaction, mọi reading-sprint participant
còn active của user trên sprint chưa explicit `COMPLETED`/`CANCELLED` được đặt
`leftAt`; check-in lịch sử được giữ. Response `200`: `ApiResponse<null>`.

### `GET /api/clubs/{clubId}/members?page=1&pageSize=20` — Public/member

Áp dụng cùng quy tắc đọc public/private như club detail.

Response `200`: `ApiResponse<PageResult<ClubMemberResponse>>`.

### `PATCH /api/clubs/{clubId}/members/{userId}/role` — Club OWNER

Request: `{ "role": "MODERATOR" }` hoặc `{ "role": "MEMBER" }`.
Không thể gán, hạ cấp hoặc chuyển vai trò `OWNER`.

Response `200`: `ApiResponse<ClubMemberResponse>`.

### `DELETE /api/clubs/{clubId}/members/{userId}` — Club manager

`OWNER` được loại member hoặc moderator. `MODERATOR` chỉ được loại `MEMBER`.
Không ai được loại `OWNER`. Việc loại member áp dụng cùng cleanup active
reading-sprint participant như club leave.

Response `200`: `ApiResponse<null>`.

### `POST /api/clubs/{clubId}/invitations` — Club OWNER/MODERATOR

Request: `{ "email": "reader@example.com" }`.

Hệ thống chỉ mời tài khoản BookSpace đã tồn tại, không cung cấp API tìm kiếm
email công khai. Gửi lại cùng lời mời đang `PENDING` là idempotent: trả đúng lời
mời hiện có và không tạo thêm notification.

Response `201`: `ApiResponse<ClubInvitationResponse>`.

### `GET /api/clubs/{clubId}/invitations?status=&page=1&pageSize=20` — Club OWNER/MODERATOR

Response `200`: `ApiResponse<PageResult<ClubInvitationResponse>>`.

### `DELETE /api/clubs/{clubId}/invitations/{invitationId}` — Club OWNER/MODERATOR

Thu hồi lời mời đang `PENDING`. Thu hồi lại lời mời đã `REVOKED` là idempotent.

Response `200`: `ApiResponse<ClubInvitationResponse>`.

### `GET /api/clubs/invitations?status=&page=1&pageSize=20` — Authenticated

Inbox chỉ chứa lời mời của principal. Khi đọc, lời mời quá hạn được chuyển sang
`EXPIRED`.

Response `200`: `ApiResponse<PageResult<ClubInvitationResponse>>`.

### `POST /api/clubs/invitations/{invitationId}/accept` — Invitation recipient

Chấp nhận tạo đúng một membership `MEMBER`; gọi lặp lại không tạo membership hay
notification trùng.

Response `200`: `ApiResponse<ClubMemberResponse>`.

### `POST /api/clubs/invitations/{invitationId}/decline` — Invitation recipient

Từ chối lại lời mời đã `DECLINED` là idempotent.

Response `200`: `ApiResponse<ClubInvitationResponse>`.

### `PUT /api/clubs/{clubId}/current-book` — Club OWNER/MODERATOR

Request: `{ "bookId": "UUID" }`. Sách phải tồn tại trong catalog nội bộ
BookSpace. Chọn lại cùng sách không tạo notification trùng.

Response `200`: `ApiResponse<ClubResponse>`.

### `DELETE /api/clubs/{clubId}/current-book` — Club OWNER/MODERATOR

Gỡ lại khi club chưa có sách đọc chung là idempotent.

Response `200`: `ApiResponse<ClubResponse>`.

### `GET /api/clubs/{clubId}/posts?page=1&pageSize=20` — Public/member

Response `200`: `ApiResponse<PageResult<ClubPostResponse>>`.

### `POST /api/clubs/{clubId}/posts` — Club member

Request:

```json
{
  "content": "Chi tiết nào trong chương đầu làm bạn ấn tượng nhất?"
}
```

Response `201`: `ApiResponse<ClubPostResponse>`.

### `DELETE /api/clubs/posts/{postId}` — Author, club OWNER/MODERATOR hoặc ADMIN

Soft delete. Response `200`: `ApiResponse<null>`.

### `GET /api/clubs/posts/{postId}/comments?page=1&pageSize=20` — Public/member

Response `200`: `ApiResponse<PageResult<ClubPostCommentResponse>>`.

### `POST /api/clubs/posts/{postId}/comments` — Club member

Request: `{ "content": "Mình đồng ý với nhận xét này." }`.

Response `201`: `ApiResponse<ClubPostCommentResponse>`.

### `DELETE /api/clubs/post-comments/{commentId}` — Author, club OWNER/MODERATOR hoặc ADMIN

Soft delete. Response `200`: `ApiResponse<null>`.

### Reading sprint

Các GET bên dưới dùng cùng visibility boundary với club: public club đọc được
công khai; private club chỉ trả dữ liệu cho active member và trả 404 cho người
ngoài dù biết UUID. Mọi mutation yêu cầu authentication.

#### `GET /api/clubs/{clubId}/reading-sprints?status=&page=1&pageSize=20`

`status` tùy chọn: `PLANNED`, `ACTIVE`, `ENDED`, `COMPLETED`, `CANCELLED`.
Response `200`: `ApiResponse<PageResult<ReadingSprintSummaryResponse>>`.

#### `GET /api/clubs/{clubId}/reading-sprints/{sprintId}`

Response `200`: `ApiResponse<ReadingSprintDetailResponse>`.

#### `POST /api/clubs/{clubId}/reading-sprints` — Club OWNER/MODERATOR

Request:

```json
{
  "bookId": "UUID",
  "title": "Đọc chung Clean Architecture",
  "description": "Cùng hoàn thành và thảo luận theo từng mốc.",
  "startsAt": "2026-08-01T00:00:00Z",
  "endsAt": "2026-08-21T23:59:59Z",
  "targetUnit": "PAGES",
  "targetValue": 432
}
```

Book phải tồn tại trong catalog nội bộ. `title` tối đa 200 ký tự,
`description` tối đa 2.000 ký tự, `endsAt > startsAt` và `endsAt` phải ở tương
lai. Target `PAGES` không vượt `book.pageCount`; target `CHAPTERS` trong
1..500. Response `201`: `ApiResponse<ReadingSprintDetailResponse>`.

#### `PATCH /api/clubs/{clubId}/reading-sprints/{sprintId}` — Club OWNER/MODERATOR

Gửi đầy đủ payload như create. Chỉ sprint có derived status `PLANNED`, trước
`startsAt`, được sửa; `ACTIVE`, `ENDED`, `COMPLETED`, `CANCELLED` bị khóa.
Không đổi `targetUnit` sau khi có participant/milestone và không hạ target thấp
hơn progress hoặc milestone lớn nhất.
Response `200`: `ApiResponse<ReadingSprintDetailResponse>`.

#### `POST /api/clubs/{clubId}/reading-sprints/{sprintId}/join` — Club member

Join và rejoin là idempotent. Rejoin tái kích hoạt participant cũ, giữ identity
và lịch sử; không tạo participant thứ hai.
Response `200`: `ApiResponse<ReadingSprintParticipantResponse>`.

#### `DELETE /api/clubs/{clubId}/reading-sprints/{sprintId}/join` — Active participant

Leave lặp lại là idempotent và giữ participant dưới dạng inactive để có thể
rejoin. Response `200`: `ApiResponse<ReadingSprintParticipantResponse>`.

#### `PUT /api/clubs/{clubId}/reading-sprints/{sprintId}/progress` — Active participant

Request:

```json
{
  "progressValue": 120,
  "note": "Đã hoàn tất phần kiến trúc ứng dụng."
}
```

Chỉ sprint `ACTIVE`. `progressValue` là giá trị tuyệt đối trong
`0..targetValue`, không được giảm. Gửi lại đúng giá trị hiện tại là idempotent:
không tạo check-in mới. `note` tùy chọn, tối đa 1.000 ký tự.
Response `200`: `ApiResponse<ReadingSprintParticipantResponse>`.

#### `GET /api/clubs/{clubId}/reading-sprints/{sprintId}/leaderboard?page=1&pageSize=20`

Chỉ gồm active participant, xếp progress giảm dần; khi bằng progress, ưu tiên
`completedAt`, rồi `lastCheckInAt`, `joinedAt`, cuối cùng `id`, đều tăng dần.
`progressPercent` bị chặn trong 0..100.
Response `200`: `ApiResponse<PageResult<ReadingSprintParticipantResponse>>`.

#### `GET /api/clubs/{clubId}/reading-sprints/{sprintId}/timeline?page=1&pageSize=20`

Chỉ trả check-in được tạo khi progress thực sự tăng, sắp mới nhất trước và áp
dụng visibility của club.
Response `200`: `ApiResponse<PageResult<ReadingSprintCheckInResponse>>`.

#### `POST /api/clubs/{clubId}/reading-sprints/{sprintId}/milestones` — Club OWNER/MODERATOR

Request: `{ "title": "Mốc 100 trang", "description": "Thảo luận phần đầu", "targetValue": 100 }`.
Milestone chỉ được mutation khi sprint `PLANNED` hoặc `ACTIVE`; `targetValue`
trong 1..target sprint.
Response `201`: `ApiResponse<ReadingSprintMilestone>`.

#### `PATCH /api/clubs/{clubId}/reading-sprints/{sprintId}/milestones/{milestoneId}` — Club OWNER/MODERATOR

Gửi đầy đủ payload milestone như create và áp dụng cùng invariant.
Response `200`: `ApiResponse<ReadingSprintMilestone>`.

#### `DELETE /api/clubs/{clubId}/reading-sprints/{sprintId}/milestones/{milestoneId}` — Club OWNER/MODERATOR

Soft delete milestone và ẩn khỏi detail active. Response `200`:
`ApiResponse<null>`.

#### `GET /api/clubs/{clubId}/reading-sprints/{sprintId}/milestones/{milestoneId}/responses?page=1&pageSize=20`

Response `200`:
`ApiResponse<PageResult<ReadingSprintMilestoneResponse>>`.

#### `POST /api/clubs/{clubId}/reading-sprints/{sprintId}/milestones/{milestoneId}/responses` — Active participant

Request: `{ "content": "Mình chú ý đến cách tác giả tách policy khỏi framework." }`.
Participant được đăng nhiều response dạng thread; `content` tối đa 2.000 ký tự.
Không có endpoint sửa response.
Response `201`: `ApiResponse<ReadingSprintMilestoneResponse>`.

#### `DELETE /api/clubs/{clubId}/reading-sprints/{sprintId}/milestone-responses/{responseId}` — Author hoặc club OWNER/MODERATOR

Chỉ sprint `ACTIVE`. Soft delete. Response `200`: `ApiResponse<null>`.

#### `POST /api/clubs/{clubId}/reading-sprints/{sprintId}/reminders` — Club OWNER/MODERATOR

Chỉ sprint `ACTIVE`. Tối đa một lần gửi trong mỗi ngày UTC cho cùng sprint; gọi
lại trong ngày trả detail hiện tại và không tạo notification trùng. Recipient
là active participant chưa đạt target, còn là club member và khác actor.
Response `200`: `ApiResponse<ReadingSprintDetailResponse>`.

#### `POST /api/clubs/{clubId}/reading-sprints/{sprintId}/complete` — Club OWNER/MODERATOR

Không complete sprint `PLANNED`. Complete lại sprint đã `COMPLETED` là
idempotent; sprint `CANCELLED` không thể complete.
Response `200`: `ApiResponse<ReadingSprintDetailResponse>`.

#### `POST /api/clubs/{clubId}/reading-sprints/{sprintId}/cancel` — Club OWNER/MODERATOR

Cancel lại sprint đã `CANCELLED` là idempotent; sprint `COMPLETED` không thể
cancel. Response `200`: `ApiResponse<ReadingSprintDetailResponse>`.

Private club chỉ nhận thành viên qua invitation. Bookstore có thể bổ sung metadata
sách nhưng không tham gia xác thực, membership hoặc quyền quản trị club.
Reading sprint chỉ dùng `Book.Id` nội bộ và vẫn hoạt động đầy đủ khi Bookstore
integration tắt hoặc lỗi.

## 15. Challenge API

### `GET /api/challenges?page=1&pageSize=20` — Public

Public chỉ nhận challenge đã xuất bản. Response `200`: `ApiResponse<PageResult<ChallengeResponse>>`.

### `GET /api/challenges/{challengeId}` — Public

Chỉ trả challenge đã xuất bản. Bản nháp chỉ có mặt trong danh sách quản trị. Response `200`: `ApiResponse<ChallengeResponse>`.

### `POST /api/challenges/{challengeId}/join` — Authenticated

Challenge phải đã publish và chưa kết thúc. Application tạo participation, suy ra
initial progress/completion từ thư viện thật và chèn completion notification liên
quan trong cùng transaction. Response chỉ được trả sau khi operation này commit;
nếu lỗi xảy ra trước commit thì toàn bộ transaction rollback. Response `200`:
`ApiResponse<ChallengeResponse>` đã phản ánh progress vừa commit.

Join dùng serialized, non-deferred SQLite challenge-mutation boundary chung với
unpublish/delete; write lock được lấy trước khi đọc điều kiện và giữ đến commit.
Nếu join commit trước, admin mutation đồng thời trả conflict vì đã có participant;
nếu admin mutation commit trước, join trả conflict hoặc not-found vì challenge
không còn hợp lệ. Mọi eligibility/precondition read trong boundary là async, nhận
cùng request cancellation token và không chạy trước khi lấy lock.

Nếu operation ném trước khi bắt đầu commit thì transaction rollback. Application
không chạy DB work hoặc follow-up read sau commit. Cancellation hoặc mất kết nối
trong lúc/sau commit tạo commit-ack ambiguity; khi không nhận được response, client
phải đọc lại detail hoặc `/api/challenges/my`. Retry join đã commit có thể trả
`409 CHALLENGE_ALREADY_JOINED`.

### `DELETE /api/challenges/{challengeId}/join` — Authenticated

Application load participation, xóa, đồng bộ trạng thái còn lại và map DTO trong
cùng transaction. Controller chỉ trả DTO đã commit, không gọi detail/sync lần hai.
Response `200`: `ApiResponse<ChallengeResponse>` có `isJoined=false`,
`currentBooks=0`. Retry DELETE sau khi đã rời trả
`404 CHALLENGE_PARTICIPATION_NOT_FOUND`.
Nếu cancellation/mất response xảy ra trong lúc hoặc sau commit, client dùng detail
hoặc `/api/challenges/my` để đối soát.

### `GET /api/challenges/my?page=1&pageSize=20` — Authenticated

Response `200`: `ApiResponse<PageResult<ChallengeResponse>>`.

Không có endpoint write-progress. `/api/challenges/my` là route canonical;
`/api/challenges/mine` được giữ làm alias tương thích. Khi mutation thư viện hoặc
phiên đọc lần đầu hoàn tất sách, server lưu dữ liệu đọc và đồng bộ challenge
trong cùng transaction. List, detail, `/my` và dashboard vẫn đồng bộ lại trước
khi map/filter/phân trang để sửa dữ liệu cũ. Progress dùng atomic max ở database.
Lần đầu đạt mục tiêu tạo notification `CHALLENGE` với link
`/challenges/{challengeId}` và event key riêng có unique index; request đồng thời
hoặc đọc lại qua các surface không tạo notification trùng. Application sở hữu
việc suy ra progress, quyết định completion và nội dung notification;
Infrastructure chỉ cung cấp transaction và các primitive persistence
provider-specific như atomic high-water update, unique insert và retry khi cần.

## 16. Admin challenge API

### `GET /api/admin/challenges?page=1&pageSize=50` — ADMIN

Trả cả bản nháp lẫn challenge đã xuất bản để quản trị tiếp tục chỉnh sửa, xuất bản hoặc xóa theo vòng đời nghiệp vụ. Response `200`: `ApiResponse<PageResult<ChallengeResponse>>`.

### `POST /api/admin/challenges` — ADMIN

Request:

```json
{
  "title": "12 cuốn trong 12 tuần",
  "description": "Hoàn thành 12 cuốn trong thời gian diễn ra.",
  "startDate": "2026-08-01T00:00:00Z",
  "endDate": "2026-10-23T23:59:59Z",
  "goalBooks": 12,
  "coverImageUrl": "https://images.example.com/challenges/12-books.jpg"
}
```

Response `201`: `ApiResponse<ChallengeResponse>`, mặc định draft.

### `PATCH /api/admin/challenges/{challengeId}` — ADMIN

Request dùng đủ payload như create. Sau publish vẫn có thể cập nhật title, description và ảnh bìa, nhưng không thể đổi `goalBooks`, `startDate` hoặc `endDate` (trả `409 CHALLENGE_RULES_LOCKED`).

Response `200`: `ApiResponse<ChallengeResponse>`.

### `PATCH /api/admin/challenges/{challengeId}/publish` — ADMIN

Request:

```json
{
  "isPublished": true
}
```

Response `200`: `ApiResponse<ChallengeResponse>`.

Đặt `isPublished=false` chỉ thành công khi challenge không có bất kỳ row vật lý
`ChallengeParticipation` nào.

### `DELETE /api/admin/challenges/{challengeId}` — ADMIN

Chỉ draft không có bất kỳ row vật lý `ChallengeParticipation` nào được xóa mềm.
Response `200`: `ApiResponse<null>`.

Trong guard unpublish/delete, “participant” nghĩa là mọi row vật lý có
`ChallengeId` tương ứng, bỏ qua global query filters: gồm row có `DeletedAt` và row
thuộc user có `DeletedAt`. Guard được đánh giá sau khi lấy cùng serialized write
lock mà join sử dụng, để restoration không làm participation cũ xuất hiện lại trên
challenge draft/đã xóa. Thứ tự commit quyết định command thắng: join thắng làm
admin mutation trả conflict; admin mutation thắng làm join trả
conflict/not-found. Publish `true` và update challenge không thuộc boundary hẹp
này. Acquire/retry write lock dùng cửa sổ ngắn và tôn trọng request cancellation;
nếu bị hủy trước khi lấy lock thì callback nghiệp vụ không chạy và không có
mutation được commit.

Nếu admin không nhận được response trong lúc/sau commit, danh sách quản trị là
nguồn đối soát trạng thái.

## 17. Notification API

Tất cả endpoint chỉ truy cập notification của principal.

### `GET /api/notifications?unreadOnly=false&page=1&pageSize=20`

Response `200`: `ApiResponse<PageResult<NotificationResponse>>`.

### `GET /api/notifications/unread-count`

Response data: `{ "count": 3 }`.

### `PATCH /api/notifications/{notificationId}/read`

Idempotent. Response `200`: `ApiResponse<NotificationResponse>`.

### `PATCH /api/notifications/read-all`

Idempotent. Response `200`: `ApiResponse<null>`.

## 18. Dashboard API

### `GET /api/dashboard` — Authenticated

Response `200`: `ApiResponse<DashboardResponse>`.

Mọi thống kê lấy từ BookSpace DB, không gọi Bookstore.

## 19. External provider API

### `GET /api/external-books/search?query=clean+code&limit=20`

Đây là surface discovery tùy chọn. Khi query không rỗng, endpoint luôn trả
`200` với `ApiResponse<ExternalBookSearchResult>`; client dùng `available` để
biết provider có đang dùng được hay không.

Response data:

```json
{
  "available": true,
  "provider": "bookstore",
  "message": "Thành công",
  "items": [
    {
      "externalId": "bookstore-book-id",
      "title": "Clean Code",
      "authors": [
        "Robert C. Martin"
      ],
      "coverImageUrl": "https://provider.example.com/clean-code.jpg",
      "isbn": "9780132350884",
      "price": 180000,
      "purchaseUrl": "https://provider.example.com/books/bookstore-book-id"
    }
  ]
}
```

Endpoint không tạo BookSpace `Book` và không thay đổi catalog nội bộ.

Khi Bookstore tắt hoặc upstream không phản hồi, response vẫn có envelope thành
công với `available: false`, `items: []` và message có thể hiển thị cho người
dùng. Điều này giữ cho BookSpace độc lập và không biến trạng thái của provider
thành lỗi của core API.

## 20. Error registry

| Code | HTTP | Điều kiện |
|---|---:|---|
| `VALIDATION_ERROR` | 400 | field/request sai |
| `UNAUTHORIZED` | 401 | access token thiếu/sai |
| `INVALID_CREDENTIALS` | 401 | đăng nhập sai |
| `EMAIL_ALREADY_EXISTS` | 409 | email trùng |
| `INVALID_REFRESH_TOKEN` | 401 | refresh token không hợp lệ, hết hạn hoặc đã bị thu hồi |
| `WEAK_PASSWORD` | 400 | password chưa đạt yêu cầu |
| `ACCOUNT_UNAVAILABLE` | 400 | tài khoản bị khóa hoặc không còn khả dụng |
| `FORBIDDEN` | 403 | không đủ quyền |
| `USER_NOT_FOUND` | 404 | user không tồn tại |
| `INVALID_USER_SEARCH` | 400 | search độc giả khác rỗng ngoài 2-100 ký tự |
| `CANNOT_FOLLOW_SELF` | 400 | tự follow |
| `ALREADY_FOLLOWING` | 409 | follow trùng |
| `BOOK_NOT_FOUND` | 404 | sách không tồn tại |
| `ISBN_ALREADY_EXISTS` | 409 | ISBN trùng |
| `AUTHOR_NOT_FOUND` | 404 | tác giả không tồn tại |
| `CATEGORY_NOT_FOUND` | 404 | category không tồn tại |
| `AUTHOR_ALREADY_EXISTS` | 409 | tên tác giả trùng |
| `CATEGORY_ALREADY_EXISTS` | 409 | tên category trùng |
| `AUTHOR_IN_USE` | 409 | tác giả đang được gắn với book |
| `CATEGORY_IN_USE` | 409 | category đang được gắn với book |
| `LIBRARY_ITEM_NOT_FOUND` | 404 | item không thuộc principal |
| `BOOK_ALREADY_IN_LIBRARY` | 409 | sách đã ở library |
| `INVALID_READING_PROGRESS` | 400 | trang ngoài giới hạn |
| `READING_PROGRESS_CANNOT_DECREASE` | 409 | tiến độ bị lùi |
| `INVALID_READING_DATE` | 400 | ngày/giờ session không hợp lệ |
| `INVALID_READING_DURATION` | 400 | duration session không khớp |
| `INVALID_PAGES_READ` | 400 | số trang trong session vượt book |
| `READING_GOAL_NOT_FOUND` | 404 | goal không tồn tại/không thuộc principal |
| `INVALID_READING_GOAL_METRIC` | 400 | metric không thuộc `BOOKS`, `PAGES`, `MINUTES` |
| `INVALID_READING_GOAL_PERIOD` | 400 | period không thuộc `WEEK`, `MONTH`, `YEAR`, `CUSTOM` |
| `INVALID_READING_GOAL_TARGET` | 400 | target không nằm trong 1..1.000.000 |
| `INVALID_READING_GOAL_DATE` | 400 | end không hợp lệ, không ở tương lai hoặc thời lượng quá 366 ngày |
| `READING_GOAL_OVERLAPS` | 409 | overlap với goal active cùng metric |
| `READING_GOAL_ALREADY_COMPLETED` | 400 | cố update goal đã hoàn thành |
| `READING_GOAL_ALREADY_EXPIRED` | 400 | cố update goal đã hết hạn |
| `READING_NOTE_NOT_FOUND` | 404 | note không tồn tại/không thuộc principal |
| `READING_NOTE_CONTENT_REQUIRED` | 400 | thiếu cả quote và content |
| `INVALID_NOTE_PAGE_NUMBER` | 400 | page không trong phạm vi của book |
| `INVALID_READING_NOTE_TAG` | 400 | tag sai ký tự, số lượng hoặc độ dài |
| `INVALID_READING_NOTE_SEARCH` | 400 | search dài quá 200 ký tự |
| `INVALID_INSIGHTS_RANGE` | 400 | `days` không thuộc 30, 90, 365 |
| `INVALID_INSIGHTS_YEAR` | 400 | calendar year ngoài 1900..năm local hiện tại |
| `INVALID_INSIGHTS_WEEKS` | 400 | `weeks` ngoài 4..52 |
| `INVALID_INSIGHTS_MONTHS` | 400 | `months` không thuộc 6, 12, 24 |
| `INVALID_UTC_OFFSET` | 400 | `utcOffsetMinutes` ngoài -840..840 |
| `REVIEW_NOT_FOUND` | 404 | review không tồn tại |
| `REVIEW_ALREADY_EXISTS` | 409 | user đã review sách |
| `COMMENT_NOT_FOUND` | 404 | comment không tồn tại |
| `CLUB_NOT_FOUND` | 404 | club không tồn tại/không được xem |
| `PRIVATE_CLUB` | 403 | cố join private club |
| `ALREADY_CLUB_MEMBER` | 409 | membership trùng |
| `CLUB_MEMBERSHIP_REQUIRED` | 403 | thao tác yêu cầu membership |
| `CLUB_MEMBERSHIP_NOT_FOUND` | 404 | chưa là member |
| `OWNER_CANNOT_LEAVE` | 409 | owner cố leave |
| `CLUB_POST_NOT_FOUND` | 404 | post không tồn tại |
| `READING_SPRINT_NOT_FOUND` | 404 | sprint không thuộc club hoặc không tồn tại |
| `INVALID_BOOK_ID` | 400 | UUID book rỗng trong request sprint |
| `INVALID_READING_SPRINT_PERIOD` | 400 | `endsAt` không sau `startsAt` |
| `READING_SPRINT_END_MUST_BE_FUTURE` | 400 | `endsAt` không ở tương lai |
| `INVALID_READING_SPRINT_TARGET_UNIT` | 400 | target unit không phải `PAGES`/`CHAPTERS` |
| `INVALID_READING_SPRINT_TARGET` | 400 | target không dương hoặc ngoài giới hạn chung |
| `READING_SPRINT_TARGET_EXCEEDS_BOOK_PAGES` | 400 | target `PAGES` vượt page count |
| `READING_SPRINT_CHAPTER_TARGET_TOO_LARGE` | 400 | target `CHAPTERS` vượt 500 |
| `READING_SPRINT_UPDATE_NOT_ALLOWED` | 409 | sửa sprint không còn `PLANNED` |
| `READING_SPRINT_TARGET_UNIT_LOCKED` | 409 | đổi unit sau khi có participant/milestone |
| `READING_SPRINT_TARGET_BELOW_PROGRESS` | 409 | hạ target dưới progress lớn nhất |
| `READING_SPRINT_TARGET_BELOW_MILESTONE` | 409 | hạ target dưới milestone lớn nhất |
| `READING_SPRINT_PARTICIPATION_NOT_ALLOWED` | 409 | join/leave ngoài `PLANNED`/`ACTIVE` |
| `READING_SPRINT_PARTICIPANT_NOT_FOUND` | 404 | principal chưa có participant |
| `READING_SPRINT_PARTICIPATION_INACTIVE` | 403 | participant đã leave |
| `READING_SPRINT_PARTICIPATION_REQUIRED` | 403 | thảo luận khi chưa là active participant |
| `READING_SPRINT_PROGRESS_CANNOT_DECREASE` | 409 | progress tuyệt đối bị giảm |
| `INVALID_READING_SPRINT_PROGRESS` | 400 | progress âm hoặc vượt target |
| `READING_SPRINT_NOT_ACTIVE` | 409 | mutation chỉ hợp lệ khi sprint `ACTIVE` |
| `READING_SPRINT_MILESTONE_NOT_FOUND` | 404 | milestone không tồn tại/đã soft-delete |
| `INVALID_READING_SPRINT_MILESTONE_TARGET` | 400 | target milestone ngoài 1..sprint target |
| `READING_SPRINT_MILESTONE_MUTATION_NOT_ALLOWED` | 409 | milestone mutation ngoài `PLANNED`/`ACTIVE` |
| `READING_SPRINT_RESPONSE_NOT_FOUND` | 404 | response không thuộc sprint/không tồn tại |
| `READING_SPRINT_RESPONSE_DELETE_FORBIDDEN` | 403 | không phải author hoặc club manager |
| `READING_SPRINT_NOT_STARTED` | 409 | complete sprint `PLANNED` |
| `READING_SPRINT_ALREADY_COMPLETED` | 409 | cancel sprint đã complete |
| `READING_SPRINT_ALREADY_CANCELLED` | 409 | complete sprint đã cancel |
| `CHALLENGE_NOT_FOUND` | 404 | challenge không tồn tại |
| `CHALLENGE_NOT_PUBLISHED` | 409 | join draft |
| `CHALLENGE_NOT_ACTIVE` | 409 | chưa bắt đầu/đã kết thúc |
| `CHALLENGE_ALREADY_JOINED` | 409 | participation trùng |
| `CHALLENGE_NOT_JOINED` | 404 | chưa tham gia |
| `CHALLENGE_RULES_LOCKED` | 409 | không đổi mục tiêu hoặc thời gian sau publish |
| `CHALLENGE_DELETE_REQUIRES_DRAFT` | 409 | chỉ xóa bản nháp |
| `CHALLENGE_HAS_PARTICIPANTS` | 409 | không unpublish/xóa khi còn bất kỳ row vật lý participation nào, kể cả row bị global filter ẩn |
| `NOTIFICATION_NOT_FOUND` | 404 | notification không thuộc principal |
| `ROUTE_NOT_FOUND` | 404 | route hoặc tài nguyên HTTP không tồn tại |
| `INTERNAL_ERROR` | 500 | lỗi không dự kiến |

## 21. Token và CORS

- JWT claim `sub` là BookSpace `User.Id`.
- JWT claim `role` là `USER` hoặc `ADMIN`.
- API không chấp nhận JWT do Bookstore ký trong Goal 1.
- Refresh token không xuất hiện trong URL hoặc log.
- Development allowlist mặc định `http://localhost:5173`.
- Production dùng allowlist cụ thể; không dùng wildcard với credential.
