# BookSpace Backend

BookSpace là sản phẩm cộng đồng đọc sách độc lập, xây dựng bằng ASP.NET Core 10 và Clean Architecture. Hệ thống tự quản lý tài khoản, catalog, thư viện cá nhân, hành trình đọc, đánh giá, cộng đồng, câu lạc bộ, thử thách, thông báo và dashboard. Bookstore chỉ là một nguồn sách bên ngoài tùy chọn; BookSpace không dùng chung database và không cần Bookstore để chạy.

## Cấu trúc

```text
backend/
├── src/
│   ├── BookSpace.Domain/          # Entity, enum, domain behavior, domain exception
│   ├── BookSpace.Application/     # Use case, port, contract, pagination
│   ├── BookSpace.Infrastructure/  # EF Core, SQLite, JWT, BCrypt, seed, adapter ngoài
│   └── BookSpace.Api/             # HTTP API, auth, middleware, OpenAPI
├── tests/
│   ├── BookSpace.UnitTests/
│   └── BookSpace.IntegrationTests/
├── BookSpace.sln
└── Dockerfile
```

Dependency rule:

```text
Api -> Application + Infrastructure
Infrastructure -> Application + Domain
Application -> Domain
Domain -> không phụ thuộc framework
```

## Chạy nhanh

Yêu cầu: .NET SDK 10.

```powershell
cd T:\bookspace\backend
dotnet restore BookSpace.sln
dotnet run --project src\BookSpace.Api\BookSpace.Api.csproj
```

Địa chỉ:

- API: `http://localhost:5080`
- Health: `http://localhost:5080/health`
- OpenAPI JSON: `http://localhost:5080/openapi/v1.json`
- Frontend mặc định: `http://localhost:5173`

SQLite được tạo tại `src/BookSpace.Api/data/bookspace.db` khi chạy local và được nâng cấp bằng EF Core migrations. Không cần MySQL, PostgreSQL hay Docker.

## Tài khoản demo

Chỉ được seed trong môi trường `Development` khi `SeedData:Enabled=true`:

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | `admin@bookspace.local` | `Admin123!` |
| Thành viên | `reader@bookspace.local` | `Reader123!` |

Không bật seed và không dùng các mật khẩu này ở production.

## Cấu hình

ASP.NET Core không tự đọc file `.env`. `.env.example` là danh sách biến dành cho Docker Compose, IDE hoặc shell. Với PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:BOOKSPACE_Jwt__Secret = 'replace-with-a-random-secret-at-least-32-bytes'
$env:BOOKSPACE_Cors__AllowedOrigins__0 = 'http://localhost:5173'
dotnet run --project src\BookSpace.Api\BookSpace.Api.csproj
```

Production bắt buộc cung cấp `BOOKSPACE_Jwt__Secret` có ít nhất 32 byte. SQLite mặc định dùng:

```text
BOOKSPACE_ConnectionStrings__DefaultConnection=Data Source=data/bookspace.db
```

## Contract HTTP

Mọi response dùng JSON camelCase và enum dạng chuỗi:

```json
{
  "success": true,
  "message": "Thành công.",
  "data": {},
  "code": null,
  "timestamp": "2026-07-29T10:00:00Z"
}
```

Response phân trang:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

Role là `USER | ADMIN`; trạng thái kệ sách là `WANT_TO_READ | READING | READ`.

### Auth và người dùng

| Method | Endpoint | Auth |
|---|---|---|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/refresh` | Public |
| POST | `/api/auth/logout` | Public |
| GET | `/api/auth/me` | User |
| GET | `/api/users?search=&page=1&pageSize=20` | Public |
| GET | `/api/users/suggestions?page=1&pageSize=20` | User |
| GET | `/api/users/{id}` | Public |
| PATCH | `/api/users/me` | User |
| POST / DELETE | `/api/users/{id}/follow` | User |
| GET | `/api/users/{id}/followers` | Public |
| GET | `/api/users/{id}/following` | Public |

Auth login/register/refresh trả `{ accessToken, refreshToken, expiresAt, user }`. Refresh token được sinh ngẫu nhiên, chỉ lưu SHA-256 hash, được rotate khi làm mới và revoke khi logout.

### Catalog và đọc sách

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/books`, `/api/books/{id}` | Public |
| GET | `/api/authors`, `/api/categories` | Public |
| GET / POST | `/api/library` | User |
| PATCH / DELETE | `/api/library/{itemId}` | User |
| PATCH | `/api/library/{itemId}/progress` | User |
| GET / POST | `/api/reading-sessions` | User |
| GET / POST | `/api/reading-goals` | User |
| GET / PATCH / DELETE | `/api/reading-goals/{id}` | Owner |
| GET / POST | `/api/reading-notes` | User |
| GET / PATCH / DELETE | `/api/reading-notes/{id}` | Owner |
| GET | `/api/dashboard` | User |
| GET | `/api/insights/overview` | User |
| GET | `/api/insights/calendar` | User |
| GET | `/api/insights/weekly` | User |
| GET | `/api/insights/monthly` | User |

PATCH thư viện nhận đồng thời hoặc riêng lẻ `shelf`, `currentPage`, `progressPercent`. Khi đạt trang cuối, trạng thái tự chuyển sang `READ`.

Mục tiêu đọc là dữ liệu riêng của owner: metric `BOOKS | PAGES | MINUTES`, period `WEEK | MONTH | YEAR | CUSTOM` (giá trị enum khác bị từ chối), target từ 1 đến 1.000.000, `endDate` phải ở tương lai, sau `startDate` và thời lượng không quá 366 ngày. Hai mục tiêu còn hoạt động cùng metric không được chồng khoảng thời gian. `currentValue` được tính lại từ sách đã đọc hoặc phiên đọc trong khoảng ngày; list goal đồng bộ completion trước khi filter/phân trang, và khi đạt target hệ thống đánh dấu hoàn thành một lần, tạo notification `SYSTEM` dẫn tới `/goals`.

Ghi chú đọc cũng là dữ liệu riêng của owner. Ghi chú phải gắn với book tồn tại, có ít nhất quote hoặc content, page (nếu có) nằm trong 1..`pageCount` của book. Tag được trim, bỏ trùng không phân biệt hoa/thường, tối đa 10 tag, mỗi tag tối đa 30 ký tự và không chứa `|`. List hỗ trợ filter `bookId`, `tag`, `search` cùng phân trang.

Reading Insights là read model riêng tư được suy ra trực tiếp từ `ReadingSession`, `LibraryItem` và `ReadingGoal`; không có bảng aggregate riêng và không phụ thuộc Bookstore. Overview chấp nhận `days=30|90|365`; calendar nhận rolling `days=30|90|365` hoặc `year=1900..năm local hiện tại`; weekly nhận `weeks=4..52`; monthly nhận `months=6|12|24`. Cả bốn endpoint nhận `utcOffsetMinutes=-840..840`, mặc định `0`; giá trị `420` nghĩa là UTC+7. Ngày local dùng khoảng nửa mở `[startUtc, endUtc)`, phiên xuyên nửa đêm thuộc ngày bắt đầu, và calendar luôn điền đủ ngày không có hoạt động. Forecast sách lấy tối đa 30 ngày gần đây của đúng book rồi chia tổng trang cho số ngày lịch từ hoạt động đầu tiên trong cửa sổ đến hôm nay.

### Cộng đồng

| Method | Endpoint | Auth |
|---|---|---|
| GET / POST | `/api/reviews?bookId=...`, `/api/reviews` | Public / User |
| GET / POST | `/api/books/{bookId}/reviews` | Public / User |
| PUT / DELETE | `/api/reviews/{id}` | Owner hoặc Admin |
| POST / DELETE | `/api/reviews/{id}/like` | User |
| GET / POST | `/api/reviews/{id}/comments` | Public / User |
| DELETE | `/api/review-comments/{id}` | Owner hoặc Admin |
| GET | `/api/feed` | User |

### Câu lạc bộ và thử thách

| Method | Endpoint | Auth |
|---|---|---|
| GET / POST | `/api/clubs` | Public / User |
| GET / PATCH | `/api/clubs/{id}` | Public/member / Owner |
| POST / DELETE | `/api/clubs/{id}/join` | User |
| GET / PATCH / DELETE | `/api/clubs/{id}/members[...]` | Public/member / Owner/Moderator |
| GET / POST / DELETE | `/api/clubs/{id}/invitations[...]` | Owner/Moderator |
| GET / POST | `/api/clubs/invitations[...]` | Invitation recipient |
| PUT / DELETE | `/api/clubs/{id}/current-book` | Owner/Moderator |
| GET / POST | `/api/clubs/{id}/posts` | Public/member |
| GET / POST | `/api/clubs/posts/{postId}/comments` | Public/member |
| GET / POST | `/api/clubs/{id}/reading-sprints` | Public/member / Owner/Moderator |
| GET / PATCH | `/api/clubs/{id}/reading-sprints/{sprintId}` | Public/member / Owner/Moderator |
| POST / DELETE | `/api/clubs/{id}/reading-sprints/{sprintId}/join` | Club member / Participant |
| PUT | `/api/clubs/{id}/reading-sprints/{sprintId}/progress` | Active participant |
| GET | `/api/clubs/{id}/reading-sprints/{sprintId}/leaderboard`, `/timeline` | Public/member |
| POST / PATCH / DELETE | `/api/clubs/{id}/reading-sprints/{sprintId}/milestones[...]` | Owner/Moderator |
| GET / POST | `/api/clubs/{id}/reading-sprints/{sprintId}/milestones/{milestoneId}/responses` | Public/member / Active participant |
| DELETE | `/api/clubs/{id}/reading-sprints/{sprintId}/milestone-responses/{responseId}` | Author/Owner/Moderator |
| POST | `/api/clubs/{id}/reading-sprints/{sprintId}/reminders`, `/complete`, `/cancel` | Owner/Moderator |
| GET | `/api/challenges`, `/api/challenges/{id}` | Public |
| GET | `/api/challenges/my` | User |
| POST / DELETE | `/api/challenges/{id}/join` | User |

Tiến độ thử thách do server suy ra từ sách shelf `READ` có `FinishedAt` trong
cửa sổ UTC của challenge. Client không có endpoint ghi progress; giá trị đã
đồng bộ chỉ tăng, không vượt `goalBooks`, và completion notification là
idempotent.

`/api/challenges/my` là route canonical; `/api/challenges/mine` chỉ là alias
tương thích. Mutation thư viện/phiên đọc và đồng bộ challenge được commit trong
cùng transaction; join cũng commit participation, initial progress/completion và
notification trước khi trả DTO. Application sở hữu orchestration nghiệp vụ;
Infrastructure chỉ cung cấp transaction, atomic max và dedupe insert. Completion
event dùng deduplication key có unique index riêng.

Join, unpublish và delete dùng cùng serialized, non-deferred SQLite challenge
write boundary, lấy lock trước khi đọc điều kiện và giữ đến commit; command thắng
theo thứ tự commit nên database không thể có challenge draft/đã xóa kèm
participant. Leave remove, sync và map DTO trong một transaction; controller
không đọc lại sau commit. Publish `true` và update không thuộc boundary hẹp này.
Các query eligibility/precondition của các mutation này được materialize async
qua Application abstraction với cùng cancellation token sau khi boundary lấy
lock; query progress cũng dùng async executor và token của transaction tương ứng.
Nếu operation ném trước khi bắt đầu commit thì transaction rollback; application
không chạy DB work/follow-up read sau commit. Cancellation hoặc mất response trong
lúc/sau commit yêu cầu client đọc lại detail/`my` để đối soát.

Reading sprint dùng sách catalog nội bộ, metric `PAGES` hoặc `CHAPTERS` và status
suy ra `PLANNED`, `ACTIVE`, `ENDED` hoặc explicit `COMPLETED`, `CANCELLED`.
Owner/moderator chỉ sửa luật khi sprint còn `PLANNED`; participant có thể
leave/rejoin cùng identity, còn progress tuyệt đối chỉ tăng. Leaderboard dùng
tie-break ổn định; timeline chỉ tạo item khi progress thực sự tăng. Milestone là
soft-delete content và response là thread nhiều item, chỉ author hoặc manager
được xóa. Reminder tối đa một lần mỗi ngày UTC và không tạo notification trùng.
Toàn bộ luồng chạy khi Bookstore integration tắt.

### Thông báo và quản trị

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/notifications` | User |
| GET | `/api/notifications/unread-count` | User |
| PATCH | `/api/notifications/{id}/read` | User |
| PATCH | `/api/notifications/read-all` | User |
| POST / PATCH / DELETE | `/api/admin/books` | Admin |
| POST / PATCH / DELETE | `/api/admin/authors` | Admin |
| POST / PATCH / DELETE | `/api/admin/categories` | Admin |
| POST / PATCH / DELETE | `/api/admin/challenges` | Admin |
| PATCH | `/api/admin/challenges/{id}/publish` | Admin |

## Kết nối Bookstore tùy chọn

`IExternalBookProvider` nằm sau abstraction của Application. Adapter HTTP chỉ được sử dụng khi:

```text
BOOKSPACE_BookstoreIntegration__Enabled=true
```

Tìm kiếm qua `GET /api/external-books/search?query=...`. Khi integration tắt, thiếu cấu hình hoặc Bookstore không phản hồi, endpoint trả `available=false` và danh sách rỗng; catalog, thư viện và mọi nghiệp vụ BookSpace vẫn chạy bình thường. Không có shared database.

## Test và kiểm tra

```powershell
cd T:\bookspace\backend
dotnet format BookSpace.sln --verify-no-changes
dotnet build BookSpace.sln
dotnet test BookSpace.sln --no-build
```

Integration tests khởi động API thật bằng `WebApplicationFactory`, dùng SQLite file tạm và kiểm tra health, OpenAPI, auth, RBAC, people discovery, follow graph, catalog, thư viện, phiên đọc, mục tiêu đọc, ghi chú riêng tư, dashboard, Reading Insights, club reading sprint và admin CRUD.

Tạo migration mới sau khi thay đổi persistence model:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef migrations add TenMigration `
  --project src\BookSpace.Infrastructure `
  --startup-project src\BookSpace.Api `
  --output-dir Persistence\Migrations
```

Ứng dụng tự chạy `Database.MigrateAsync()` khi khởi động.

## Docker

```powershell
cd T:\bookspace\backend
docker build -t bookspace-api .
docker run --rm -p 5080:5080 `
  -e BOOKSPACE_Jwt__Secret='replace-with-a-random-secret-at-least-32-bytes' `
  -e BOOKSPACE_Cors__AllowedOrigins__0='http://localhost:5173' `
  -v bookspace-data:/app/data `
  bookspace-api
```

Volume `/app/data` giữ file `/app/data/bookspace.db`.
