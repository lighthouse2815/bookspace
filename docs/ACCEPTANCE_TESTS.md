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
| AC-BLD-016 | P0 | GitHub Actions chạy backend format/build/test/EF model-drift, frontend `npm ci`/typecheck/lint/test/build và `docker compose config`; bất kỳ job lỗi nào đều làm workflow thất bại và có thể được đặt làm required check trong branch protection. |
| AC-BLD-017 | P0 | `/health` kiểm tra được kết nối database BookSpace với timeout 5 giây; database lỗi trả 503 với body tối thiểu, không lộ connection string hoặc exception trong response/log result. |

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
| AC-API-011 | P0 | Mọi response có `X-Correlation-ID`; ID đầu vào hợp lệ được giữ nguyên, ID thiếu/không hợp lệ được thay bằng ID an toàn do server tạo. |

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

## 4A. Personalized onboarding v1

| ID | P | Given/When/Then |
|---|---|---|
| AC-ONB-001 | P0 | Register user mới trả session dùng được; `GET /api/users/me/onboarding` trả đúng bốn field `status=PENDING`, `finishedAt=null`, `preferredCategoryIds=[]`, `referenceBookIds=[]`. |
| AC-ONB-002 | P0 | Cả bốn endpoint onboarding thiếu/sai token trả 401; admin không có endpoint đọc preference theo user ID. |
| AC-ONB-003 | P0 | Khi state là `PENDING`/`SKIPPED`, PUT 0–2 ID duy nhất mỗi tập lưu được draft, full-replace cả hai mảng, không tự đổi state và tải lại bằng GET giữ đúng dữ liệu; thiếu/null một mảng trả `VALIDATION_ERROR`. |
| AC-ONB-004 | P0 | Duplicate ID được chuẩn hóa thành tập duy nhất; hơn 5 ID duy nhất trả đúng error limit tiếng Việt và không thay đổi tập nào. |
| AC-ONB-005 | P0 | Category/book không tồn tại hoặc soft-delete trả `...NOT_FOUND`; update hai tập atomic, không để một tập mới và một tập cũ. Full-replace phải xóa cả association bị query filter che khi target soft-delete, nên restore target không làm preference cũ xuất hiện lại. |
| AC-ONB-006 | P0 | Complete khi một tập có dưới 3 target active trả 400 `ONBOARDING_INCOMPLETE`, giữ `PENDING` và `finishedAt=null`. |
| AC-ONB-007 | P0 | Với 3–5 category và 3–5 reference book active, complete trả `COMPLETED`, `finishedAt` UTC; GET phản ánh cùng state và timestamp. |
| AC-ONB-008 | P0 | Complete lặp lại idempotent, không đổi `finishedAt`; preference đã lưu có thể sửa bằng PUT mà không tự thay đổi status/timestamp, nhưng state `COMPLETED` từ chối mọi payload làm một tập còn dưới 3 target active. |
| AC-ONB-009 | P0 | Skip từ `PENDING` trả `SKIPPED` và timestamp; retry không đổi timestamp và không xóa draft. |
| AC-ONB-010 | P0 | `SKIPPED` có thể complete khi đủ lựa chọn; skip sau `COMPLETED` không downgrade hoặc đổi timestamp. |
| AC-ONB-011 | P0 | Public profile, directory, feed và response user khác không chứa status, category IDs hoặc reference book IDs của owner. |
| AC-ONB-012 | P0 | Recommendation hợp nhất preferred category và author/category từ reference books với tín hiệu hiện hữu, giữ reason code cũ và luôn loại chính reference books khỏi candidate/count/pagination. |
| AC-ONB-013 | P0 | Recommendation của user đã skip không có preference vẫn dùng `POPULAR_FALLBACK`; không đọc preference của user khác và không gọi Bookstore/ML. |
| AC-ONB-014 | P0 | PUT/complete/skip đồng thời được tuần tự hóa: complete và skip luôn kết thúc ở `COMPLETED`; draft dưới 3 phần tử không thể commit vào state `COMPLETED`, timestamp và preference vẫn nhất quán. |

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
| AC-CAT-013 | P0 | Guest gọi `GET /api/books/recommendations` nhận 401 `UNAUTHORIZED`; member nhận `ApiResponse<PageResult<BookRecommendationResponse>>` với mặc định `page=1`, `pageSize=12`. |
| AC-CAT-014 | P0 | Mỗi recommendation có đúng `book`, `reasonCode`, `reasonText`; code thuộc `FOLLOWED_READER_LIKED`, `MATCHED_AUTHOR`, `MATCHED_CATEGORY`, `POPULAR_FALLBACK` và text khớp mapping tiếng Việt trong API contract. |
| AC-CAT-015 | P0 | Recommendation loại mọi sách đang ở library active của principal bất kể shelf và mọi sách principal đã review, loại book soft delete trước count/phân trang và trả `book.shelf=null`. |
| AC-CAT-016 | P0 | Sách có nhiều review 4–5 sao hơn từ các user active principal đang follow đứng trước; review rating dưới 4, đã xóa hoặc của user locked/soft delete không tạo social signal. |
| AC-CAT-017 | P0 | Khi không có social signal, author match từ library/review 4–5 sao của principal ưu tiên category overlap; sau đó dùng global average rating, review count và `book.id asc`. |
| AC-CAT-018 | P0 | Hai request cùng principal/dữ liệu trả cùng thứ tự; ghép nhiều page không trùng/mất item và metadata count phản ánh candidate sau exclusion. |
| AC-CAT-019 | P0 | Tài khoản cold-start không library/review/follow vẫn nhận candidate `POPULAR_FALLBACK` theo rating/review count công khai và tie-break ổn định. |
| AC-CAT-020 | P0 | Ranking không đọc library/session/note của user khác; global fallback không biến dữ liệu đọc riêng tư thành reason. |
| AC-CAT-021 | P0 | Sau library, review hoặc follow mutation liên quan, request recommendation mới phản ánh source hiện tại; read model không giữ server cache stale. |

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
| AC-ADM-013 | P0 | USER gọi `POST /api/admin/books/import` nhận 403; request thiếu token nhận 401. |
| AC-ADM-014 | P0 | ADMIN tìm metadata ngoài, chọn kết quả và import; book nhận `Guid` nội bộ rồi xuất hiện trong catalog, onboarding và recommendation candidate theo luật hiện hữu. |
| AC-ADM-015 | P0 | Import tải lại detail theo external ID; author/category được ghép theo ID/tên không phân biệt hoa thường hoặc tạo mới atomically. |
| AC-ADM-016 | P0 | Retry cùng `(provider, externalId)` trả `ALREADY_IMPORTED`, cùng `book.id`, không gọi provider lại và không tạo link/book/relation trùng. |
| AC-ADM-017 | P0 | ISBN ngoài sau chuẩn hóa trùng sách active trả `LINKED_EXISTING`, chỉ tạo source link và không ghi đè metadata nội bộ. |
| AC-ADM-018 | P0 | Import mới thiếu author, category hoặc page count trả lỗi tiếng Việt ổn định và không lưu row dở dang. |
| AC-ADM-019 | P0 | Provider tắt/lỗi trả 503 `EXTERNAL_CATALOG_UNAVAILABLE` cho import mới; catalog và mọi core flow vẫn hoạt động. |
| AC-ADM-020 | P0 | Unique `(Provider, ExternalId)` và FK `BookId` restrict được migration/model bảo vệ. |

## 8. Library và state transition

| ID | P | Given/When/Then |
|---|---|---|
| AC-LIB-001 | P0 | Reader thêm book chưa có vào shelf `WANT_TO_READ`, API trả 201 và `currentPage=0`. |
| AC-LIB-002 | P0 | Thêm lại cùng book còn active trong library trả 409 `BOOK_ALREADY_IN_LIBRARY`. |
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
| AC-LIB-013 | P0 | Thêm lại book đã soft-delete restore đúng library item ID; `READING` giữ progress high-water, `WANT_TO_READ` reset 0 và `READ` đạt page count. |

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
| AC-READ-012 | P0 | `GET /api/reading-sessions/active` trả `data=null` khi không có phiên và chỉ trả active session của principal khi có. |
| AC-READ-013 | P0 | Start dùng UTC server, snapshot current page, tạo/chuyển library item sang `READING`; item từng soft-delete được restore đúng ID và giữ tiến độ; start lần hai hoặc start sách `READ` bị từ chối. |
| AC-READ-014 | P0 | Mỗi user chỉ có một active row ở database; hai start cạnh tranh không thể cùng commit thành công. |
| AC-READ-015 | P0 | Pause đóng elapsed hiện tại, thời gian chờ không làm elapsed tăng; pause/resume cùng trạng thái là idempotent và reload khôi phục đúng state. |
| AC-READ-016 | P0 | Finish dưới 60 giây, `endingPage<=startPage`, nhỏ hơn current library page hoặc vượt page count bị từ chối mà active session còn nguyên. |
| AC-READ-017 | P0 | Finish hợp lệ tiêu thụ active đúng một lần, tạo completed session với active minutes/pages delta, cập nhật library tuyệt đối và đồng bộ challenge/goal notification. |
| AC-READ-018 | P0 | Cancel xóa active state nhưng không tạo completed history, không thay đổi library, goals, challenge, feed hoặc insights. |
| AC-READ-019 | P0 | Owner correction session cập nhật history/projection; user khác nhận 404. Delta trang dương có thể đẩy library tới trước nhưng correction giảm không làm lùi library/challenge/completed goal; nếu cùng sách đang Focus thì delta dương bị chặn, còn note/time/within-high-water vẫn hợp lệ. |
| AC-READ-020 | P0 | Ghi chú manual/focus/correction chỉ có trong history của owner và không xuất hiện ở Feed, hồ sơ, club hoặc notification. |
| AC-READ-021 | P0 | Start/finish Focus cạnh tranh với đổi kệ/xóa/cập nhật tiến độ/ghi manual vẫn được serialize: active session không mồ côi và library progress không bao giờ bị ghi lùi bởi entity stale. Manual cùng active book trả 409; manual book khác hợp lệ; nếu manual commit trước Start thì `startPage` phản ánh progress mới. |
| AC-READ-022 | P0 | Nếu catalog book bị admin soft-delete giữa phiên, GET/pause/resume vẫn trả active DTO với `book=null`; UI render fallback và cho phép cancel thay vì nhốt user ở error state. |

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
| AC-FEED-003 | P0 | Feed trả đúng event type `REVIEW`, `READING_PROGRESS`, `BOOK_FINISHED`, `CHALLENGE` hoặc `CLUB_POST` cùng payload liên quan. |
| AC-FEED-004 | P0 | Feed áp dụng filter/visibility trước count và phân trang, rồi sắp `createdAt desc, id desc` ổn định qua các trang. |
| AC-FEED-005 | P0 | Private club activity không lộ cho người ngoài. |
| AC-FEED-006 | P0 | `type=REVIEW`, `CLUB`, `CHALLENGE` chỉ trả event tương ứng; `type=READING` gộp đúng `READING_PROGRESS` và `BOOK_FINISHED`; bỏ `type` trả mọi nhóm. |
| AC-FEED-007 | P0 | `type` ngoài `REVIEW`, `READING`, `CLUB`, `CHALLENGE` trả 400 `INVALID_FEED_TYPE` với message tiếng Việt. |
| AC-FEED-008 | P0 | Mỗi `ReadingSession` tạo projection `READING_PROGRESS` theo `StartedAt`, với phần trăm bằng tỷ lệ trang của riêng phiên trên tổng trang sách; sách hoàn tất tạo `BOOK_FINISHED` theo đúng `LibraryItem.FinishedAt`, không dùng `LibraryItem.StartedAt`. |
| AC-FEED-009 | P0 | Principal luôn thấy hoạt động đọc của mình; hoạt động đọc của user đang follow chỉ xuất hiện khi actor bật `IsReadingActivityPublic`. |
| AC-FEED-010 | P0 | `ReadingSession.Note` và `ReadingNote` không xuất hiện trong field `note`, `content` hoặc field nào của feed response. |

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

### 14.1 Club chat realtime

| ID | P | Given/When/Then |
|---|---|---|
| AC-CHAT-001 | P0 | Active member gửi content hợp lệ nhận 201, message được persist và history trả đúng sender/content/thời gian. |
| AC-CHAT-002 | P0 | Content rỗng hoặc vượt 2.000 ký tự trả validation tiếng Việt; client không thể giả sender/user ID. |
| AC-CHAT-003 | P0 | Nonmember public club nhận 403 `CLUB_CHAT_MEMBERSHIP_REQUIRED`; nonmember private club nhận 404 cho history/send/unread/read dù biết UUID. |
| AC-CHAT-004 | P0 | Cursor history sắp `createdAt desc, id desc`, không trùng/bỏ item giữa các trang và giới hạn page size. |
| AC-CHAT-005 | P0 | Unread chỉ tính message của member khác sau thời điểm membership/read marker; message của chính principal và lịch sử trước membership không tăng count. |
| AC-CHAT-006 | P0 | Mark-read dùng message thuộc đúng club, idempotent và high-water không lùi khi request cũ đến sau. |
| AC-CHAT-007 | P0 | Mỗi message tạo notification `CLUB` cho active member khác khi preference bật, không tạo cho sender/member đã rời hoặc preference tắt. |
| AC-CHAT-008 | P0 | SignalR chỉ chấp nhận JWT trên hub path và phát `ClubChatMessageCreated` tới user active; member vừa leave/kick không nhận event sau đó. |
| AC-CHAT-009 | P0 | UI chỉ kết nối/mount chat cho authenticated member, merge POST và event theo ID không trùng, reconnect refetch để lấp event bị mất. |
| AC-CHAT-010 | P0 | UI tải tin cũ bằng cursor, giữ vị trí khi đang đọc phía trên, hiện badge tin mới và mark-read khi người dùng thực sự ở cuối transcript. |

## 14A. Direct Messages v1

| ID | P | Given/When/Then |
|---|---|---|
| AC-DM-001 | P0 | Hai active user chỉ có thể mở cuộc trò chuyện khi đang theo dõi lẫn nhau; tự nhắn, follow một chiều hoặc user đã khóa trả lỗi tiếng Việt phù hợp. |
| AC-DM-002 | P0 | Cùng một cặp user luôn nhận đúng một conversation chuẩn hóa dù hai phía gửi request mở hội thoại đồng thời hoặc đảo thứ tự target. |
| AC-DM-003 | P0 | Chỉ participant đọc được detail/history; đổi UUID sang conversation khác trả 404 và không làm lộ participant hoặc message. |
| AC-DM-004 | P0 | Message v1 chỉ có text không rỗng, tối đa 2.000 ký tự; sender lấy từ principal, không nhận user ID từ client. |
| AC-DM-005 | P0 | Gửi message persist message, cập nhật `lastActivityAt`, sắp inbox mới nhất trước và tạo notification `DIRECT_MESSAGE` cho recipient khi preference bật. |
| AC-DM-006 | P0 | History dùng cursor theo `createdAt desc, id desc`, không trùng/bỏ item giữa các trang; inbox và DTO công khai không lộ email hoặc dữ liệu riêng tư. |
| AC-DM-007 | P0 | Unread chỉ đếm message của người còn lại sau read marker; mark-read idempotent, high-water không lùi và message phải thuộc đúng conversation. |
| AC-DM-008 | P0 | SignalR `/hubs/direct-messages` yêu cầu JWT, chỉ phát `DirectMessageCreated` sau commit tới participant được phép; reconnect refetch REST để bù event bị mất. |
| AC-DM-009 | P0 | Sau khi một bên unfollow, cả hai vẫn đọc được lịch sử nhưng `canSend=false` và send mới trả `DIRECT_MESSAGE_MUTUAL_FOLLOW_REQUIRED`. |
| AC-DM-010 | P0 | Block theo một trong hai chiều làm cả hai phía không còn thấy/mở conversation; unblock không tự khôi phục follow. |
| AC-DM-011 | P0 | Mute actor một chiều lọc message, unread, notification mới và realtime của actor khỏi principal nhưng không cấm actor thực hiện mutation hợp lệ. |
| AC-DM-012 | P0 | Participant có thể report `DIRECT_MESSAGE`; admin chọn `CONTENT_REMOVED` soft-delete message khỏi history và giữ audit/report snapshot. |
| AC-DM-013 | P0 | UI `/messages` và `/messages/:conversationId` có inbox, badge unread, loading/error/empty state, cursor history, gửi text, mark-read và cập nhật realtime không trùng message. |

### 14.2 Đợt đọc chung

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
| AC-NOTI-008 | P0 | List và unread-count lọc đúng category; `REVIEW` gồm cả `REVIEW_LIKE` và `COMMENT`, pagination có tie-break `id desc`; enum số không xác định trả `INVALID_NOTIFICATION_CATEGORY`. |
| AC-NOTI-009 | P0 | Preference mặc định bật cho follow/review/club/challenge/direct message; PATCH lưu đủ năm flag và chỉ principal đọc/sửa được. |
| AC-NOTI-010 | P0 | Khi một preference tắt, sự kiện mới tương ứng không insert notification nhưng nghiệp vụ gốc vẫn commit; bật lại cho phép sự kiện sau tạo notification. |
| AC-NOTI-011 | P0 | `SYSTEM` luôn được tạo dù năm preference đang tắt; đổi preference không xóa hoặc ẩn notification lịch sử. |

## 17. Community Safety

| ID | P | Given/When/Then |
|---|---|---|
| AC-SAFE-001 | P0 | Authenticated user report được `USER`, review/comment, club post/comment, club chat message và direct message đang nhìn thấy; guest nhận 401. |
| AC-SAFE-002 | P0 | User không thể report target của chính mình; một report pending trùng principal/target trả 409. |
| AC-SAFE-003 | P0 | Outsider report nội dung private club hoặc chat nhận 404 và không suy ra target tồn tại. |
| AC-SAFE-004 | P0 | Snapshot tối đa 500 ký tự được lưu cùng target owner/link; public response không lộ email hoặc dữ liệu riêng tư. |
| AC-SAFE-005 | P0 | USER gọi `/api/admin/reports` hoặc resolution nhận 403; ADMIN lọc queue theo status/target/reason và pagination ổn định. |
| AC-SAFE-006 | P0 | `CONTENT_REMOVED` soft-delete target khỏi public query và đóng các report pending sibling; audit giữ moderator/action/note/time. |
| AC-SAFE-007 | P0 | `USER_LOCKED` không áp dụng cho ADMIN; token cũ của user bị khóa nhận 401 ở request tiếp theo và profile/nội dung bị ẩn. |
| AC-SAFE-008 | P0 | `DISMISSED` chỉ đi với `NONE`; `RESOLVED` phải có action; exact retry cùng quyết định idempotent. |
| AC-SAFE-009 | P0 | Block user khác trả safety entry, tự gỡ follow hai chiều; block lặp lại idempotent, tự block trả 400 `CANNOT_BLOCK_SELF`. |
| AC-SAFE-010 | P0 | Khi có block theo một trong hai chiều, cả hai bên nhận 404 khi xem profile/nội dung của nhau và 403 `USER_RELATION_BLOCKED` khi follow/like/comment. |
| AC-SAFE-011 | P0 | Block loại hai user khỏi search, suggestions, followers/following, feed, review tổng hợp, club post/comment, club chat/unread, direct message inbox/history/unread và notification mới có actor. |
| AC-SAFE-012 | P0 | Unblock lặp lại idempotent, khôi phục visibility nhưng không tự khôi phục follow cũ. |
| AC-SAFE-013 | P0 | Mute một chiều vẫn cho xem profile/follow nhưng loại actor khỏi feed, review tổng hợp, club post/comment, chat/unread, direct message history/unread/realtime, recommendation social signal và notification mới. |
| AC-SAFE-014 | P0 | Unmute khôi phục read model; mute/unmute lặp lại idempotent, tự mute trả 400 và mute khi đang block trả 409 `USER_RELATION_BLOCKED`. |
| AC-SAFE-015 | P0 | `GET /api/users/me/safety` chỉ trả block/mute do principal tạo, phân trang mới nhất trước và không lộ user đã chặn principal. |

## 18. Dashboard

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

## 19. Frontend route và UX

### 19.1 Route công khai

| ID | P | Route | Tiêu chí |
|---|---|---|---|
| AC-WEB-001 | P0 | `/` | render hero, featured books từ API và CTA điều hướng được |
| AC-WEB-002 | P0 | `/explore` | search/filter/sort sách, loading/error/empty state đầy đủ; member thấy khu “Dành cho bạn” từ API |
| AC-WEB-003 | P0 | `/books` | catalog phân trang từ API |
| AC-WEB-004 | P0 | `/books/:id` | metadata, shelf action và review thật |
| AC-WEB-005 | P0 | `/login` | validation, login, redirect intended route |
| AC-WEB-006 | P0 | `/register` | validation, register, session bootstrap và chuyển `/onboarding` với intended path nội bộ đã sanitize |
| AC-WEB-007 | P0 | `/users/:id` | tabs tổng quan/kệ sách/review/activity, privacy state, follow/unfollow và dialog connections |
| AC-WEB-007A | P0 | `/people` | URL search, directory pagination, guest CTA, suggestions có reason và follow/unfollow trực tiếp |
| AC-WEB-008 | P0 | `/clubs` | list/search và empty state |
| AC-WEB-009 | P0 | `/clubs/:id` | detail, join/leave, posts theo quyền |
| AC-WEB-009A | P0 | `/clubs/:clubId/sprints/:sprintId` | join/leave, progress, leaderboard, timeline, manager controls và milestone thread theo permission DTO |
| AC-WEB-010 | P0 | `/challenges` | list, join/leave, progress tự động và card link tới detail |
| AC-WEB-010A | P0 | `/challenges/:id` | deep-link detail, loading/error/empty/unauthenticated CTA, join/leave không reload |

### 19.2 Route protected

| ID | P | Route | Tiêu chí |
|---|---|---|---|
| AC-WEB-011 | P0 | `/dashboard` | tất cả dashboard metrics từ API |
| AC-WEB-012 | P0 | `/library` | ba shelf, update progress, remove item |
| AC-WEB-013 | P0 | `/journal` | focus timer server-backed có start/pause/resume/finish/cancel, recovery sau reload, list/create/correction completed session |
| AC-WEB-014 | P0 | `/feed` | feed 10 item/trang từ network, bộ lọc, phân trang, gợi ý follow và empty CTA tới `/people` |
| AC-WEB-015 | P0 | `/notifications` | server unread count, tab all/unread, category filter, URL pagination, deep-link và optimistic read/read-all |
| AC-WEB-016 | P0 | `/settings` | update display name, bio, avatar, hai quyền riêng tư đọc, năm notification preferences, quản lý bỏ ẩn/bỏ chặn và liên kết “Sở thích đọc” tới `/onboarding?mode=edit` |
| AC-WEB-017 | P0 | `/profile` | hiển thị current user hoặc redirect đúng `/users/:id` |
| AC-WEB-018 | P0 | `/goals` | list/filter, create/update/delete goal; progress/status hiển thị từ API, không có UI ghi progress tay |
| AC-WEB-019 | P0 | `/notes` | list/filter/search, create/update/delete note; edit không đổi book và PATCH không gửi `bookId` |
| AC-WEB-020 | P0 | `/insights` | overview, rolling heatmap, streak, weekly/monthly, comparison và forecast từ API theo offset trình duyệt |
| AC-WEB-020A | P0 | `/onboarding` | protected, khôi phục state server, lưu draft 3–5 thể loại/sách tham chiếu, complete/skip và các quick action tùy chọn |
| AC-WEB-020B | P0 | `/messages` | inbox principal, badge unread, loading/error/empty state và cập nhật realtime |
| AC-WEB-020C | P0 | `/messages/:conversationId` | lịch sử cursor, gửi text, mark-read và trạng thái không thể gửi sau unfollow |

Khách vào từng route AC-WEB-011 đến AC-WEB-020C phải chuyển `/login`
sau khi auth bootstrap kết thúc.

### 19.3 Route admin

| ID | P | Route | Tiêu chí |
|---|---|---|---|
| AC-WEB-021 | P0 | `/admin/books` | ADMIN create/patch/delete book bằng `/api/admin/books`; USER bị chặn |
| AC-WEB-022 | P0 | `/admin/challenges` | ADMIN create/patch/publish/delete challenge; USER bị chặn |
| AC-WEB-022A | P0 | `/admin/moderation` | ADMIN lọc queue, xem snapshot, ghi note, bác bỏ, ẩn nội dung hoặc khóa tài khoản; USER bị chặn |

### 19.4 Trạng thái chung

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
| AC-WEB-034 | P0 | `/feed` dùng URL chữ thường `type=review`, `type=reading`, `type=club` hoặc `type=challenge` cùng `page`; service đổi filter sang giá trị API chữ hoa, còn “Tất cả” bỏ hẳn `type`. |
| AC-WEB-035 | P0 | Đổi filter feed đưa `page` về 1; back/forward khôi phục filter/page, suggestion follow biến mất sau thành công và empty state chỉ dẫn `/people`, không dẫn `/explore`. |
| AC-WEB-036 | P0 | Card `READING_PROGRESS` diễn đạt `progressPercent` là phần trăm cuốn sách đọc trong phiên này, không gắn nhãn như tiến độ library tích lũy. |
| AC-WEB-037 | P0 | Guest vào `/explore` không gọi recommendation API và không thấy “Dành cho bạn”; catalog/“Được đọc nhiều” công khai vẫn hoạt động. |
| AC-WEB-038 | P0 | Member thấy recommendation 12 item/trang, reason text từ server, loading/error/empty state và pagination độc lập với catalog search. |
| AC-WEB-039 | P0 | Thêm nhanh recommendation vào `WANT_TO_READ` gọi library contract hiện hữu, chặn double-click; thành công làm item biến mất, lỗi giữ card và hiện feedback có thể hành động. |
| AC-WEB-040 | P0 | Query key recommendation chứa principal ID, page, pageSize; đổi account không dùng lại dữ liệu. Library add/update/remove, reading-session create, review create/update/delete và follow/unfollow invalidate recommendation cache. |
| AC-WEB-041 | P0 | Active timer tick từ `elapsedSeconds` server response, không tự tính cả khoảng pause; mutation chặn double-submit và refetch authoritative state sau lỗi. |
| AC-WEB-042 | P0 | Finish form mặc định từ `startPage`, kiểm tra ending page/note; thành công xóa active panel, thêm history và làm mới library/dashboard/goals/insights/challenges/feed/notifications. |
| AC-WEB-043 | P0 | Entry point Focus từ Library, Dashboard hoặc Book Detail chỉ xuất hiện/hoạt động với book có thể đọc; URL preselect đúng book và không tạo active session trước thao tác xác nhận của user. |
| AC-WEB-044 | P0 | Biểu mẫu manual khóa cuốn đang có Focus session và giải thích lý do; sách khác vẫn chọn được, còn backend tiếp tục là nguồn bảo vệ authoritative cho request cạnh tranh. |
| AC-WEB-045 | P0 | Profile có nút ẩn nội dung và dialog xác nhận chặn; chặn thành công chuyển về `/people`, không để profile target còn trong cache. |
| AC-WEB-046 | P0 | Feed, review và club chat cho phép ẩn actor trực tiếp; mutation làm mới mọi read model principal liên quan và hiển thị feedback tiếng Việt. |
| AC-WEB-047 | P0 | Onboarding query chờ auth bootstrap, key chứa principal ID; reload hoặc đăng nhập lại không mất draft và đổi account không dùng lại preference. |
| AC-WEB-048 | P0 | Chỉ bật Complete khi có 3–5 thể loại và 3–5 sách; mutation có disabled/loading chống double-submit, lỗi tiếng Việt giữ lựa chọn để sửa. |
| AC-WEB-049 | P0 | Complete/Skip quay về intended internal path an toàn, mặc định `/dashboard`; external URL không được dùng làm redirect. Login thường không ép redirect, Dashboard có CTA khi chưa completed. |
| AC-WEB-050 | P0 | Chế độ edit từ Settings tải state hiện tại, lưu preference và quay `/settings`; edit không tự skip hoặc làm đổi terminal status. |
| AC-WEB-051 | P0 | Quick-add recommendation dùng contract `WANT_TO_READ`, follow suggestion dùng contract follow, mục tiêu đầu tiên dùng reading-goal contract; từng bước độc lập, tùy chọn và lỗi không chặn Complete. |
| AC-WEB-052 | P0 | Sau PUT/complete/skip, frontend ghi state authoritative vào cache onboarding theo principal và invalidate recommendation/dashboard/các consumer hiện hữu; quick-add/follow giữ invalidation riêng, reference book không render lại trong recommendation. |
| AC-WEB-053 | P0 | Category picker tải đủ mọi trang catalog, dedupe theo ID và luôn hiển thị preference đã lưu ngoài trang đầu để người dùng có thể bỏ chọn; chuyển bước focus heading mới và Skip lưu draft hiện tại trước khi đổi state. |
| AC-WEB-054 | P0 | `/admin/books` cho ADMIN tìm metadata ngoài, thấy provider-disabled/empty/error state, xem trước, sửa author/category/page/year/language/description và nhận feedback riêng cho imported/linked/already-imported. |

## 20. Security và isolation

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
| AC-SEC-009 | P1 | Login/refresh có rate limit độc lập, cấu hình được và partition theo IP sau trusted-proxy processing; vượt ngưỡng trả 429 `RATE_LIMITED`, message tiếng Việt và `Retry-After` mà không gọi nghiệp vụ auth. |
| AC-SEC-010 | P1 | Nội dung người dùng nhập bị giới hạn độ dài ở API, không chỉ ở UI. |
| AC-SEC-011 | P0 | Recommendation không trả hay suy luận từ library, reading session, reading note hoặc visibility flag của user khác; chỉ review công khai hợp lệ tham gia social/global signal. |
| AC-SEC-012 | P0 | REST và SignalR direct message không cho nonparticipant đọc nội dung, suy ra participant hoặc nhận event; block/mute được áp dụng ở server, không dựa vào UI. |

## 21. Tính độc lập và integration

| ID | P | Given/When/Then |
|---|---|---|
| AC-IND-001 | P0 | Không cài/chạy Bookstore, BookSpace vẫn restore/build/test/start. |
| AC-IND-002 | P0 | `BOOKSPACE_BookstoreIntegration__Enabled=false`, health trả 200. |
| AC-IND-003 | P0 | Integration tắt, register/login/catalog/library/session/goal/note/review/club/reading sprint/challenge/dashboard/insights vẫn đạt. |
| AC-IND-004 | P0 | Integration tắt, không có outbound request Bookstore trong log/network. |
| AC-IND-005 | P0 | `/api/external-books/search` khi tắt trả 200, `success=true`, `data.available=false`, `items=[]`; API không crash. |
| AC-IND-006 | P0 | Source/config BookSpace không chứa connection string trỏ database Bookstore. |
| AC-IND-007 | P0 | Source/config không copy JWT signing secret, refresh secret hoặc password hash Bookstore. |
| AC-IND-008 | P1 | Khi bật provider với Bookstore test URL, search/detail map đúng external ID/title/authors/categories/cover/ISBN/description/page/year/language/price/purchase URL. |
| AC-IND-009 | P1 | Provider timeout trả result `available=false` trong timeout budget và core health vẫn xanh. |
| AC-IND-010 | P1 | Payload upstream không nhận diện được trả result có giới hạn, không lưu dữ liệu rác. |
| AC-IND-011 | P0 | Recommendation rule-based hoạt động khi Bookstore integration tắt, không gọi provider ngoài và không yêu cầu model/ML service. |

## 22. Coverage tự động tối thiểu

### Unit tests

- Domain invariant cho User login availability.
- Onboarding state transition/timestamp idempotency và giới hạn preference.
- Follow self.
- Library transition và page bounds.
- Reading session time/pages/duration và correction forward-only.
- Active reading state machine, elapsed loại pause, minimum duration/page bounds và single-active invariant.
- Reading goal enum/target/date/overlap, derived progress và completion idempotency.
- Reading note quote/content/page/tag invariants.
- Reading Insights range/offset, local-day grouping, streak, comparison và forecast.
- Review rating.
- Club owner role.
- Challenge period, derived high-water progress và completion idempotency.
- Refresh token revoke/rotation.
- Notification mark read idempotent.
- Conversation chuẩn hóa participant, direct message content bound và read marker high-water.

### Integration tests

- Auth register/login/refresh/logout.
- Onboarding owner/auth, draft full-replace atomic, catalog validation, complete/skip
  transition/idempotency, privacy và migration mapping.
- Role policy cho admin route.
- Catalog query/pagination.
- External provider search/detail mapping và admin import mới/link ISBN/retry idempotent.
- Recommendation auth, reason mapping, exclusion library, ranking/tie-break,
  cold-start, privacy source và freshness sau mutation.
- Library ownership.
- Focus Reading auth/ownership, unique active concurrency, idempotent pause/resume, atomic finish/cancel, recovery, goal/challenge synchronization và note privacy.
- Completed reading-session correction ownership, projection refresh và high-water semantics.
- Reading goal status synchronization/filter, completion notification và ownership.
- Reading note CRUD/filter/search/owner isolation.
- Reading Insights auth, range validation, UTC+7 calendar, reports, comparison, forecasts, completion sync và owner isolation.
- Review unique/like/comment.
- Feed filter validation, nguồn/timestamp sự kiện đọc, privacy activity, private-club isolation, paging/order ổn định và không lộ note.
- Club membership/post permission.
- Reading sprint lifecycle, permission, participant idempotency, progress, leaderboard, timeline, milestone/response, reminder deduplication và private-club isolation.
- Challenge detail published/draft, atomic join/rollback, concurrent duplicate join 409, serialized join-vs-unpublish/delete, physical-participation guard, cancellable lock acquisition, leave không post-read và nonparticipant leave, progress từ sách hoàn tất trước/sau join, deterministic stale-low high-water, repair `CompletedAt`, mutation-time completion và notification dedupe/unique constraint.
- Notification ownership, category filters, preferences và delivery policy.
- Direct message mutual-follow, normalized conversation concurrency, ownership, cursor history,
  unread/read marker, notification preference, unfollow history, block/mute, SignalR auth và moderation.
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
- Focus timer recovery/ticking/pause-resume/finish-cancel, preselected book, double-submit guard và completed-session correction.
- Review request top-level `/reviews`.
- Admin request path `/admin/books` và `/admin/challenges`.
- Production App deep-link challenge, loading/error/empty/guest CTA, intended login return, principal-scoped cache, join/leave invalidation, reading-mutation challenge invalidation và không có manual-progress request.
- Loading/error/empty, filter URL, pagination và optimistic read/read-all cho notifications.
- Feed URL filter/pagination, page size 10, suggestion follow, privacy-aware rendering và empty CTA `/people`.
- Explore recommendation guest/member states, principal-scoped pagination,
  reason rendering, quick-add `WANT_TO_READ` và mutation invalidation.
- Onboarding register redirect, protected/resumable draft, selection bounds,
  complete/skip/intended redirect, optional quick actions, Dashboard CTA và Settings edit mode.
- Direct message service contract, mutual-follow profile entry point, inbox/thread states,
  send/read invalidation, unread badge và realtime merge không trùng ID.

Không đặt ngưỡng coverage phần trăm giả tạo cho Goal 1. Mọi invariant và authorization branch liệt kê trên phải có test trực tiếp.

## 23. Điều kiện ký nghiệm thu

Goal 1 chỉ hoàn thành khi:

1. AC P0 từ phần 2 đến phần 19 đạt.
2. Không còn drift field/path giữa frontend service, API controller và tài liệu.
3. Unit/integration/frontend test bắt các contract quan trọng ở phần 19.
4. Seed Development hoạt động và Production không seed tài khoản demo.
5. BookSpace chạy độc lập khi Bookstore không tồn tại.
6. README có lệnh chạy, URL, tài khoản dev và hướng xử lý lỗi phổ biến.
7. Kết quả verify được ghi lại bằng lệnh, exit code và HTTP/browser evidence thực tế.
