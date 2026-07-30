# BookSpace — Đặc tả sản phẩm

> Phiên bản: 1.0<br>
> Phạm vi: Goal 1 — sản phẩm web độc lập, chạy được end-to-end<br>
> Trạng thái: Hợp đồng nguồn sự thật cho backend, frontend và kiểm thử

## 1. Tuyên bố sản phẩm

BookSpace là nền tảng quản lý hành trình đọc và cộng đồng dành cho người đọc sách. Người dùng có thể khám phá sách, xây dựng thư viện cá nhân, ghi nhận tiến độ đọc, đặt mục tiêu đọc đo được, lưu ghi chú riêng tư, viết đánh giá, theo dõi người đọc khác, tham gia câu lạc bộ và thử thách đọc.

BookSpace phải có thể đăng ký, đăng nhập, sử dụng và vận hành đầy đủ khi toàn bộ hệ thống Bookstore không tồn tại hoặc đang ngừng hoạt động.

Bookstore là một tích hợp tùy chọn trong tương lai: nó có thể cung cấp catalog/giá/đường dẫn mua hoặc gửi sự kiện mua hàng. Tích hợp này không được trở thành điều kiện để BookSpace khởi động hay hoàn thành bất kỳ luồng lõi nào.

## 2. Mục tiêu Goal 1

Goal 1 chỉ được coi là hoàn thành khi có một vertical slice đầy đủ:

1. Backend ASP.NET Core theo Clean Architecture, có cơ sở dữ liệu riêng.
2. Frontend React gọi API thật; không dùng dữ liệu giả cho luồng chính.
3. Đăng ký, đăng nhập, refresh token, đăng xuất và phân quyền hoạt động.
4. Catalog sách, tác giả, thể loại có trang công khai và CRUD dành cho quản trị viên.
5. Thành viên quản lý thư viện và tiến độ đọc của riêng mình.
6. Thành viên ghi nhận phiên đọc, đặt và theo dõi mục tiêu đọc cá nhân.
7. Thành viên lưu, tìm kiếm, cập nhật và xóa ghi chú đọc riêng tư theo sách/trang/tag.
8. Thành viên viết đánh giá, bình luận và thích đánh giá.
9. Thành viên theo dõi nhau và xem feed cộng đồng.
10. Thành viên tham gia câu lạc bộ, đăng bài và bình luận.
11. Thành viên tham gia thử thách và xem tiến độ.
12. Thông báo và dashboard cá nhân phản ánh dữ liệu thật.
13. Seed dữ liệu phát triển cho phép kiểm tra cả vai trò `ADMIN` và `USER`.
14. Build, test, seed và chạy local có hướng dẫn tái lập.

## 3. Nguyên tắc không thương lượng

- BookSpace sở hữu database, tài khoản, token, catalog và dữ liệu cộng đồng của mình.
- Không đọc hoặc ghi trực tiếp database của Bookstore.
- Không chia sẻ `JWT_SECRET`, refresh token, cookie, password hash hoặc khóa riêng với Bookstore.
- Tất cả khóa chính nghiệp vụ là UUID.
- Thời gian API dùng UTC, định dạng ISO 8601.
- Xóa dữ liệu nghiệp vụ là soft delete khi còn quan hệ lịch sử; không làm mất dữ liệu tham chiếu.
- API trả cùng một envelope thành công/lỗi.
- Mọi danh sách có khả năng tăng trưởng phải phân trang.
- Mọi thay đổi quyền sở hữu phải được kiểm tra ở backend; ẩn nút ở frontend không phải là biện pháp phân quyền.
- Bookstore integration mặc định tắt và không thuộc điều kiện hoàn thành Goal 1.

## 4. Người dùng và vai trò

### 4.1 Khách chưa đăng nhập

- Xem trang chủ, catalog, chi tiết sách, tác giả, thể loại.
- Xem đánh giá công khai, câu lạc bộ công khai và thử thách đã xuất bản.
- Tìm kiếm sách.
- Đăng ký và đăng nhập.
- Không thể tạo nội dung hoặc xem dữ liệu cá nhân.

### 4.2 Thành viên — `USER`

Có toàn bộ quyền của khách và:

- Cập nhật hồ sơ của chính mình.
- Theo dõi hoặc bỏ theo dõi thành viên khác.
- Quản lý thư viện, tiến độ, phiên đọc và mục tiêu đọc của mình.
- Tạo/sửa/xóa ghi chú đọc riêng tư của mình; tìm lại theo sách, tag hoặc từ khóa.
- Tạo/sửa/xóa đánh giá, lượt thích và bình luận của mình.
- Xem feed từ những người đang theo dõi.
- Tạo câu lạc bộ, tham gia/rời câu lạc bộ, đăng bài và bình luận theo quyền trong câu lạc bộ.
- Tham gia/rời thử thách và xem tiến độ của mình.
- Đọc, đánh dấu đã đọc thông báo của mình.

### 4.3 Quản trị viên — `ADMIN`

Có quyền thành viên và:

- CRUD sách, tác giả, thể loại.
- CRUD và xuất bản thử thách.
- Quản lý nội dung catalog bị soft delete.
- Xem dashboard quản trị tối thiểu.

Goal 1 không thêm vai trò nhân viên, nhà cung cấp hoặc quản trị hệ thống nhiều cấp. Quyền nội bộ câu lạc bộ dùng `ClubMemberRole`, không phải vai trò hệ thống.

## 5. Bounded context và năng lực

| Bounded context | Năng lực lõi | Phụ thuộc |
|---|---|---|
| Identity & Profile | tài khoản, phiên đăng nhập, hồ sơ, theo dõi | không |
| Catalog | sách, tác giả, thể loại, tìm kiếm | không |
| Reading | thư viện, trạng thái đọc, tiến độ, phiên đọc, mục tiêu đọc, ghi chú riêng tư | Identity, Catalog |
| Community | review, like, comment, feed | Identity, Catalog, Reading |
| Clubs | câu lạc bộ, thành viên, bài đăng, bình luận, đợt đọc chung và cột mốc thảo luận | Identity, Catalog, Notifications |
| Challenges | thử thách, tham gia, tiến độ | Identity, Reading |
| Notifications | thông báo trong ứng dụng | các sự kiện nội bộ |
| Integration | provider catalog/offer, webhook mua hàng | tùy chọn, bị cô lập |

Chi tiết entity, invariant và quan hệ nằm trong [DOMAIN_MODEL.md](./DOMAIN_MODEL.md).

## 6. Các use case bắt buộc

### UC-01 — Tạo tài khoản

Khách nhập tên hiển thị, email và mật khẩu. Hệ thống chuẩn hóa email, kiểm tra trùng, hash mật khẩu và tạo `User`, sau đó trả phiên đăng nhập. Tài khoản mặc định có role `USER`.

### UC-02 — Đăng nhập và duy trì phiên

Thành viên đăng nhập bằng email/mật khẩu để nhận access token ngắn hạn và refresh token có thể thu hồi. Refresh token được xoay vòng; logout thu hồi token hiện tại.

### UC-03 — Khám phá sách

Người dùng tìm theo từ khóa và lọc theo tác giả/thể loại. Kết quả có phân trang. Chi tiết sách gồm tác giả, thể loại, thống kê rating và trạng thái thư viện của người đang đăng nhập nếu có.

### UC-04 — Quản lý thư viện

Thành viên thêm sách với một trong ba trạng thái:

- `WANT_TO_READ`
- `READING`
- `READ`

Mỗi người chỉ có một mục thư viện cho mỗi sách. Cập nhật tiến độ không được vượt quá số trang của sách. Hoàn tất sách đặt trạng thái `READ` và thời điểm hoàn thành.

### UC-05 — Ghi phiên đọc

Thành viên ghi thời điểm bắt đầu/kết thúc và trang đầu/cuối. Phiên đọc phải có thời lượng dương và trang cuối không nhỏ hơn trang đầu. Phiên đọc có thể cập nhật tiến độ thư viện nhưng không được làm tiến độ lùi.

### UC-05A — Đặt mục tiêu đọc cá nhân

Thành viên tạo mục tiêu riêng với metric `BOOKS`, `PAGES` hoặc `MINUTES`; period `WEEK`, `MONTH`, `YEAR` hoặc `CUSTOM`; giá trị enum ngoài tập này bị từ chối. Target và khoảng thời gian do client gửi. `period` là nhãn phân loại, không tự sinh hoặc tự điều chỉnh ngày bắt đầu/kết thúc. Server chỉ nhận target từ 1 đến 1.000.000, `endDate` ở tương lai, sau `startDate` và khoảng kéo dài không quá 366 ngày. Một thành viên không thể có hai mục tiêu còn hoạt động cùng metric bị chồng thời gian.

Tiến độ không được client ghi trực tiếp: server tính `BOOKS` từ library item `READ` có `finishedAt` trong khoảng mục tiêu; `PAGES` và `MINUTES` từ `ReadingSession` có `startedAt` trong khoảng mục tiêu. Khi current value đạt target, mục tiêu được đánh dấu `COMPLETED` đúng một lần và tạo một thông báo `SYSTEM` liên kết `/goals`. Mục tiêu đã hoàn thành hoặc hết hạn không thể cập nhật; xóa là soft delete và chỉ owner thấy dữ liệu.

### UC-05B — Lưu ghi chú đọc riêng tư

Thành viên tạo ghi chú cho một book tồn tại; book không bắt buộc phải nằm trong library. Ghi chú phải có ít nhất quote hoặc content. `pageNumber` là tùy chọn nhưng nếu có phải trong khoảng 1..`pageCount` của book. Quote tối đa 500 ký tự, content tối đa 5.000 ký tự. Tag được trim, bỏ trùng không phân biệt hoa/thường, tối đa 10 tag, mỗi tag tối đa 30 ký tự và không chứa `|`.

Mọi ghi chú chỉ hiển thị cho owner; không đi vào review, feed, club hay notification. Owner có thể lọc danh sách theo `bookId`, tag chính xác không phân biệt hoa/thường hoặc từ khóa trong quote/content/tag. Khi cập nhật, book của ghi chú không đổi; xóa là soft delete.

### UC-05C — Phân tích nhịp đọc và dự báo

Thành viên xem Reading Insights được suy ra từ dữ liệu BookSpace của chính mình, gồm tổng phiên/trang/phút, ngày hoạt động, trung bình theo ngày hoạt động, chuỗi ngày đọc hiện tại và dài nhất, heatmap rolling 30/90/365 ngày hoặc một năm lịch, báo cáo tuần/tháng, so sánh hai giai đoạn liền kề, dự báo hoàn thành sách đang đọc và dự báo mục tiêu đang hoạt động.

Insights không là entity và không lưu snapshot aggregate. `ReadingSession.StartedAt` quyết định ngày hoạt động; toàn bộ phiên xuyên nửa đêm thuộc ngày bắt đầu. API nhận `utcOffsetMinutes` từ -840 đến 840, trong đó `420` nghĩa là UTC+7, rồi chuyển biên ngày local thành khoảng UTC nửa mở `[startUtc, endUtc)`. Mọi calendar phải điền cả ngày không có hoạt động. Chuỗi hiện tại bắt đầu từ hôm nay nếu có đọc, nếu không thì được phép bắt đầu từ hôm qua; khoảng trống trước đó làm chuỗi hiện tại bằng 0.

Dự báo sách chỉ dùng library item `READING` và tối đa 30 ngày phiên đọc gần đây của đúng sách; tốc độ bằng tổng trang chia số ngày lịch từ hoạt động đầu tiên trong cửa sổ đến hôm nay. Dự báo mục tiêu chỉ dùng goal `ACTIVE` và cùng nguồn tiến độ với Reading Goal. Goal chưa bắt đầu hoặc thiếu tốc độ trả ETA `null` thay vì số vô hạn. Việc đọc Insights phải đồng bộ goal vừa đạt target thành `COMPLETED` đúng một lần và không tạo notification trùng.

### UC-06 — Đánh giá và tương tác

Thành viên chỉ có một review trên một cuốn sách, rating từ 1 đến 5. Chủ sở hữu có thể sửa/xóa review. Thành viên khác có thể like/unlike và bình luận; một người chỉ tạo một lượt like cho một review.

### UC-07 — Theo dõi và feed

Thành viên có thể theo dõi người khác, không thể tự theo dõi hoặc tạo quan hệ trùng. Feed hiển thị hoạt động mới nhất từ chính mình và những tài khoản đang theo dõi, gồm hoàn tất sách, tạo review và hoạt động công khai liên quan.

### UC-08 — Câu lạc bộ

Thành viên tạo câu lạc bộ và trở thành `OWNER`. Người dùng có thể tham gia trực tiếp câu lạc bộ công khai; câu lạc bộ riêng tư chỉ nhận thành viên qua lời mời gửi tới một tài khoản BookSpace đã tồn tại. Lời mời có vòng đời `PENDING`, `ACCEPTED`, `DECLINED`, `REVOKED`, `EXPIRED`, chỉ người được mời được chấp nhận/từ chối và thao tác lặp không tạo membership hoặc notification trùng.

`OWNER` được sửa thông tin câu lạc bộ, đổi vai trò `MEMBER`/`MODERATOR` và loại mọi thành viên không phải owner. `MODERATOR` được mời thành viên, thu hồi lời mời, loại `MEMBER`, quản lý bài viết/bình luận và chọn sách đang đọc chung, nhưng không thể tác động owner hoặc moderator khác. Owner không thể rời câu lạc bộ; thành viên bị loại/rời không thể tạo nội dung mới. Sách đọc chung luôn tham chiếu catalog nội bộ và có thể để trống; Bookstore chỉ bổ sung offer/link mua tùy chọn.

### UC-08A — Đợt đọc chung trong câu lạc bộ

`OWNER` hoặc `MODERATOR` tạo đợt đọc chung cho một sách thuộc catalog nội bộ, đặt tên, khoảng thời gian, loại mục tiêu `PAGES` hoặc `CHAPTERS` và giá trị mục tiêu. Mục tiêu `PAGES` không vượt số trang sách; `CHAPTERS` tối đa 500; `EndsAt` phải ở tương lai và sau `StartsAt`. Người quản lý chỉ được sửa sprint khi trạng thái suy ra là `PLANNED`, trước `StartsAt`; khi đã bắt đầu, luật đọc không thể đổi. Hoàn tất/hủy lặp lại trả cùng trạng thái nhưng mọi mutation nội dung, tiến độ, cột mốc và nhắc đọc sau khi kết thúc đều bị từ chối.

Chỉ thành viên câu lạc bộ nhìn thấy đợt đọc của câu lạc bộ riêng tư. Thành viên có thể tham gia, rời rồi tham gia lại mà không tạo bản ghi participant thứ hai. Join, rejoin và leave có tính idempotent; người chưa tham gia không được ghi tiến độ, phản hồi cột mốc hoặc xuất hiện trên bảng xếp hạng.

Khi user rời club hoặc bị manager loại, mọi participant còn active của user đó
trên sprint chưa explicit `COMPLETED`/`CANCELLED` được đặt `LeftAt` trong cùng
transaction. Timeline cũ vẫn được giữ nhưng user biến mất khỏi leaderboard.

Tiến độ là giá trị tuyệt đối, không âm, không vượt mục tiêu và không được giảm. Gửi lại đúng giá trị hiện tại không tạo activity hoặc notification trùng. Server tính phần trăm theo mục tiêu, chặn trong `0..100`, rồi xếp leaderboard theo tiến độ giảm dần và tie-break ổn định. Timeline chỉ hiển thị activity của sprint cho thành viên có quyền xem và không làm lộ activity của câu lạc bộ riêng tư.

Người quản lý tạo, sửa và soft-delete cột mốc thảo luận khi sprint đang `PLANNED` hoặc `ACTIVE`. Participant còn hoạt động có thể đăng nhiều phản hồi dạng thread; response không có thao tác sửa. Author của response hoặc manager được soft-delete response, và DTO trả `canDelete` theo principal. Cột mốc hoặc phản hồi đã soft-delete không xuất hiện trong dữ liệu active.

Người quản lý có thể gửi lời nhắc đọc tối đa một lần trong mỗi ngày UTC cho cùng sprint. Lần gọi lại trong ngày là idempotent và không tạo notification thứ hai cho cùng người nhận. Lịch sử hỗ trợ lọc theo trạng thái để thành viên xem lại các đợt đã hoàn tất/hủy.

Sách, membership, tiến độ, leaderboard, timeline và notification của đợt đọc đều nằm trong BookSpace. Bookstore không tham gia xác thực hoặc vòng đời sprint; tắt integration không làm mất bất kỳ chức năng nào.

### UC-09 — Thử thách đọc

Quản trị viên tạo bản nháp, chỉnh sửa và xuất bản thử thách. Thành viên chỉ tham gia thử thách `PUBLISHED` còn hạn. Server tự suy ra tiến độ từ số `LibraryItem` của thành viên có shelf `READ` và `FinishedAt` trong khoảng UTC đóng `[StartDate, EndDate]`, cùng quy tắc với Reading Goal metric `BOOKS`; thời điểm tham gia không thu hẹp cửa sổ. Client không có API nhập tiến độ. Mutation hoàn tất sách đồng bộ challenge trong cùng transaction; participation dùng atomic high-water mark nên tiến độ không giảm và completion event có khóa chống trùng ở database.

### UC-10 — Thông báo

Các sự kiện follow, like, comment, club và challenge tạo thông báo cho đúng người nhận, trừ khi tác nhân cũng là người nhận. Thành viên chỉ đọc/đánh dấu thông báo của chính mình.

### UC-11 — Quản trị catalog

Quản trị viên tạo/sửa/soft-delete sách, tác giả, thể loại. Không cho xóa mềm tác giả/thể loại đang là liên kết duy nhất cần thiết của một sách đang hoạt động nếu request không đồng thời gỡ/chuyển liên kết hợp lệ.

### UC-12 — Tích hợp nhà cung cấp tùy chọn

Khi provider được bật, người dùng có thể tìm metadata/offer ngoài hệ thống. Lỗi provider phải trả trạng thái tích hợp có kiểm soát và không làm hỏng catalog nội bộ, thư viện hay đăng nhập.

## 7. Bản đồ trang frontend

Tên route là hợp đồng điều hướng Goal 1; thay đổi cần đồng bộ test và tài liệu.

### Công khai

| Route | Trang | Nội dung tối thiểu |
|---|---|---|
| `/` | Home | giới thiệu, sách nổi bật, hoạt động cộng đồng |
| `/explore` | Explore | tìm kiếm/lọc sách có phân trang |
| `/books` | Books | catalog đầy đủ có phân trang |
| `/books/:id` | Book detail | metadata, tác giả, thể loại, review |
| `/challenges` | Challenges | thử thách đã xuất bản |
| `/challenges/:id` | Challenge detail | chi tiết, tiến độ tự động và join/leave |
| `/clubs` | Clubs | danh sách câu lạc bộ công khai |
| `/clubs/:id` | Club detail | thông tin, thành viên, bài đăng và đợt đọc chung theo quyền |
| `/clubs/:clubId/sprints/:sprintId` | Reading sprint | tiến độ, leaderboard, timeline, quản trị và cột mốc theo quyền |
| `/users/:id` | Public profile | hồ sơ, thống kê công khai |
| `/login` | Login | đăng nhập |
| `/register` | Register | đăng ký |

### Cần đăng nhập

| Route | Trang | Nội dung tối thiểu |
|---|---|---|
| `/feed` | Feed | hoạt động của mạng theo dõi |
| `/library` | My library | lọc theo ba trạng thái |
| `/journal` | Reading journal | tạo và xem các phiên đọc |
| `/goals` | Reading goals | tạo, sửa, xóa và theo dõi tiến độ mục tiêu riêng |
| `/notes` | Reading notes | tạo, sửa, xóa, lọc và tìm ghi chú riêng tư |
| `/insights` | Reading insights | heatmap, streak, báo cáo tuần/tháng, so sánh kỳ và dự báo |
| `/dashboard` | My dashboard | số sách, trang/phút đọc, challenge |
| `/profile` | My profile | hồ sơ hiện tại hoặc chuyển tới `/users/:id` |
| `/settings` | Settings | chỉnh hồ sơ và thiết lập tài khoản |
| `/notifications` | Notifications | danh sách và trạng thái chưa đọc |
| `/clubs/new` | Create club | tạo câu lạc bộ công khai hoặc riêng tư |
| `/clubs/invitations` | Club invitations | lời mời CLB đang chờ và thao tác chấp nhận/từ chối |

### Quản trị

| Route | Trang |
|---|---|
| `/admin/books` | CRUD sách |
| `/admin/challenges` | CRUD/xuất bản thử thách |

Frontend dùng lazy route và protected route cho trang yêu cầu quyền. Code và authentication state là riêng của BookSpace.

## 8. Trạng thái nghiệp vụ chính

### 8.1 Library item

```text
WANT_TO_READ ── bắt đầu đọc ──> READING ── hoàn thành ──> READ
     │                              │                     │
     └──────── đánh dấu đã đọc ─────┴─────────────────────┘

READ ── đọc lại ──> READING
```

Không cho tiến độ âm, vượt số trang hoặc tự động lùi. Người dùng có thể đổi `READ` về `READING` khi đọc lại; lần hoàn thành mới không được làm mất lịch sử `ReadingSession`.

### 8.2 Challenge

```text
DRAFT ── publish ──> PUBLISHED ── hết hạn/đóng ──> ENDED
  │
  └── soft delete
```

Chỉ `DRAFT` được sửa các trường làm thay đổi luật (mục tiêu, thời gian). Bản `PUBLISHED` chỉ sửa nội dung mô tả không làm thay đổi kết quả hoặc kết thúc sớm.

### 8.3 Reading goal

```text
ACTIVE -- currentValue >= targetValue --> COMPLETED
ACTIVE -- now > endDate -------------> EXPIRED
```

`COMPLETED` có `completedAt` chỉ được đặt một lần. `COMPLETED` và `EXPIRED` là trạng thái suy ra từ dữ liệu lưu và thời điểm UTC hiện tại; không có endpoint đổi trạng thái hay ghi tiến độ thủ công. Với danh sách lọc `ACTIVE`, mục tiêu có `endDate` đúng thời điểm hiện tại vẫn còn active.

### 8.4 Membership câu lạc bộ

```text
không tham gia ── join/invitation ──> MEMBER ── leave/remove ──> không tham gia
                                         │
                                         └── owner promote/demote ──> MODERATOR

OWNER ── vai trò bất biến; không thể leave/remove
```

Một câu lạc bộ luôn có đúng một `OWNER` đang hoạt động.

### 8.5 Đợt đọc chung

```text
ACTIVE ── complete ──> COMPLETED
   │
   └──── cancel ─────> CANCELLED
```

Trạng thái được suy ra theo thời điểm thành `PLANNED`, `ACTIVE` hoặc `ENDED`, trừ khi đã được đặt `COMPLETED`/`CANCELLED`. Chỉ `PLANNED` được sửa luật sprint. Gọi lại đúng complete/cancel command trả trạng thái hiện có; không có transition từ trạng thái đã kết thúc về `ACTIVE` hoặc sang trạng thái kết thúc còn lại. Participant có lifecycle active/inactive riêng để leave và rejoin giữ cùng identity cũng như lịch sử.

## 9. Seed phục vụ kiểm thử local

Seed chỉ chạy trong môi trường Development:

| Tài khoản | Mật khẩu | Vai trò | Mục đích |
|---|---|---|---|
| `admin@bookspace.local` | `Admin123!` | `ADMIN` | CRUD catalog/challenge |
| `reader@bookspace.local` | `Reader123!` | `USER` | luồng người đọc |

Seed tối thiểu thêm:

- 6 tác giả, 5 thể loại, 12 sách có số trang và ảnh bìa hợp lệ.
- 3 mục thư viện ở đủ ba trạng thái cho reader.
- 2 phiên đọc, 2 review có bình luận/like.
- 2 hồ sơ người dùng để kiểm tra follow/feed.
- 1 câu lạc bộ công khai có bài đăng.
- 1 challenge `PUBLISHED` đang diễn ra và 1 challenge `DRAFT`.
- Một số thông báo đã đọc/chưa đọc.

Mật khẩu seed là thông tin dev-only, không được dùng hoặc tự động tạo ở Production.

## 10. Ngoài phạm vi Goal 1

- Thanh toán, giỏ hàng, vận chuyển và tồn kho.
- Đọc ebook có DRM hoặc lưu file sách.
- Chat thời gian thực.
- Recommendation bằng machine learning.
- Multi-tenant hoặc nhiều tổ chức.
- Mobile app.
- SSO với Bookstore.
- Đồng bộ đơn hàng thật và webhook production.
- Message broker, event sourcing, CQRS framework hoặc microservice hóa.

Các mục này không được thêm làm điều kiện để luồng MVP chạy.

## 11. Chỉ số sản phẩm ban đầu

Dashboard phải tính được từ dữ liệu nội bộ:

- Số sách theo từng trạng thái thư viện.
- Số sách hoàn thành trong tháng/năm.
- Tổng trang và tổng phút đọc từ phiên đọc.
- Số review đã viết.
- Tiến độ challenge đang tham gia.
- Số follower/following.

Không cần hệ thống analytics ngoài trong Goal 1.

## 12. Definition of Done cấp sản phẩm

- Tất cả tiêu chí P0 trong [ACCEPTANCE_TESTS.md](./ACCEPTANCE_TESTS.md) đạt.
- Backend build/test xanh và khởi động với database mới.
- Frontend build/lint/test xanh và gọi được backend local.
- Không có route lõi dùng mock array hoặc localStorage thay cho API.
- Người dùng seed hoàn thành được các luồng từ UI.
- Tắt toàn bộ biến tích hợp Bookstore vẫn không làm giảm chức năng lõi.
- Không có secret hoặc mật khẩu production được commit.
- API thực tế khớp [API_CONTRACT.md](./API_CONTRACT.md), hoặc tài liệu được cập nhật trong cùng thay đổi.
