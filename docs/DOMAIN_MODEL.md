# BookSpace — Mô hình miền

> Đây là inventory entity và luật nghiệp vụ chuẩn cho Goal 1.<br>
> Tên C# dùng PascalCase; JSON dùng camelCase.

## 1. Quy ước chung

### 1.1 Identity và thời gian

- Mọi entity có `Id: Guid`; API biểu diễn bằng chuỗi UUID chuẩn.
- Aggregate/entity có vòng đời độc lập dùng `CreatedAt`, `UpdatedAt`, `DeletedAt?`.
- Join entity bất biến có thể chỉ dùng khóa ghép và `CreatedAt`.
- Mọi thời điểm lưu UTC bằng `DateTimeOffset`; API trả ISO 8601 có hậu tố `Z`.
- `DeletedAt != null` nghĩa là đã soft delete và bị loại khỏi truy vấn mặc định.
- Không sử dụng ID của Bookstore làm khóa chính BookSpace.

### 1.2 Chuẩn hóa dữ liệu

- Email: trim, lowercase để so sánh; giữ một giá trị chuẩn duy nhất.
- Chuỗi hiển thị: trim hai đầu; chuỗi chỉ có khoảng trắng được xem là rỗng.
- URL ảnh: `http`/`https`, tối đa 2.048 ký tự; có thể `null`.
- Nội dung người dùng nhập phải được render dạng text/Markdown đã sanitize, không render HTML thô.
- Phân trang bắt đầu từ trang 1.

### 1.3 Quyền sở hữu

- `ADMIN` có thể xóa review, comment và bài đăng vi phạm; không thể sửa hồ sơ người khác trong Goal 1.
- Chủ sở hữu được sửa/xóa nội dung của mình nếu entity còn hoạt động.
- Quyền `OWNER/MODERATOR/MEMBER` chỉ có ý nghĩa bên trong một câu lạc bộ.

## 2. Bounded context Identity & Profile

### 2.1 `User`

Aggregate root của tài khoản và hồ sơ.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | tạo phía server |
| `Email` | `string` | có | email hợp lệ, unique không phân biệt hoa thường, tối đa 320 |
| `PasswordHash` | `string` | có | chỉ lưu hash qua password hasher, không trả API |
| `DisplayName` | `string` | có | 2–100 ký tự sau trim |
| `Bio` | `string?` | không | tối đa 500 ký tự |
| `AvatarUrl` | `string?` | không | URL hợp lệ, tối đa 1.000 |
| `Role` | `UserRole` | có | `USER` hoặc `ADMIN`; đăng ký công khai luôn là `USER` |
| `IsLocked` | `bool` | có | khóa đăng nhập nhưng không xóa dữ liệu |
| `CreatedAt` | `DateTimeOffset` | có | UTC |
| `UpdatedAt` | `DateTimeOffset` | có | không trước `CreatedAt` |
| `DeletedAt` | `DateTimeOffset?` | không | tài khoản bị vô hiệu hóa/soft delete |

Invariant:

- Email đang hoạt động là duy nhất.
- Không cho đăng nhập khi `DeletedAt` khác `null` hoặc `IsLocked=true`.
- Không cho client tự đặt `Role`.
- Xóa tài khoản thu hồi toàn bộ refresh token còn hiệu lực.
- Hồ sơ công khai không bao giờ lộ `PasswordHash`, token hoặc email nếu chính sách response không cho phép.

### 2.2 `RefreshToken`

Entity thuộc vòng đời xác thực của `User`.

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | server tạo |
| `UserId` | `Guid` | FK tới `User` đang hoạt động |
| `TokenHash` | `string` | unique; không lưu token thô |
| `ExpiresAt` | `DateTimeOffset` | sau `CreatedAt` |
| `RevokedAt` | `DateTimeOffset?` | có giá trị khi logout/rotate/revoke |
| `ReplacedByTokenId` | `Guid?` | token mới trong chuỗi rotation |
| `CreatedAt` | `DateTimeOffset` | UTC |

Token hợp lệ khi chưa hết hạn, chưa thu hồi và user còn hoạt động. Refresh thành công phải thu hồi token cũ và tạo token mới.

### 2.3 `Follow`

Quan hệ có hướng giữa hai `User`.

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | server tạo |
| `FollowerId` | `Guid` | người thực hiện follow |
| `FollowingId` | `Guid` | người được follow |
| `CreatedAt` | `DateTimeOffset` | UTC |

Invariant:

- `FollowerId != FollowingId`.
- Unique `(FollowerId, FollowingId)`.
- Cả hai user phải đang hoạt động.
- Unfollow xóa quan hệ; không ảnh hưởng nội dung lịch sử.

## 3. Bounded context Catalog

### 3.1 `Author`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `Name` | `string` | có | 1–160 ký tự |
| `Biography` | `string?` | không | tối đa 5.000 |
| `AvatarUrl` | `string?` | không | URL hợp lệ |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Tên không cần unique tuyệt đối vì tác giả có thể trùng tên. Tìm kiếm sử dụng tên đã normalize.

### 3.2 `Category`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `Name` | `string` | có | 1–100 ký tự, unique không phân biệt hoa thường |
| `Description` | `string?` | không | tối đa 500 |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

### 3.3 `Book`

Aggregate root catalog.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `Title` | `string` | có | 1–300 ký tự |
| `Description` | `string?` | không | tối đa 10.000 |
| `Isbn` | `string?` | không | ISBN-10/ISBN-13 sau khi bỏ dấu nối; unique nếu có |
| `CoverImageUrl` | `string?` | không | URL hợp lệ, tối đa 1.000 |
| `PageCount` | `int` | có | 1–20.000 |
| `PublishedYear` | `int?` | không | 1000..2200 |
| `Language` | `string?` | không | mã/ngôn ngữ hiển thị, tối đa 50 |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Invariant:

- Một sách hoạt động phải có ít nhất một `BookAuthor`.
- `AverageRating` và `ReviewCount` là projection từ review đang hoạt động, không phải nguồn sự thật có thể sửa từ API.
- Không xóa cứng sách đã xuất hiện trong library, review, session hoặc challenge.

### 3.4 `BookAuthor`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `BookId` | `Guid` | FK `Book` |
| `AuthorId` | `Guid` | FK `Author` |

Khóa duy nhất `(BookId, AuthorId)`. Không liên kết entity đã soft delete.

### 3.5 `BookCategory`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `BookId` | `Guid` | FK `Book` |
| `CategoryId` | `Guid` | FK `Category` |

Khóa duy nhất `(BookId, CategoryId)`. Một sách có thể chưa có category, nhưng phải có tác giả.

## 4. Bounded context Reading

### 4.1 `LibraryItem`

Aggregate cho trạng thái một cuốn sách trong thư viện một người.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `UserId` | `Guid` | có | owner |
| `BookId` | `Guid` | có | sách đang hoạt động |
| `Status` | `LibraryStatus` | có | `WANT_TO_READ`, `READING`, `READ`; API map thành `shelf` |
| `CurrentPage` | `int` | có | 0..`Book.PageCount` |
| `StartedAt` | `DateTimeOffset?` | không | được đặt khi bắt đầu |
| `FinishedAt` | `DateTimeOffset?` | không | bắt buộc khi hoàn thành |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Invariant:

- Unique active `(UserId, BookId)`.
- `WANT_TO_READ`: `CurrentPage = 0`, `FinishedAt = null`.
- `READING`: `StartedAt != null`, `0 <= CurrentPage < PageCount`, `FinishedAt = null`.
- `READ`: `StartedAt != null`, `CurrentPage = PageCount`, `FinishedAt != null`.
- Tiến độ API không được làm `CurrentPage` giảm. Luồng đọc lại phải được biểu diễn bằng hành động đổi `READ -> READING`, đặt mốc đọc lại mới nhưng giữ `ReadingSession` cũ.
- `FinishedAt >= StartedAt`.

### 4.2 `ReadingSession`

Nhật ký một phiên đọc đã hoàn tất.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `UserId` | `Guid` | có | owner |
| `BookId` | `Guid` | có | sách trong catalog |
| `StartedAt` | `DateTimeOffset` | có | không ở tương lai quá 5 phút |
| `EndedAt` | `DateTimeOffset?` | không | không trước `StartedAt` |
| `PagesRead` | `int` | có | 1..`Book.PageCount` |
| `DurationMinutes` | `int` | có | 1..1.440 |
| `Note` | `string?` | không | tối đa 2.000 |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Invariant:

- Chỉ owner được xem. Reading session là nhật ký append-only trong Goal 1.
- Một phiên có thể tăng `CurrentPage` của `LibraryItem` tương ứng lên `min(CurrentPage + PagesRead, PageCount)`.
- Ghi session phải tạo library item `READING` nếu chưa tồn tại.
- Nếu đạt `PageCount`, library item chuyển `READ` và đặt `FinishedAt`.

### 4.3 `ReadingGoal`

Mục tiêu đọc cá nhân, thuộc riêng một `User`. Mục tiêu không lưu tiến độ đếm được; tiến độ được tính lại từ dữ liệu đọc đã có khi service map response.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `UserId` | `Guid` | có | owner, FK `User` restrict delete |
| `Metric` | `ReadingGoalMetric` | có | `BOOKS`, `PAGES`, `MINUTES` |
| `Period` | `ReadingGoalPeriod` | có | `WEEK`, `MONTH`, `YEAR`, `CUSTOM`; nhãn phân loại, không tự suy diễn ngày |
| `TargetValue` | `int` | có | 1..1.000.000 |
| `StartDate`, `EndDate` | `DateTimeOffset` | có | UTC/ISO 8601 qua API |
| `CompletedAt` | `DateTimeOffset?` | không | được đặt đúng một lần khi đạt target |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

`CurrentValue`, `ProgressPercent` và `Status` là các trường response suy ra, không phải cột aggregate:

- `BOOKS`: đếm `LibraryItem` của owner có `Status=READ`, `FinishedAt` trong đoạn đóng `[StartDate, EndDate]`.
- `PAGES`: tổng `ReadingSession.PagesRead` của owner có `StartedAt` trong đoạn đóng đó.
- `MINUTES`: tổng `ReadingSession.DurationMinutes` theo cùng điều kiện.
- `ProgressPercent = round(CurrentValue * 100 / TargetValue)`, bị chặn trong 0..100.
- `Status`: `COMPLETED` khi có `CompletedAt`; nếu chưa hoàn thành và `now > EndDate` là `EXPIRED`; còn lại là `ACTIVE`.

Invariant:

- `EndDate > StartDate`; thời lượng không quá 366 ngày. Create/update còn yêu cầu `EndDate` nằm ở tương lai.
- `Metric` và `Period` bắt buộc là giá trị enum đã khai báo; giá trị không xác định bị từ chối.
- Không có hai mục tiêu chưa hoàn thành, chưa hết hạn của cùng `UserId` và `Metric` có khoảng thời gian giao nhau. Hai khoảng chỉ chạm đầu/cuối nhau không bị coi là giao nhau.
- Chỉ owner được list/detail/create/update/delete; truy cập ID không thuộc owner trả not-found để không lộ dữ liệu.
- Sau khi hoàn thành hoặc hết hạn, mục tiêu không thể update. Không có API client ghi `CurrentValue`, `Status` hoặc `CompletedAt`.
- Ở lần đánh giá đầu tiên thấy `CurrentValue >= TargetValue`, hệ thống đặt `CompletedAt` và tạo đúng một `Notification` kiểu `SYSTEM`, link `/goals`.

Persistence: bảng `reading_goals`, index `(UserId, Metric, StartDate, EndDate)` và `(UserId, CompletedAt, EndDate)`, global filter bỏ row soft-delete.

### 4.4 `ReadingNote`

Ghi chú đọc riêng tư của một `User` cho một `Book` trong catalog. `BookId` không yêu cầu có `LibraryItem` tương ứng.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `UserId` | `Guid` | có | owner, FK `User` restrict delete |
| `BookId` | `Guid` | có | FK `Book` restrict delete; book phải tồn tại |
| `PageNumber` | `int?` | không | nếu có: 1..`Book.PageCount` |
| `Quote` | `string?` | không | trim, tối đa 500 ký tự |
| `Content` | `string?` | không | trim, tối đa 5.000 ký tự |
| `TagsCsv` | `string?` | không | lưu nội bộ bằng `|`; response là `tags: string[]` |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Invariant:

- Ít nhất một trong `Quote` hoặc `Content` phải có nội dung sau trim.
- Tag rỗng bị bỏ; tag còn lại được trim, de-duplicate không phân biệt hoa/thường, tối đa 10 tag, mỗi tag tối đa 30 ký tự, tổng chuỗi tối đa 500 ký tự và không chứa `|`.
- `BookId` chỉ có trong create request; update không đổi book của ghi chú.
- Chỉ owner được đọc/sửa/xóa. Ghi chú không được đưa vào community/feed/review/notification.
- List hỗ trợ filter `BookId`, tag chính xác không phân biệt hoa/thường và `search` (quote, content, tags); sort theo `UpdatedAt ?? CreatedAt` giảm dần.

Persistence: bảng `reading_notes`, index `(UserId, BookId, CreatedAt)` và `(UserId, UpdatedAt)`, global filter bỏ row soft-delete.

### 4.5 `ReadingInsights` — derived read model

`ReadingInsights` không phải entity, không có identity, lifecycle hay bảng persistence riêng. Đây là read model riêng tư được dựng theo request từ các nguồn chuẩn:

- `ReadingSession`: số phiên, trang, phút, ngày hoạt động và tốc độ theo sách.
- `LibraryItem`: sách đang đọc, current page, page count và thời điểm hoàn thành.
- `ReadingGoal`: target, kỳ hạn, trạng thái và tiến độ được tính lại từ cùng nguồn như mục tiêu đọc.

Các projection trả qua API:

| Projection | Ý nghĩa |
|---|---|
| Overview | tổng hoạt động, trung bình theo ngày hoạt động, streak, goal summary, period comparison và forecast |
| Calendar | một ô cho mỗi ngày local trong rolling 30/90/365 ngày hoặc một năm lịch |
| Weekly | 4..52 tuần lịch, tuần bắt đầu thứ Hai |
| Monthly | 6, 12 hoặc 24 tháng lịch |
| Book forecast | library item `READING`; tốc độ bằng trang của đúng sách trong tối đa 30 ngày chia số ngày lịch từ hoạt động đầu tiên đến hôm nay |
| Goal forecast | dự báo cho goal `ACTIVE` bằng cùng nguồn progress của Reading Goal |

Quy tắc thời gian:

- Timestamps vẫn lưu UTC. Request cung cấp `UtcOffsetMinutes` trong -840..840; `420` nghĩa là UTC+7.
- Ngày local được đổi thành khoảng UTC nửa mở `[StartUtc, EndUtc)`, tránh đếm đôi đúng thời điểm nửa đêm.
- Một `ReadingSession` được gán toàn bộ cho ngày local chứa `StartedAt`; phiên xuyên nửa đêm không bị chia nhỏ.
- Calendar điền đầy đủ ngày rỗng và sắp tăng dần.
- Current streak đếm từ hôm nay nếu hôm nay active, nếu không được phép bắt đầu từ hôm qua; longest streak là kỷ lục toàn lịch sử của owner.
- Period comparison dùng hai cửa sổ liền kề cùng độ dài. Phần trăm thay đổi là `null` khi kỳ trước bằng 0 nhưng kỳ hiện tại dương, và bằng 0 khi cả hai kỳ đều bằng 0.

Read model không nhận `UserId` từ query. Controller luôn dùng principal hiện tại; user khác nhận tập kết quả độc lập, không thể suy ra dữ liệu owner. Việc dựng Insights được phép đồng bộ một Reading Goal vừa đạt target sang `COMPLETED`, nhưng completion và notification phải idempotent.

## 5. Bounded context Community

### 5.1 `Review`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `UserId` | `Guid` | có | author |
| `BookId` | `Guid` | có | sách được review |
| `Rating` | `int` | có | 1..5 |
| `Content` | `string` | có | 10–5.000 ký tự |
| `ContainsSpoilers` | `bool` | có | mặc định `false` |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Invariant:

- Unique active `(UserId, BookId)`.
- Chủ review được sửa/xóa; sửa rating cập nhật projection rating.
- Nội dung spoiler phải bị che ở UI cho tới khi người xem chủ động mở.

### 5.2 `ReviewLike`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | server tạo |
| `ReviewId` | `Guid` | review đang hoạt động |
| `UserId` | `Guid` | người like |
| `CreatedAt` | `DateTimeOffset` | UTC |

Unique `(ReviewId, UserId)`. Endpoint like là idempotent; gọi lại không tạo bản ghi thứ hai. Unlike cũng idempotent.

### 5.3 `ReviewComment`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `ReviewId` | `Guid` | có | review đang hoạt động |
| `UserId` | `Guid` | có | author |
| `Content` | `string` | có | 1–2.000 ký tự |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Goal 1 không có comment lồng nhau. Xóa review làm comment không còn hiển thị nhưng vẫn giữ lịch sử.

### 5.4 Feed

Feed là read model/projection, không phải entity ghi độc lập trong Goal 1. Nguồn dữ liệu:

- `Review.CreatedAt`
- lần đầu `LibraryItem` chuyển sang `READING` hoặc `READ`
- `ClubPost.CreatedAt` nếu người xem có quyền xem câu lạc bộ
- `ChallengeParticipation.CompletedAt` khi người đọc hoàn tất thử thách đã xuất bản

Item feed có `type`, `actor`, `occurredAt`, `book?`, `review?`, `club?`. Kết quả sắp xếp `occurredAt desc, id desc`.

## 6. Bounded context Clubs

### 6.1 `BookClub`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `OwnerId` | `Guid` | có | một user đang hoạt động |
| `Name` | `string` | có | sau khi trim không rỗng, tối đa 150 ký tự |
| `Description` | `string?` | không | tối đa 2.000 ký tự |
| `CoverUrl` | `string?` | không | tối đa 1.000 ký tự; API kiểm tra URL hợp lệ |
| `Visibility` | `ClubVisibility` | có | `PUBLIC`, `PRIVATE` |
| `CurrentBookId` | `Guid?` | không | sách đang hoạt động trong catalog nội bộ |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Invariant:

- Tạo club đồng thời tạo membership `OWNER`.
- Mỗi club có đúng một owner hoạt động.
- Chỉ `OWNER` sửa tên, mô tả, ảnh bìa và visibility.
- `PUBLIC` cho join trực tiếp; `PRIVATE` chỉ nhận thành viên qua lời mời còn hiệu lực.
- `OWNER` hoặc `MODERATOR` được đặt/xóa sách đọc chung; đặt lại cùng sách là idempotent.

### 6.2 `BookClubMember`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | server tạo |
| `ClubId` | `Guid` | club đang hoạt động |
| `UserId` | `Guid` | member đang hoạt động |
| `Role` | `ClubMemberRole` | `OWNER`, `MODERATOR`, `MEMBER` |
| `JoinedAt` | `DateTimeOffset` | UTC |

Unique có điều kiện `(ClubId, UserId)` cho membership chưa bị soft-delete. Một club có đúng một row `OWNER`. Owner không thể leave và vai trò `OWNER` không thể gán qua endpoint đổi role. Chỉ owner promote/demote giữa `MEMBER` và `MODERATOR`. Moderator chỉ được loại member thường; owner có thể loại member hoặc moderator.

### 6.3 `ClubInvitation`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `ClubId` | `Guid` | có | club đang hoạt động |
| `InviterId` | `Guid` | có | owner hoặc moderator |
| `InvitedUserId` | `Guid` | có | tài khoản BookSpace được tìm bằng email chuẩn hóa |
| `Status` | `ClubInvitationStatus` | có | `PENDING`, `ACCEPTED`, `DECLINED`, `REVOKED`, `EXPIRED` |
| `ExpiresAt` | `DateTimeOffset` | có | UTC, sau thời điểm tạo |
| `RespondedAt` | `DateTimeOffset?` | không | thời điểm kết thúc trạng thái pending |
| `CreatedAt`, `UpdatedAt` | thời gian | có | audit |

Không mời chính mình hoặc user đã là member. Mỗi cặp `(ClubId, InvitedUserId)` chỉ có tối đa một lời mời `PENDING`. Gửi lại khi còn pending trả chính lời mời đó và không tạo notification trùng. Chỉ invitee accept/decline; owner hoặc moderator revoke. Accept lặp lại không tạo membership thứ hai.

### 6.4 `ClubPost`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `ClubId` | `Guid` | có | club |
| `UserId` | `Guid` | có | member tạo bài |
| `Content` | `string` | có | 1–10.000 ký tự |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Chỉ member đang hoạt động được tạo bài. Author, owner, moderator hoặc system admin được soft-delete bài theo quyền; Goal 1 không sửa bài sau khi đăng.

### 6.5 `ClubPostComment`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `ClubPostId` | `Guid` | có | post đang hoạt động |
| `UserId` | `Guid` | có | member tạo comment |
| `Content` | `string` | có | 1–2.000 ký tự |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

Không có comment lồng nhau trong Goal 1.

### 6.6 `ClubReadingSprint`

Đợt đọc chung thuộc đúng một `BookClub` và tham chiếu đúng một `Book` trong
catalog nội bộ; không lưu external Bookstore ID và không gọi provider để quyết
định nghiệp vụ.

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | server tạo |
| `ClubId` | `Guid` | club đang hoạt động |
| `BookId` | `Guid` | book catalog nội bộ đang hoạt động |
| `CreatedById` | `Guid` | owner/moderator tạo |
| `Title` | `string` | 1..200 ký tự sau trim |
| `Description` | `string?` | tối đa 2.000 ký tự |
| `StartsAt`, `EndsAt` | `DateTimeOffset` | UTC, `EndsAt > StartsAt` |
| `TargetUnit` | `ReadingSprintTargetUnit` | `PAGES`, `CHAPTERS` |
| `TargetValue` | `int` | `PAGES`: 1..`Book.PageCount`; `CHAPTERS`: 1..500 |
| `CompletedAt`, `CancelledAt` | `DateTimeOffset?` | explicit terminal marker, loại trừ nhau |
| `LastReminderAt` | `DateTimeOffset?` | UTC; khử trùng theo ngày UTC |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | audit/soft delete |

### 6.7 `ClubReadingSprintParticipant`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | giữ nguyên khi leave/rejoin |
| `SprintId`, `UserId` | `Guid` | unique theo cặp |
| `JoinedAt` | `DateTimeOffset` | thời điểm tham gia lần đầu |
| `LeftAt` | `DateTimeOffset?` | null nghĩa là active |
| `ProgressValue` | `int` | tuyệt đối, chỉ tăng, không vượt target |
| `CompletedAt` | `DateTimeOffset?` | đặt một lần khi progress đạt target |
| `LastCheckInAt` | `DateTimeOffset?` | lần progress thực sự tăng gần nhất |

Unique `(SprintId, UserId)` bảo đảm leave/rejoin không tạo participant thứ hai.
Index `(SprintId, LeftAt, ProgressValue)` phục vụ active leaderboard.

### 6.8 `ClubReadingSprintCheckIn`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `Guid` | server tạo |
| `ParticipantId`, `SprintId`, `UserId` | `Guid` | cùng một participant/sprint/actor |
| `ProgressValue` | `int` | snapshot tuyệt đối sau lần tăng |
| `Note` | `string?` | tối đa 1.000 ký tự |
| `CreatedAt` | `DateTimeOffset` | UTC, dùng cho timeline |

Gửi progress bằng giá trị hiện tại không tạo `ClubReadingSprintCheckIn`.
Timeline dùng index `(SprintId, CreatedAt)`.

### 6.9 `ClubReadingSprintMilestone`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id`, `SprintId`, `CreatedById` | `Guid` | server/sprint/manager |
| `Title` | `string` | 1..150 ký tự |
| `Description` | `string?` | tối đa 2.000 ký tự |
| `TargetValue` | `int` | 1..sprint target |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | audit/soft delete |

Index `(SprintId, TargetValue)` phục vụ detail theo thứ tự cột mốc.

### 6.10 `ClubReadingSprintMilestoneResponse`

| Trường | Kiểu | Quy tắc |
|---|---|---|
| `Id`, `MilestoneId`, `AuthorId` | `Guid` | server/milestone/participant |
| `Content` | `string` | 1..2.000 ký tự |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | audit/soft delete |

Thread dùng index `(MilestoneId, CreatedAt)`; không có unique theo author vì một
participant được đăng nhiều response.

Các invariant bắt buộc:

- Metric chỉ là `PAGES` hoặc `CHAPTERS`; target dương và ngày kết thúc phải sau ngày bắt đầu.
- Khi create/update, `EndsAt` phải ở tương lai. Đổi `TargetUnit` bị khóa nếu đã có participant hoặc milestone; target mới không được thấp hơn progress hoặc milestone lớn nhất.
- Chỉ `OWNER` hoặc `MODERATOR` của club đang hoạt động được tạo, sửa, hoàn tất, hủy sprint, quản lý milestone và gửi reminder.
- Trạng thái đọc được suy ra theo UTC là `PLANNED`, `ACTIVE` hoặc `ENDED`, trừ khi manager đã đặt `COMPLETED`/`CANCELLED`. Chỉ `PLANNED` và trước `StartsAt` được sửa luật sprint. Complete/cancel lại cùng trạng thái là idempotent; mọi mutation nghiệp vụ khác sau khi sprint kết thúc bị từ chối.
- Mỗi cặp sprint/user có tối đa một participant. Leave giữ lịch sử và đánh dấu participant không hoạt động; rejoin tái kích hoạt chính row đó, không tạo identity mới.
- Club leave/kick đồng thời đặt `LeftAt` cho participant active của mọi sprint chưa explicit `COMPLETED`/`CANCELLED`, kể cả status thời gian `ENDED`; check-in lịch sử không bị xóa.
- Chỉ club member đang hoạt động được join. Chỉ participant đang hoạt động được ghi progress hoặc phản hồi milestone.
- Progress là giá trị tuyệt đối trong `0..TargetValue`, chỉ tăng. Gửi cùng giá trị hiện tại không tạo activity mới. Phần trăm bằng `min(100, Progress * 100 / TargetValue)`.
- Leaderboard chỉ lấy participant đang hoạt động, sắp progress giảm dần rồi dùng tie-break định danh ổn định. Timeline chỉ lấy activity chưa bị ẩn của đúng sprint.
- Milestone và response là user-created content có soft delete. Manager được tạo/sửa/xóa milestone khi sprint `PLANNED` hoặc `ACTIVE`. Participant active được đăng nhiều response dạng thread; response không có update. Author hoặc club manager được soft-delete response, và quyền `canDelete` được tính theo principal khi map DTO.
- Mỗi sprint có tối đa một lần gửi reminder trong một ngày UTC. Dấu reminder và các notification tương ứng được lưu atomic để retry không tạo notification trùng.
- Private club áp dụng cùng visibility boundary cho list, detail, history, leaderboard, timeline, milestone và response; biết UUID không làm tăng quyền.

## 7. Bounded context Challenges

### 7.1 `ReadingChallenge`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `CreatedById` | `Guid` | có | ADMIN tạo challenge |
| `Title` | `string` | có | không rỗng, tối đa 200 ký tự |
| `Description` | `string?` | không | tối đa 2.000 ký tự |
| `GoalBooks` | `int` | có | 1..1.000 |
| `StartDate` | `DateTimeOffset` | có | UTC |
| `EndDate` | `DateTimeOffset` | có | sau `StartDate` |
| `CoverImageUrl` | `string?` | không | URL hợp lệ, tối đa 1.000 |
| `IsPublished` | `bool` | có | mặc định `false` |
| `CreatedAt`, `UpdatedAt`, `DeletedAt?` | thời gian | có | audit/soft delete |

`IsPublished=false` tương ứng `DRAFT`; `true` và còn hạn tương ứng `PUBLISHED`; đã qua `EndDate` là `ENDED` khi đọc. Không cần lưu enum trạng thái trùng lặp.

Invariant:

- Chỉ `ADMIN` tạo/sửa/xóa/publish.
- Sau publish không thay đổi `GoalBooks`, `StartDate`, `EndDate`.
- Chỉ challenge chưa bị xóa, đã publish và chưa hết hạn mới nhận participant mới.

### 7.2 `ChallengeParticipation`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `ReadingChallengeId` | `Guid` | có | challenge |
| `UserId` | `Guid` | có | participant |
| `CurrentBooks` | `int` | có | 0..`ReadingChallenge.GoalBooks` |
| `JoinedAt` | `DateTimeOffset` | có | trong thời gian cho phép |
| `CompletedAt` | `DateTimeOffset?` | không | đặt khi đạt mục tiêu |

Invariant:

- Unique `(ReadingChallengeId, UserId)`.
- `CurrentBooks` do participant cập nhật thủ công qua API; server chỉ nhận số tuyệt đối, không nhận phép cộng tùy ý.
- Tiến độ mới không được nhỏ hơn tiến độ hiện tại và không vượt `GoalBooks`.
- Đạt mục tiêu đặt `CompletedAt` đúng một lần.

## 8. Bounded context Notifications

### 8.1 `Notification`

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---:|---|
| `Id` | `Guid` | có | server tạo |
| `UserId` | `Guid` | có | người nhận |
| `Type` | `NotificationType` | có | loại sự kiện đã biết |
| `Title` | `string` | có | 1–160 ký tự |
| `Message` | `string` | có | 1–500 ký tự |
| `Link` | `string?` | không | internal path bắt đầu `/` hoặc URL cho phép |
| `ReadAt` | `DateTimeOffset?` | không | `null` là chưa đọc |
| `CreatedAt` | `DateTimeOffset` | có | UTC |
| `DeletedAt` | `DateTimeOffset?` | không | cleanup/soft delete |

`NotificationType` Goal 1:

- `FOLLOW`
- `REVIEW_LIKE`
- `COMMENT`
- `CLUB`
- `CHALLENGE`
- `SYSTEM`

Chỉ người nhận được truy cập và đánh dấu đọc. `mark read` và `mark all read` là idempotent.

## 9. Integration model

Goal 1 không persist entity bên ngoài. `ExternalBookResult` là DTO tạm thời:

| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `ExternalId` | `string` | ID chỉ có nghĩa trong provider |
| `Title` | `string` | tiêu đề |
| `Authors` | `string[]` | tên tác giả |
| `Isbn` | `string?` | dùng đối sánh |
| `CoverImageUrl` | `string?` | ảnh ngoài |
| `Price` | `decimal?` | giá tham khảo từ provider |
| `PurchaseUrl` | `string?` | trang nguồn/mua |

`Provider` và `Available` nằm ở `ExternalBookSearchResult`, không lặp trong từng item. DTO item không tự tạo `Book`, không được dùng làm FK và không thay thế catalog nội bộ.

## 10. Quan hệ tổng thể

```mermaid
erDiagram
    USER ||--o{ REFRESH_TOKEN : owns
    USER ||--o{ FOLLOW : follower
    USER ||--o{ FOLLOW : following
    USER ||--o{ LIBRARY_ITEM : owns
    USER ||--o{ READING_SESSION : logs
    USER ||--o{ READING_GOAL : owns
    USER ||--o{ READING_NOTE : owns
    USER ||--o{ REVIEW : writes
    USER ||--o{ REVIEW_LIKE : likes
    USER ||--o{ REVIEW_COMMENT : comments
    BOOK ||--o{ BOOK_AUTHOR : has
    AUTHOR ||--o{ BOOK_AUTHOR : writes
    BOOK ||--o{ BOOK_CATEGORY : has
    CATEGORY ||--o{ BOOK_CATEGORY : classifies
    BOOK ||--o{ LIBRARY_ITEM : appears_in
    BOOK ||--o{ READING_SESSION : read_in
    BOOK ||--o{ READING_NOTE : annotated_in
    BOOK ||--o{ REVIEW : reviewed_by
    REVIEW ||--o{ REVIEW_LIKE : receives
    REVIEW ||--o{ REVIEW_COMMENT : receives
    USER ||--o{ BOOK_CLUB : owns
    BOOK ||--o{ BOOK_CLUB : current_read
    BOOK_CLUB ||--o{ BOOK_CLUB_MEMBER : contains
    USER ||--o{ BOOK_CLUB_MEMBER : joins
    BOOK_CLUB ||--o{ CLUB_INVITATION : issues
    USER ||--o{ CLUB_INVITATION : invites
    USER ||--o{ CLUB_INVITATION : receives
    BOOK_CLUB ||--o{ CLUB_POST : contains
    CLUB_POST ||--o{ CLUB_POST_COMMENT : receives
    BOOK_CLUB ||--o{ CLUB_READING_SPRINT : hosts
    BOOK ||--o{ CLUB_READING_SPRINT : selected_for
    USER ||--o{ CLUB_READING_SPRINT : creates
    CLUB_READING_SPRINT ||--o{ CLUB_READING_SPRINT_PARTICIPANT : has
    USER ||--o{ CLUB_READING_SPRINT_PARTICIPANT : joins
    CLUB_READING_SPRINT ||--o{ CLUB_READING_SPRINT_CHECK_IN : records
    CLUB_READING_SPRINT_PARTICIPANT ||--o{ CLUB_READING_SPRINT_CHECK_IN : creates
    CLUB_READING_SPRINT ||--o{ CLUB_READING_SPRINT_MILESTONE : defines
    CLUB_READING_SPRINT_MILESTONE ||--o{ CLUB_READING_SPRINT_MILESTONE_RESPONSE : discusses
    USER ||--o{ CLUB_READING_SPRINT_MILESTONE_RESPONSE : writes
    READING_CHALLENGE ||--o{ CHALLENGE_PARTICIPATION : has
    USER ||--o{ CHALLENGE_PARTICIPATION : joins
    USER ||--o{ NOTIFICATION : receives
```

## 11. Transaction boundaries

Các thao tác sau phải atomic:

- Register: tạo `User` và hồ sơ tích hợp trong cùng entity.
- Refresh: thu hồi token cũ và tạo token mới.
- Create club: tạo `BookClub` và `BookClubMember(OWNER)`.
- Club leave/kick: soft-delete membership và đặt `LeftAt` cho participant active của sprint chưa explicit terminal.
- Join/rejoin reading sprint: tạo mới hoặc tái kích hoạt đúng một participant.
- Update sprint progress: cập nhật progress và chỉ tạo timeline activity khi giá trị thực sự tăng.
- Create milestone response: tạo một thread item mới cho participant active; author hoặc club manager soft-delete response theo quyền.
- Send sprint reminder: tạo dấu ngày UTC và notification cho active participant chưa đạt target, còn là club member và khác actor trong cùng transaction; retry cùng ngày không tạo thêm dữ liệu.
- Complete/cancel reading sprint: đặt trạng thái terminal đúng một lần và không phát side effect khi gọi lại.
- Record reading session: tạo session và cập nhật/tạo library item.
- Complete book: cập nhật library item, cập nhật challenge participation liên quan và tạo notification hoàn thành nếu đạt mục tiêu.
- Evaluate reading goal: tính lại tiến độ; nếu lần đầu đạt target thì lưu `CompletedAt` và tạo `Notification(SYSTEM, /goals)` trong cùng lần lưu. Khi list goals, mọi goal pending của owner được đồng bộ trước khi filter status và phân trang; không có client write-progress endpoint.
- Create/update/delete reading note: xác thực owner và book/page/tag invariant rồi lưu hoặc soft-delete aggregate.
- Delete review: soft-delete review; like/comment không còn được đọc qua API.

Notification có thể được tạo trong cùng transaction ở Goal 1. Không cần outbox/message broker cho monolith hiện tại.
