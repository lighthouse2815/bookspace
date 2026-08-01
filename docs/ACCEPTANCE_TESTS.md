# BookSpace — Kế hoạch nghiệm thu sản phẩm

> Mục tiêu: chứng minh sản phẩm chạy end-to-end bằng API thật, dữ liệu thật và quyền thật.<br>
> P0: bắt buộc để hoàn thành Goal 1. P1: bắt buộc trước khi phát hành public.

## 1. Môi trường nghiệm thu

### 1.1 Công cụ

- .NET SDK 10.
- Node.js tương thích Vite 8.
- npm theo lockfile.
- Docker Desktop cho kiểm tra container.
- PowerShell 7 hoặc Windows PowerShell chạy script local.

### 1.2 Seed Development

| Email | Password | Role |
|---|---|---|
| `admin@bookspace.local` | `Admin123!` | `ADMIN` |
| `reader@bookspace.local` | `Reader123!` | `USER` |

Test tạo thêm user bằng API:

```json
{
  "displayName": "Bạn Đọc",
  "email": "friend.acceptance@bookspace.local",
  "password": "Friend123!"
}
```

Seed content tối thiểu:

- 6 author.
- 5 category.
- 12 book, mỗi book có author, page count, cover và category.
- 3 library entry của reader ở đủ `WANT_TO_READ`, `READING`, `READ`.
- 2 reading session của reader.
- 2 review, trong đó có một review của reader.
- 1 public club có owner, member và post.
- 1 published active challenge.
- 1 draft challenge.
- 2 notification, gồm một đã đọc và một chưa đọc.

Seed chỉ tồn tại trong Development. Production startup không tạo các tài khoản trên.

### 1.3 Nguyên tắc chạy test

- Bắt đầu từ database mới cho test repeatable.
- ID được lấy từ response, không hard-code UUID seed.
- Mỗi test mutation tự tạo hoặc dọn dữ liệu riêng.
- Không dùng UI mock/localStorage thay API.
- API assertion kiểm tra status, `success`, `message`, `data`, `code`, `timestamp`.
- Test authorization gọi trực tiếp API, không chỉ kiểm tra nút bị ẩn.

## 2. Build, startup và cấu trúc

| ID | P | Given/When/Then |
|---|---|---|
| AC-BLD-001 | P0 | Khi chạy `dotnet restore T:\bookspace\backend\BookSpace.slnx`, lệnh exit 0. |
| AC-BLD-002 | P0 | Sau restore, khi chạy `dotnet build T:\bookspace\backend\BookSpace.slnx --no-restore`, build exit 0 và không có compile error. |
| AC-BLD-003 | P0 | Sau build, khi chạy `dotnet test T:\bookspace\backend\BookSpace.slnx --no-build`, toàn bộ unit/integration test pass. |
| AC-BLD-004 | P0 | Trong frontend, `npm ci` dùng đúng lockfile và exit 0. |
| AC-BLD-005 | P0 | `npm run typecheck` exit 0; type API dùng `USER|ADMIN` và `shelf`. |
| AC-BLD-006 | P0 | `npm run lint` exit 0. |
| AC-BLD-007 | P0 | `npm run build` tạo production bundle và exit 0. |
| AC-BLD-008 | P0 | `docker compose config` exit 0; service `api`, `web`, volume `bookspace-data` hợp lệ. |
| AC-BLD-009 | P0 | `docker compose up --build` làm API healthy ở `http://localhost:5080/health` và web render ở `http://localhost:5173`. |
| AC-BLD-010 | P0 | Database trống được tạo/migrate/seed mà không cần thao tác SQL thủ công. |
| AC-BLD-011 | P0 | Restart container giữ dữ liệu nhờ volume, không seed trùng user/catalog. |
| AC-BLD-012 | P0 | Không còn WeatherForecast controller/page, `Class1.cs`, `UnitTest1.cs` hoặc scaffold demo được expose/build như chức năng sản phẩm. |
| AC-BLD-013 | P0 | `BookSpace.Domain` không reference ASP.NET Core, EF Core, JWT hoặc SQLite. |
| AC-BLD-014 | P0 | Controller không truy cập DbContext trực tiếp; page React không gọi Axios trực tiếp. |
| AC-BLD-015 | P0 | Repository không chứa secret production, database file runtime hoặc refresh token. |

## 3. API envelope, validation và pagination

| ID | P | Given/When/Then |
|---|---|---|
| AC-API-001 | P0 | Với endpoint thành công, response có đủ `success=true`, `message`, `data`, `code=null`, `timestamp` UTC. |
| AC-API-002 | P0 | Với endpoint lỗi, response có `success=false`, `data=null` hoặc validation details, `code` ổn định và timestamp UTC. |
| AC-API-003 | P0 | Validation nhiều field trả 400 `VALIDATION_ERROR` và map lỗi theo camelCase field. |
| AC-API-004 | P0 | Danh sách không có item trả `items=[]`, `totalItems=0`, `totalPages=0`. |
| AC-API-005 | P0 | `page=1&pageSize=20` trả metadata đúng trong body. |
| AC-API-006 | P0 | `page=0` được normalize về 1 và response trả `page=1`. |
| AC-API-007 | P0 | `pageSize=101` được clamp về 100 và response trả `pageSize=100`. |
| AC-API-008 | P0 | UUID sai format trả 400, không trả stack trace. |
| AC-API-009 | P0 | Route không tồn tại trả envelope 404. |
| AC-API-010 | P0 | Exception không dự kiến trả 500 `INTERNAL_ERROR`, không lộ SQL, path máy, stack trace hoặc secret. |

## 4. Authentication và user

| ID | P | Given/When/Then |
|---|---|---|
| AC-AUTH-001 | P0 | Khi register display name/email/password hợp lệ, API trả 201 `AuthSessionResponse`, role `USER`, access token và refresh token dùng được. |
| AC-AUTH-002 | P0 | Register email đã tồn tại không phân biệt hoa thường trả 409 `EMAIL_ALREADY_EXISTS`. |
| AC-AUTH-003 | P0 | Register email sai trả 400 `VALIDATION_ERROR`. |
| AC-AUTH-004 | P0 | Register password dưới 8 ký tự trả 400. |
| AC-AUTH-005 | P0 | Client gửi `role=ADMIN` trong register nhưng user tạo ra vẫn là `USER`. |
| AC-AUTH-006 | P0 | Login reader đúng trả 200, principal `sub` đúng BookSpace user ID và role `USER`. |
| AC-AUTH-007 | P0 | Login admin đúng trả role `ADMIN`. |
| AC-AUTH-008 | P0 | Login email không tồn tại và mật khẩu sai cùng trả 401 `INVALID_CREDENTIALS`, không phân biệt nguyên nhân. |
| AC-AUTH-009 | P0 | `GET /api/auth/me` với access token hợp lệ trả đúng user. |
| AC-AUTH-010 | P0 | `GET /api/auth/me` không token trả 401 envelope. |
| AC-AUTH-011 | P0 | Refresh hợp lệ trả cặp token mới và thu hồi token cũ. |
| AC-AUTH-012 | P0 | Dùng lại refresh token cũ sau rotation trả 401 `INVALID_REFRESH_TOKEN`. |
| AC-AUTH-013 | P0 | Refresh token hết hạn trả 401 `INVALID_REFRESH_TOKEN`. |
| AC-AUTH-014 | P0 | Logout thu hồi refresh token; logout lặp lại không tạo lỗi 500. |
| AC-AUTH-015 | P0 | Password thô và refresh token thô không xuất hiện trong database/log. |
| AC-AUTH-016 | P0 | `PATCH /api/users/me` cập nhật display name, bio, avatar URL và không cho đổi role/email bằng body thừa. |
| AC-AUTH-017 | P0 | Profile có avatar URL sai trả 400. |
| AC-AUTH-018 | P0 | `GET /api/users/{id}` không lộ password hash, refresh token hoặc email private. |
| AC-AUTH-019 | P0 | User bị lock/soft delete không login được và token cũ không truy cập core API. |

## 5. Follow và hồ sơ công khai

| ID | P | Given/When/Then |
|---|---|---|
| AC-FOL-001 | P0 | Reader follow friend qua `/api/users/{id}/follow`, response `isFollowing=true`. |
| AC-FOL-002 | P0 | Follow tạo đúng một row và một notification `FOLLOW` cho friend. |
| AC-FOL-003 | P0 | Follow lại cùng user trả 409 `ALREADY_FOLLOWING`, không nhân đôi counter. |
| AC-FOL-004 | P0 | Tự follow trả 400 `CANNOT_FOLLOW_SELF`. |
| AC-FOL-005 | P0 | Unfollow trả `isFollowing=false` và gọi lại vẫn idempotent. |
| AC-FOL-006 | P0 | Followers/following trả PageResult và chỉ chứa quan hệ hiện hành. |
| AC-FOL-007 | P0 | Follower/following/books-read counters trên profile khớp database sau mutation. |
| AC-FOL-008 | P0 | User không tồn tại trả 404 `USER_NOT_FOUND`. |
| AC-FOL-009 | P0 | Public directory/search chỉ trả `UserDiscoveryItem`, không có email/password/token/library detail. |
| AC-FOL-010 | P0 | Search trim input, case-insensitive cho ASCII, accent-sensitive, chỉ tìm `DisplayName`; email không match. |
| AC-FOL-011 | P0 | Search khác rỗng ngoài 2-100 ký tự trả 400 `INVALID_USER_SEARCH` với message tiếng Việt. |
| AC-FOL-012 | P0 | Directory và suggestions loại user locked/soft-deleted; request có principal còn loại chính mình. |
| AC-FOL-013 | P0 | Search authenticated trả đúng `isFollowing`, `followsYou`, `mutualFollowCount`; guest nhận false/false/0. |
| AC-FOL-014 | P0 | Suggestions loại user đã follow, xếp mutual/follower/books-read/name/id deterministic và vẫn có fallback mutual=0. |
| AC-FOL-015 | P0 | Directory và suggestions phân trang ở database, page liên tiếp không trùng hoặc bỏ item do tie-break. |
| AC-FOL-016 | P0 | Follow một suggestion làm item biến mất, search trả `isFollowing=true` và activity công khai của target vào feed. |
| AC-FOL-017 | P0 | Hai request follow cùng target đồng thời trả đúng một success và một 409 `ALREADY_FOLLOWING`; chỉ có một relation và một notification. |
| AC-FOL-018 | P1 | `followerCount` trên discovery khớp public profile kể cả khi relation đến từ tài khoản locked; locked user vẫn không xuất hiện làm candidate. |
| AC-FOL-019 | P1 | Development seed bỏ qua Hà Linh đã soft-delete, không insert trùng hoặc tự restore; thêm sách đứng đầu alphabet rồi seed lại không làm tăng `booksReadCount`. |
| AC-FOL-020 | P1 | Search rỗng khóa `DIRECTORY`, search theo tên khóa `SEARCH_MATCH`; suggestions chỉ dùng 5 reason code còn lại. |
| AC-FOL-021 | P0 | Tài khoản mới có kệ sách/activity profile riêng tư; guest nhận 403 `PROFILE_SECTION_PRIVATE`, owner vẫn nhận 200. |
| AC-FOL-022 | P0 | `PATCH /api/users/me/privacy` bật/tắt độc lập hai phần và response profile phản ánh đúng state server. |
| AC-FOL-023 | P0 | Public library phân trang/lọc shelf, không trả `currentPage`, session note, reading note hoặc email. |
| AC-FOL-024 | P0 | Reviews theo user luôn công khai, phân trang ổn định và không làm lộ dữ liệu đọc riêng tư. |
| AC-FOL-025 | P0 | Public activity chỉ chứa event của target, không lộ private club post cho guest và không chứa note riêng tư. |
| AC-FOL-026 | P0 | Public profile trả `followsYou`, `mutualFollowCount`, privacy và không discover/get user locked hoặc soft-deleted. |

## 6. Catalog công khai

| ID | P | Given/When/Then |
|---|---|---|
| AC-CAT-001 | P0 | `GET /api/books` trả PageResult với `coverImageUrl`, `publishedYear`, author, categories, rating và review count đúng tên field. |
| AC-CAT-002 | P0 | Search theo một phần title trả đúng book. |
| AC-CAT-003 | P0 | Search theo ISBN trả đúng book. |
| AC-CAT-004 | P0 | Lọc `authorId` chỉ trả sách có author đó. |
| AC-CAT-005 | P0 | Lọc `categoryId` chỉ trả sách thuộc category đó. |
| AC-CAT-006 | P0 | `sort=popular` sắp xếp theo tín hiệu popularity đã định nghĩa, thứ tự tie ổn định. |
| AC-CAT-007 | P0 | `sort=newest` sắp theo published year/created time, thứ tự tie ổn định. |
| AC-CAT-008 | P0 | `GET /api/books/{id}` trả chi tiết đúng; book soft delete trả 404. |
| AC-CAT-009 | P0 | Request detail có token trả `shelf` đúng; không token hoặc chưa thêm trả `shelf=null`. |
| AC-CAT-010 | P0 | `GET /api/authors` trả PageResult, có author seed và book count đúng. |
| AC-CAT-011 | P0 | `GET /api/categories` trả PageResult, có category seed và book count đúng. |
| AC-CAT-012 | P0 | Catalog công khai hoạt động khi integration Bookstore tắt. |

## 7. Admin catalog

| ID | P | Given/When/Then |
|---|---|---|
| AC-ADM-001 | P0 | USER gọi `POST /api/admin/books` nhận 403. |
| AC-ADM-002 | P0 | ADMIN tạo book hợp lệ nhận 201 và book xuất hiện trong public catalog. |
| AC-ADM-003 | P0 | Tạo book với author/category không tồn tại trả 404 và không tạo row dở dang. |
| AC-ADM-004 | P0 | Tạo ISBN trùng trả 409 `ISBN_ALREADY_EXISTS`. |
| AC-ADM-005 | P0 | ADMIN patch title/cover/page count thành công và public detail phản ánh dữ liệu mới. |
| AC-ADM-006 | P0 | ADMIN soft-delete book; public detail/list không còn book, dữ liệu lịch sử không bị hard-delete. |
| AC-ADM-007 | P0 | ADMIN tạo author hợp lệ và author xuất hiện trong list. |
| AC-ADM-008 | P0 | ADMIN patch author name/bio/avatar thành công. |
| AC-ADM-009 | P0 | ADMIN chỉ soft-delete author chưa được gắn với book; author đang dùng trả 409 `AUTHOR_IN_USE`. |
| AC-ADM-010 | P0 | ADMIN tạo category hợp lệ; tên trùng không phân biệt hoa thường trả 409. |
| AC-ADM-011 | P0 | ADMIN patch category name/description thành công. |
| AC-ADM-012 | P0 | ADMIN chỉ soft-delete category chưa được gắn với book; category đang dùng trả 409 `CATEGORY_IN_USE`. |

## 8. Library và state transition

| ID | P | Given/When/Then |
|---|---|---|
| AC-LIB-001 | P0 | Reader thêm book chưa có vào shelf `WANT_TO_READ`, API trả 201 và `currentPage=0`. |
| AC-LIB-002 | P0 | Thêm lại cùng book trả 409 `BOOK_ALREADY_IN_LIBRARY`. |
| AC-LIB-003 | P0 | Lọc library theo từng shelf chỉ trả item đúng shelf. |
| AC-LIB-004 | P0 | Đổi `WANT_TO_READ -> READING` đặt `startedAt`, giữ `finishedAt=null`. |
| AC-LIB-005 | P0 | Cập nhật `currentPage>0` cho WANT_TO_READ tự chuyển `READING`. |
| AC-LIB-006 | P0 | `currentPage=pageCount` tự chuyển `READ`, progress 100 và đặt `finishedAt`. |
| AC-LIB-007 | P0 | Current page âm trả 400 `INVALID_READING_PROGRESS`. |
| AC-LIB-008 | P0 | Current page lớn hơn page count trả 400 `INVALID_READING_PROGRESS`. |
| AC-LIB-009 | P0 | Current page nhỏ hơn tiến độ hiện tại trả 409 `READING_PROGRESS_CANNOT_DECREASE`. |
| AC-LIB-010 | P0 | Reader không thể patch/delete library item của friend; trả 404 để không lộ ownership. |
| AC-LIB-011 | P0 | Delete library item làm item biến mất khỏi list nhưng reading session lịch sử còn nguyên. |
| AC-LIB-012 | P0 | Response dùng `shelf`, không dùng `status`; query lọc dùng `shelf`. |

## 9. Reading journal

| ID | P | Given/When/Then |
|---|---|---|
| AC-READ-001 | P0 | Reader tạo session với started/end/duration/pages hợp lệ, nhận 201. |
| AC-READ-002 | P0 | Session response dùng `startedAt`, `endedAt`, `durationMinutes`, `pagesRead`. |
| AC-READ-003 | P0 | Book chưa ở library được tự thêm `READING` khi ghi session. |
| AC-READ-004 | P0 | Session tăng current page nhưng không vượt page count. |
| AC-READ-005 | P0 | Session làm đạt page count chuyển library item sang `READ`. |
| AC-READ-006 | P0 | Started time ở tương lai quá 5 phút trả 400. |
| AC-READ-007 | P0 | Ended time trước started time trả 400. |
| AC-READ-008 | P0 | Duration không khớp started/ended quá một phút trả 400. |
| AC-READ-009 | P0 | Minutes hoặc pages bằng 0 trả 400. |
| AC-READ-010 | P0 | `GET /api/reading-sessions` chỉ trả session của principal và có phân trang. |
| AC-READ-011 | P0 | UI `/journal` tạo session qua API, invalidate library/dashboard và render item mới không reload trang. |

## 10. Reading goals và ghi chú riêng tư

### 10.1 Reading goals

| ID | P | Given/When/Then |
|---|---|---|
| AC-GOAL-001 | P0 | Reader tạo goal hợp lệ với metric/period hợp lệ, target 1..1.000.000, end ở tương lai; nhận 201 với `currentValue`, `progressPercent`, `status`, `completedAt` do server trả. |
| AC-GOAL-002 | P0 | `targetValue=0` hoặc >1.000.000 trả 400 `INVALID_READING_GOAL_TARGET`; `endDate <= startDate`, end quá khứ hoặc kỳ >366 ngày trả 400 `INVALID_READING_GOAL_DATE`. |
| AC-GOAL-003 | P0 | Metric/period không ánh xạ tới enum hợp lệ (ví dụ numeric `999`) trả lần lượt 400 `INVALID_READING_GOAL_METRIC` và `INVALID_READING_GOAL_PERIOD`. |
| AC-GOAL-004 | P0 | Cùng owner, cùng metric, hai khoảng active giao nhau trả 409 `READING_GOAL_OVERLAPS`; metric khác hoặc hai khoảng chỉ chạm đầu/cuối được tạo. |
| AC-GOAL-005 | P0 | `BOOKS` chỉ đếm library item `READ` có `finishedAt` trong kỳ; `PAGES`/`MINUTES` cộng reading session có `startedAt` trong kỳ; client không có field/endpoint ghi progress trực tiếp. |
| AC-GOAL-006 | P0 | Sau session làm đạt goal, `GET /api/reading-goals?status=COMPLETED` trả goal ngay cả khi chưa từng GET detail; đồng bộ completion chạy trước status filter và pagination. |
| AC-GOAL-007 | P0 | Lần đầu đạt target đặt `completedAt`, progress 100 và tạo đúng một notification `SYSTEM` link `/goals`; đọc/list lại không tạo notification thứ hai. |
| AC-GOAL-008 | P0 | PATCH gửi đủ writable fields cho goal active thành công; goal completed trả 400 `READING_GOAL_ALREADY_COMPLETED`, goal expired trả 400 `READING_GOAL_ALREADY_EXPIRED`. |
| AC-GOAL-009 | P0 | User khác GET/PATCH/DELETE goal trả 404 `READING_GOAL_NOT_FOUND`; owner delete soft-delete và GET sau đó trả 404. |

### 10.2 Reading notes

| ID | P | Given/When/Then |
|---|---|---|
| AC-NOTE-001 | P0 | Reader POST note cho book tồn tại (không cần có trong library) nhận 201, `bookId`, book projection, page, quote/content và tag được trả đúng. |
| AC-NOTE-002 | P0 | Quote và content cùng rỗng/blank trả 400 `READING_NOTE_CONTENT_REQUIRED`; quote >500 hoặc content >5.000 bị validation ở API. |
| AC-NOTE-003 | P0 | `pageNumber` nếu có phải 1..`pageCount`; 0 hoặc vượt book trả 400 `INVALID_NOTE_PAGE_NUMBER`; book không tồn tại trả `BOOK_NOT_FOUND`. |
| AC-NOTE-004 | P0 | Tag được trim, deduplicate không phân biệt hoa/thường; quá 10 tag, tag >30 ký tự, tổng >500 hoặc chứa `|` trả 400 `INVALID_READING_NOTE_TAG`. |
| AC-NOTE-005 | P0 | GET notes filter đúng owner theo `bookId`, tag đầy đủ không phân biệt hoa/thường và search quote/content/tags; search >200 ký tự trả `INVALID_READING_NOTE_SEARCH`; sort `updatedAt ?? createdAt desc`. |
| AC-NOTE-006 | P0 | PATCH chỉ gửi `pageNumber`, `quote`, `content`, `tags`; book của note không đổi. Frontend edit không được gửi `bookId` trong request PATCH. |
| AC-NOTE-007 | P0 | User khác GET/PATCH/DELETE note trả 404 `READING_NOTE_NOT_FOUND`; owner delete soft-delete và note không còn trong list/detail. |

## 11. Reading Insights

| ID | P | Given/When/Then |
|---|---|---|
| AC-INSIGHT-001 | P0 | Khách không có access token gọi endpoint `/api/insights/*` nhận 401; response của user đã đăng nhập chỉ suy ra từ dữ liệu principal, không nhận `userId` từ query. |
| AC-INSIGHT-002 | P0 | Overview nhận đúng `days=30|90|365`, trả totals, averages, goal summary, comparison, book forecasts và goal forecasts trong `ApiResponse`. |
| AC-INSIGHT-003 | P0 | Với `utcOffsetMinutes=420`, session `17:30Z` thuộc ngày local kế tiếp; offset biên -840/840 được nhận, -841/841 trả 400 `INVALID_UTC_OFFSET`. |
| AC-INSIGHT-004 | P0 | Rolling calendar 365 ngày trả đúng 365 `daysData` từ hôm nay-364 đến hôm nay, tăng dần, điền ngày rỗng và loại ngày hôm nay-365. |
| AC-INSIGHT-005 | P0 | Calendar theo `year` override `days`, trả 365 hoặc 366 ngày đúng năm; year <1900 hoặc lớn hơn năm local hiện tại trả `INVALID_INSIGHTS_YEAR`. |
| AC-INSIGHT-006 | P0 | Nhiều session cùng ngày chỉ tăng streak một ngày; current streak bắt đầu hôm nay hoặc hôm qua, gap làm streak 0; longest streak xét toàn lịch sử. |
| AC-INSIGHT-007 | P0 | Session xuyên nửa đêm được cộng toàn bộ vào ngày local của `startedAt`, không bị chia hoặc đếm hai lần. |
| AC-INSIGHT-008 | P0 | Comparison trả hai cửa sổ liền kề cùng độ dài; mỗi metric có current/previous/changePercent. Previous 0 và current dương trả percent null; cả hai 0 trả 0. |
| AC-INSIGHT-009 | P0 | Book forecast chỉ chứa library item `READING`; tốc độ bằng trang của đúng book trong tối đa 30 ngày chia số ngày lịch từ hoạt động đầu tiên đến hôm nay; không có tốc độ thì ETA null. |
| AC-INSIGHT-010 | P0 | Goal forecast chỉ chứa goal `ACTIVE`, dùng đúng nguồn progress `BOOKS/PAGES/MINUTES`; ETA và `isOnTrack` null khi không có tốc độ. |
| AC-INSIGHT-011 | P0 | Gọi overview sau hoạt động làm đạt goal đồng bộ goal sang `COMPLETED`, loại khỏi goal forecasts và tạo đúng một notification dù gọi overview nhiều lần. |
| AC-INSIGHT-012 | P0 | Weekly nhận 4..52, tuần bắt đầu thứ Hai, trả đủ số item kể cả tuần rỗng; ngoài khoảng trả `INVALID_INSIGHTS_WEEKS`. |
| AC-INSIGHT-013 | P0 | Monthly nhận 6, 12 hoặc 24, gồm tháng hiện tại và điền tháng rỗng; giá trị khác trả `INVALID_INSIGHTS_MONTHS`. |
| AC-INSIGHT-014 | P0 | Overview/calendar `days` ngoài 30, 90, 365 trả 400 `INVALID_INSIGHTS_RANGE`; mọi validation dùng message tiếng Việt và envelope chuẩn. |
| AC-INSIGHT-015 | P0 | User mới không có dữ liệu nhận totals/averages/streak bằng 0, calendar/report vẫn đủ bucket và các danh sách forecast rỗng. |

## 12. Review, like và comment

| ID | P | Given/When/Then |
|---|---|---|
| AC-REV-001 | P0 | Public GET reviews yêu cầu `bookId` và trả PageResult. |
| AC-REV-002 | P0 | Reader POST `/api/reviews` với bookId/rating/content nhận 201. |
| AC-REV-003 | P0 | Cùng user review cùng book lần hai trả 409 `REVIEW_ALREADY_EXISTS`. |
| AC-REV-004 | P0 | Rating 0 hoặc 6 trả 400. |
| AC-REV-005 | P0 | Review spoiler trả `containsSpoilers=true`; UI che nội dung cho tới khi người xem mở. |
| AC-REV-006 | P0 | Owner patch review thành công; user khác nhận 403. |
| AC-REV-007 | P0 | Review tạo/sửa/xóa làm `averageRating` và `reviewCount` của book cập nhật đúng. |
| AC-REV-008 | P0 | Like lần đầu tăng `likeCount`, đặt `likedByCurrentUser=true` và tạo notification `REVIEW_LIKE` cho author khác principal. |
| AC-REV-009 | P0 | Like lần hai không tạo row/counter/notification thứ hai. |
| AC-REV-010 | P0 | Unlike giảm count một lần; unlike lặp lại idempotent. |
| AC-REV-011 | P0 | Add comment hợp lệ trả 201, tăng `commentCount` và tạo notification `COMMENT` cho review author khác principal. |
| AC-REV-012 | P0 | GET comments trả PageResult theo review. |
| AC-REV-013 | P0 | Comment owner hoặc ADMIN xóa được; user khác nhận 403. |
| AC-REV-014 | P0 | Review owner hoặc ADMIN soft-delete được; review/comment/like không còn hiển thị công khai. |
| AC-REV-015 | P0 | Response dùng `likeCount`, `commentCount`, `likedByCurrentUser`, không dùng tên field cũ. |

## 13. Feed

| ID | P | Given/When/Then |
|---|---|---|
| AC-FEED-001 | P0 | Reader follow friend rồi friend tạo review; review xuất hiện trong reader feed. |
| AC-FEED-002 | P0 | Hoạt động của user không follow không xuất hiện. |
| AC-FEED-003 | P0 | Feed có type `REVIEW`, `READING_PROGRESS`, `CHALLENGE` hoặc `CLUB_POST` đúng payload liên quan. |
| AC-FEED-004 | P0 | Feed phân trang và sắp `createdAt desc` với tie-break ổn định. |
| AC-FEED-005 | P0 | Private club activity không lộ cho người ngoài. |

## 14. Clubs

| ID | P | Given/When/Then |
|---|---|---|
| AC-CLUB-001 | P0 | Public GET clubs trả PageResult và không liệt kê private club cho người ngoài. |
| AC-CLUB-002 | P0 | Authenticated user tạo public club nhận 201 và trở thành owner/member. |
| AC-CLUB-003 | P0 | Tạo club thiếu name hoặc description vượt giới hạn trả 400. |
| AC-CLUB-004 | P0 | User join public club, `isJoined=true`, member count tăng đúng một. |
| AC-CLUB-005 | P0 | Join lại trả 409 `ALREADY_CLUB_MEMBER`. |
| AC-CLUB-006 | P0 | Người ngoài GET private club nhận 404; gọi join private club đã biết ID nhận 403 `PRIVATE_CLUB`. |
| AC-CLUB-007 | P0 | Member leave public club, `isJoined=false`, count giảm một. |
| AC-CLUB-008 | P0 | Owner leave club trả 409 `OWNER_CANNOT_LEAVE`. |
| AC-CLUB-009 | P0 | Member tạo post với content hợp lệ nhận 201; người ngoài nhận 403. |
| AC-CLUB-010 | P0 | GET posts trả PageResult và đúng quyền public/private. |
| AC-CLUB-011 | P0 | Member comment post nhận 201, comment count tăng và notification `CLUB` được tạo cho post author khác principal. |
| AC-CLUB-012 | P0 | Author, owner, moderator hoặc ADMIN xóa post/comment theo quyền; user ngoài nhận 403. |
| AC-CLUB-013 | P0 | UI `/clubs/:id` join/leave/create post qua API và cập nhật state không reload toàn trang. |
| AC-CLUB-014 | P0 | Owner cập nhật tên, mô tả, ảnh bìa và public/private; moderator/member nhận 403 `CLUB_OWNER_REQUIRED`. |
| AC-CLUB-015 | P0 | Owner/moderator mời đúng email tài khoản BookSpace; không có endpoint tìm kiếm email công khai. |
| AC-CLUB-016 | P0 | Gửi lại cùng lời mời đang pending trả cùng invitation, không tạo invitation hoặc notification trùng. |
| AC-CLUB-017 | P0 | Inbox lời mời chỉ trả dữ liệu của principal; user khác accept/decline nhận 403 `CLUB_INVITATION_FORBIDDEN`. |
| AC-CLUB-018 | P0 | Recipient accept tạo đúng một membership; gọi accept lại idempotent và private club bắt đầu hiển thị cho member. |
| AC-CLUB-019 | P0 | Recipient decline và manager revoke đổi đúng trạng thái; gọi lặp trạng thái idempotent, lời mời hết hạn không thể accept. |
| AC-CLUB-020 | P0 | Owner chỉ gán `MEMBER`/`MODERATOR`; không thể gán, đổi hoặc loại `OWNER`, enum role lạ nhận 400. |
| AC-CLUB-021 | P0 | Moderator được mời, chọn sách chung và loại member thường; không được sửa club, đổi role hoặc loại owner/moderator. |
| AC-CLUB-022 | P0 | Owner/moderator đặt và gỡ sách đọc chung từ catalog nội bộ; thao tác lặp không tạo notification trùng. |
| AC-CLUB-023 | P0 | UI có tạo/sửa club, inbox accept/decline, roster, invite/revoke, role/remove và chọn/gỡ sách chung; state cập nhật không reload trang. |

### 14.1 Đợt đọc chung

| ID | P | Given/When/Then |
|---|---|---|
| AC-SPRINT-001 | P0 | Owner/moderator tạo sprint bằng sách catalog nội bộ, khoảng thời gian hợp lệ, metric `PAGES` hoặc `CHAPTERS`; member thường của public club nhận 403, người ngoài private club nhận 404. |
| AC-SPRINT-002 | P0 | Manager chỉ cập nhật sprint có derived status `PLANNED` và trước `startsAt`; sprint `ACTIVE`/đã kết thúc trả 409, còn enum, target hoặc khoảng thời gian sai nhận validation envelope 400. |
| AC-SPRINT-003 | P0 | Người ngoài không thể khám phá hoặc đọc sprint của private club dù biết UUID; member của private club xem được. |
| AC-SPRINT-004 | P0 | Member join, gọi join lại, leave và rejoin không tạo participant thứ hai; club leave/kick đặt `LeftAt` trên participant nonterminal và loại user khỏi leaderboard nhưng giữ timeline. |
| AC-SPRINT-005 | P0 | Người không phải club member không thể join; club member chưa join không thể ghi progress, phản hồi milestone hoặc xuất hiện trên leaderboard. |
| AC-SPRINT-006 | P0 | Progress là giá trị tuyệt đối, không giảm, không âm, không vượt target; gửi lại cùng giá trị là idempotent và không tạo timeline/notification trùng. |
| AC-SPRINT-007 | P0 | Cả sprint `PAGES` và `CHAPTERS` áp dụng cùng invariant tiến độ và tính phần trăm bị chặn trong 0..100. |
| AC-SPRINT-008 | P0 | Leaderboard chỉ có participant active, xếp progress giảm dần và dùng tie-break ổn định khi bằng điểm; percent khớp progress/target. |
| AC-SPRINT-009 | P0 | Timeline chỉ có activity thuộc sprint, đúng actor/thời gian và chỉ người có quyền xem club được truy cập; private activity không rò rỉ. |
| AC-SPRINT-010 | P0 | Manager tạo/sửa/soft-delete milestone khi sprint `PLANNED` hoặc `ACTIVE`; member thường không mutation được milestone, sprint đã kết thúc từ chối mutation và milestone bị xóa không còn trong danh sách active. |
| AC-SPRINT-011 | P0 | Participant active có thể POST nhiều response dạng thread, không có PATCH response; author hoặc manager soft-delete được, user khác không xóa được và DTO `canDelete` phản ánh đúng principal. |
| AC-SPRINT-012 | P0 | Manager gửi reminder hai lần trong cùng ngày UTC chỉ tạo tối đa một notification cho mỗi recipient; ngày UTC khác có thể tạo đợt nhắc mới. |
| AC-SPRINT-013 | P0 | Complete và cancel có tính idempotent; sprint terminal từ chối update, join/leave, progress, milestone, response và reminder mutation. |
| AC-SPRINT-014 | P0 | Danh sách/history lọc đúng status, sắp xếp ổn định và không trả sprint ngoài quyền xem của principal. |
| AC-SPRINT-015 | P0 | Khi Bookstore integration tắt hoặc không khả dụng, create/list/detail/join/progress/leaderboard/timeline/milestone/reminder/complete/cancel vẫn dùng catalog và database BookSpace, không phát sinh outbound dependency. |
| AC-SPRINT-016 | P0 | UI club detail có list/history, form manager, join/leave, progress, leaderboard, timeline, milestone/response và reminder theo quyền; mutation cập nhật state không reload toàn trang. |

## 15. Challenges

| ID | P | Given/When/Then |
|---|---|---|
| AC-CHAL-001 | P0 | Public GET challenges chỉ trả challenge published. |
| AC-CHAL-002 | P0 | USER gọi admin challenge endpoints nhận 403. |
| AC-CHAL-003 | P0 | ADMIN tạo challenge nhận draft với đúng `startDate`, `endDate`, `goalBooks`, `coverImageUrl`. |
| AC-CHAL-004 | P0 | End date không sau start date trả 400. |
| AC-CHAL-005 | P0 | Goal books bằng 0 hoặc lớn hơn 1.000 trả 400. |
| AC-CHAL-006 | P0 | ADMIN publish challenge; public list bắt đầu hiển thị. |
| AC-CHAL-007 | P0 | Không đổi start/end/goal sau publish; API trả 409. |
| AC-CHAL-008 | P0 | User join active published challenge trong một serialized application operation atomic: response đã commit có `isJoined=true`, participant count tăng một và `currentBooks` được suy ra ngay từ thư viện thật; lỗi initial sync/completion ném trước khi bắt đầu commit rollback participation. Nếu cancellation/mất response xảy ra trong lúc hoặc sau commit, client GET detail/`my` để đối soát; retry join đã commit trả 409. |
| AC-CHAL-009 | P0 | Join draft/challenge đã kết thúc trả 409. |
| AC-CHAL-010 | P0 | Join lặp lại trả 409, không tăng participant count. |
| AC-CHAL-011 | P0 | `currentBooks` đếm shelf `READ` có `finishedAt` trong khoảng UTC đóng `[startDate, endDate]`, không phụ thuộc `joinedAt`; client không có endpoint/UI ghi progress. |
| AC-CHAL-012 | P0 | Progress đã đồng bộ là atomic high-water mark: không giảm khi shelf đổi về sau hoặc khi sync đồng thời, không vượt goal và list/detail/`my`/dashboard không trả stale trước filter/phân trang. |
| AC-CHAL-013 | P0 | Mutation lần đầu hoàn tất sách tạo completion ngay, không cần mở challenge; đạt goal đặt `completedAt` một lần và unique event key bảo đảm đúng một notification `CHALLENGE` link `/challenges/{id}` khi request đồng thời hoặc đọc lại. |
| AC-CHAL-014 | P0 | Route canonical `GET /api/challenges/my` chỉ trả challenge principal đã tham gia; alias tương thích `GET /api/challenges/mine` trả payload tương đương. |
| AC-CHAL-015 | P0 | Join, unpublish và delete cùng dùng serialized, non-deferred SQLite challenge write boundary lấy lock trước khi đọc điều kiện: join thắng làm admin mutation trả 409; admin mutation thắng làm join trả conflict/not-found. Guard admin tính mọi row vật lý `ChallengeParticipation` của challenge, kể cả row đã soft-delete hoặc thuộc user đã soft-delete, nên các use case không thể tạo mới challenge draft/đã xóa kèm participation có thể xuất hiện lại khi restore; nếu dữ liệu cũ đã vi phạm thì admin mutation vẫn phải trả 409 và giữ nguyên trạng thái. Acquire/retry lock là ngắn và cancellable; hủy trước khi lấy lock không chạy callback hoặc commit mutation. |
| AC-CHAL-016 | P0 | Leave load/remove/sync/map trong một transaction và controller không đọc lại: response có `isJoined=false`, `currentBooks=0`; leave lặp lại trả 404. Nếu response bị mất tại/sau commit, client dùng GET detail/`my` để đối soát. |

## 16. Notifications

| ID | P | Given/When/Then |
|---|---|---|
| AC-NOTI-001 | P0 | GET notifications chỉ trả notification principal, sắp mới nhất trước. |
| AC-NOTI-002 | P0 | `unreadOnly=true` chỉ trả `isRead=false`. |
| AC-NOTI-003 | P0 | Unread count bằng số notification chưa đọc. |
| AC-NOTI-004 | P0 | Mark read đổi `isRead=true`; gọi lại idempotent. |
| AC-NOTI-005 | P0 | User không đọc/mark notification của user khác. |
| AC-NOTI-006 | P0 | Mark all read đặt toàn bộ notification principal thành đã đọc, unread count bằng 0. |
| AC-NOTI-007 | P0 | UI badge và `/notifications` cập nhật sau mark read/mark all không reload trang. |

## 17. Dashboard

| ID | P | Given/When/Then |
|---|---|---|
| AC-DASH-001 | P0 | `booksRead` bằng số library item shelf READ của principal. |
| AC-DASH-002 | P0 | `pagesRead` và `readingMinutes` bằng tổng session principal. |
| AC-DASH-003 | P0 | `currentStreak` tính theo ngày UTC/local policy đã cố định và không âm. |
| AC-DASH-004 | P0 | `weeklyPages` có đúng 7 điểm ngày, ngày không đọc có value 0. |
| AC-DASH-005 | P0 | `currentlyReading` chỉ chứa shelf READING. |
| AC-DASH-006 | P0 | `recentSessions` tối đa 5, mới nhất trước. |
| AC-DASH-007 | P0 | `activeChallenges` chỉ chứa challenge principal tham gia và chưa kết thúc. |
| AC-DASH-008 | P0 | Dashboard chỉ đọc BookSpace DB; Bookstore tắt vẫn trả 200. |

## 18. Frontend route và UX

### 18.1 Route công khai

| ID | P | Route | Tiêu chí |
|---|---|---|---|
| AC-WEB-001 | P0 | `/` | render hero, featured books từ API và CTA điều hướng được |
| AC-WEB-002 | P0 | `/explore` | search/filter/sort sách, loading/error/empty state đầy đủ |
| AC-WEB-003 | P0 | `/books` | catalog phân trang từ API |
| AC-WEB-004 | P0 | `/books/:id` | metadata, shelf action và review thật |
| AC-WEB-005 | P0 | `/login` | validation, login, redirect intended route |
| AC-WEB-006 | P0 | `/register` | validation, register, session bootstrap |
| AC-WEB-007 | P0 | `/users/:id` | tabs tổng quan/kệ sách/review/activity, privacy state, follow/unfollow và dialog connections |
| AC-WEB-007A | P0 | `/people` | URL search, directory pagination, guest CTA, suggestions có reason và follow/unfollow trực tiếp |
| AC-WEB-008 | P0 | `/clubs` | list/search và empty state |
| AC-WEB-009 | P0 | `/clubs/:id` | detail, join/leave, posts theo quyền |
| AC-WEB-009A | P0 | `/clubs/:clubId/sprints/:sprintId` | join/leave, progress, leaderboard, timeline, manager controls và milestone thread theo permission DTO |
| AC-WEB-010 | P0 | `/challenges` | list, join/leave, progress tự động và card link tới detail |
| AC-WEB-010A | P0 | `/challenges/:id` | deep-link detail, loading/error/empty/unauthenticated CTA, join/leave không reload |

### 18.2 Route protected

| ID | P | Route | Tiêu chí |
|---|---|---|---|
| AC-WEB-011 | P0 | `/dashboard` | tất cả dashboard metrics từ API |
| AC-WEB-012 | P0 | `/library` | ba shelf, update progress, remove item |
| AC-WEB-013 | P0 | `/journal` | list/create session |
| AC-WEB-014 | P0 | `/feed` | feed phân trang từ network |
| AC-WEB-015 | P0 | `/notifications` | list/read/read-all |
| AC-WEB-016 | P0 | `/settings` | update display name, bio, avatar và hai quyền riêng tư hành trình đọc |
| AC-WEB-017 | P0 | `/profile` | hiển thị current user hoặc redirect đúng `/users/:id` |
| AC-WEB-018 | P0 | `/goals` | list/filter, create/update/delete goal; progress/status hiển thị từ API, không có UI ghi progress tay |
| AC-WEB-019 | P0 | `/notes` | list/filter/search, create/update/delete note; edit không đổi book và PATCH không gửi `bookId` |
| AC-WEB-020 | P0 | `/insights` | overview, rolling heatmap, streak, weekly/monthly, comparison và forecast từ API theo offset trình duyệt |

Khách vào từng route AC-WEB-011 đến AC-WEB-020 phải chuyển `/login` sau khi auth bootstrap kết thúc.

### 18.3 Route admin

| ID | P | Route | Tiêu chí |
|---|---|---|---|
| AC-WEB-021 | P0 | `/admin/books` | ADMIN create/patch/delete book bằng `/api/admin/books`; USER bị chặn |
| AC-WEB-022 | P0 | `/admin/challenges` | ADMIN create/patch/publish/delete challenge; USER bị chặn |

### 18.4 Trạng thái chung

| ID | P | Given/When/Then |
|---|---|---|
| AC-WEB-023 | P0 | Mỗi route lazy-load và có loading state không làm nhảy về login sớm. |
| AC-WEB-024 | P0 | Network/API error hiện message có thể hành động, không render màn hình trắng. |
| AC-WEB-025 | P0 | Empty list có empty state phù hợp, không hiện spinner vô hạn. |
| AC-WEB-026 | P0 | Mutation thành công cập nhật cache/feedback; mutation lỗi rollback optimistic state. |
| AC-WEB-027 | P0 | 401 chỉ refresh một lần; refresh thất bại xóa session và về login. |
| AC-WEB-028 | P0 | Frontend chỉ unwrap exact `ApiResponse<T>`; payload direct bị coi là contract error. |
| AC-WEB-029 | P1 | Layout dùng được ở 360px, 768px, 1280px; không có overflow ngang ngoài thành phần chủ đích. |
| AC-WEB-030 | P1 | Form có label, keyboard focus, disabled/loading state và error gắn đúng field. |
| AC-WEB-031 | P1 | Màu chữ/nút/focus đạt contrast cơ bản ở light và dark theme. |
| AC-WEB-032 | P0 | People/profile/feed query chờ auth bootstrap và dùng principal-scoped key; guest, account A và account B không dùng lại relationship state. |
| AC-WEB-033 | P0 | Follow dùng response server để cập nhật state, chặn double click và invalidate people, target/current profile counters, followers/following, feed và dashboard. |

## 19. Security và isolation

| ID | P | Given/When/Then |
|---|---|---|
| AC-SEC-001 | P0 | Đổi UUID trong URL không truy cập library/session/goal/note/notification của user khác. |
| AC-SEC-002 | P0 | USER gọi mọi route `/api/admin/*` nhận 403. |
| AC-SEC-003 | P0 | HTML/script trong bio/review/comment/post không thực thi ở frontend. |
| AC-SEC-004 | P0 | CORS Development cho phép đúng frontend local và từ chối origin không allowlist. |
| AC-SEC-005 | P0 | JWT sai issuer, audience, signature hoặc expiry bị từ chối. |
| AC-SEC-006 | P0 | JWT do Bookstore ký bị từ chối trong Goal 1. |
| AC-SEC-007 | P0 | Production config không dùng Docker local JWT key hoặc seed password. |
| AC-SEC-008 | P0 | Log không chứa password, bearer token, refresh token, DB connection secret hoặc webhook secret. |
| AC-SEC-009 | P1 | Login/refresh endpoint có rate limit và trả 429 envelope. |
| AC-SEC-010 | P1 | Nội dung người dùng nhập bị giới hạn độ dài ở API, không chỉ ở UI. |

## 20. Tính độc lập và integration

| ID | P | Given/When/Then |
|---|---|---|
| AC-IND-001 | P0 | Không cài/chạy Bookstore, BookSpace vẫn restore/build/test/start. |
| AC-IND-002 | P0 | `BOOKSPACE_BookstoreIntegration__Enabled=false`, health trả 200. |
| AC-IND-003 | P0 | Integration tắt, register/login/catalog/library/session/goal/note/review/club/reading sprint/challenge/dashboard/insights vẫn đạt. |
| AC-IND-004 | P0 | Integration tắt, không có outbound request Bookstore trong log/network. |
| AC-IND-005 | P0 | `/api/external-books/search` khi tắt trả 200, `success=true`, `data.available=false`, `items=[]`; API không crash. |
| AC-IND-006 | P0 | Source/config BookSpace không chứa connection string trỏ database Bookstore. |
| AC-IND-007 | P0 | Source/config không copy JWT signing secret, refresh secret hoặc password hash Bookstore. |
| AC-IND-008 | P1 | Khi bật provider với Bookstore test URL, search map đúng external ID/title/authors/cover/ISBN/price/purchase URL. |
| AC-IND-009 | P1 | Provider timeout trả result `available=false` trong timeout budget và core health vẫn xanh. |
| AC-IND-010 | P1 | Payload upstream không nhận diện được trả result có giới hạn, không lưu dữ liệu rác. |

## 21. Coverage tự động tối thiểu

### Unit tests

- Domain invariant cho User login availability.
- Follow self.
- Library transition và page bounds.
- Reading session time/pages/duration.
- Reading goal enum/target/date/overlap, derived progress và completion idempotency.
- Reading note quote/content/page/tag invariants.
- Reading Insights range/offset, local-day grouping, streak, comparison và forecast.
- Review rating.
- Club owner role.
- Challenge period, derived high-water progress và completion idempotency.
- Refresh token revoke/rotation.
- Notification mark read idempotent.

### Integration tests

- Auth register/login/refresh/logout.
- Role policy cho admin route.
- Catalog query/pagination.
- Library ownership.
- Reading goal status synchronization/filter, completion notification và ownership.
- Reading note CRUD/filter/search/owner isolation.
- Reading Insights auth, range validation, UTC+7 calendar, reports, comparison, forecasts, completion sync và owner isolation.
- Review unique/like/comment.
- Club membership/post permission.
- Reading sprint lifecycle, permission, participant idempotency, progress, leaderboard, timeline, milestone/response, reminder deduplication và private-club isolation.
- Challenge detail published/draft, atomic join/rollback, concurrent duplicate join 409, serialized join-vs-unpublish/delete, physical-participation guard, cancellable lock acquisition, leave không post-read và nonparticipant leave, progress từ sách hoàn tất trước/sau join, deterministic stale-low high-water, repair `CompletedAt`, mutation-time completion và notification dedupe/unique constraint.
- Notification ownership.
- Global error envelope.
- Database unique constraints.
- Integration disabled behavior.

### Frontend tests

- Axios envelope unwrap.
- Auth refresh một lần.
- Protected/admin route.
- Library mutation field `shelf`.
- Reading goal route/form and no manual-progress request.
- Reading note PATCH payload không có `bookId`, quote/content/tag bounds.
- Reading Insights query key có timezone offset, route protected và session/goal mutation invalidate cache.
- Review request top-level `/reviews`.
- Admin request path `/admin/books` và `/admin/challenges`.
- Production App deep-link challenge, loading/error/empty/guest CTA, intended login return, principal-scoped cache, join/leave invalidation, reading-mutation challenge invalidation và không có manual-progress request.
- Loading/error/empty states cho catalog, library, notifications.

Không đặt ngưỡng coverage phần trăm giả tạo cho Goal 1. Mọi invariant và authorization branch liệt kê trên phải có test trực tiếp.

## 22. Điều kiện ký nghiệm thu

Goal 1 chỉ hoàn thành khi:

1. AC P0 từ phần 2 đến phần 19 đạt.
2. Không còn drift field/path giữa frontend service, API controller và tài liệu.
3. Unit/integration/frontend test bắt các contract quan trọng ở phần 19.
4. Seed Development hoạt động và Production không seed tài khoản demo.
5. BookSpace chạy độc lập khi Bookstore không tồn tại.
6. README có lệnh chạy, URL, tài khoản dev và hướng xử lý lỗi phổ biến.
7. Kết quả verify được ghi lại bằng lệnh, exit code và HTTP/browser evidence thực tế.
