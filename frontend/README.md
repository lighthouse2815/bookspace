# BookSpace Web

React frontend độc lập cho BookSpace, nền tảng quản lý hành trình đọc và cộng đồng sách. Ứng dụng không cần Bookstore để hoạt động. Tích hợp nhà cung cấp bên ngoài chỉ xuất hiện khi backend trả về `externalOffer` hợp lệ cho một cuốn sách.

## Công nghệ

- React 19 và TypeScript
- Vite 8
- Tailwind CSS 4
- React Router 7
- TanStack Query 5
- Axios
- Phosphor Icons
- Manrope Variable được đóng gói cục bộ

## Chạy local

Yêu cầu Node.js 22 trở lên.

```powershell
cd T:\bookspace\frontend
Copy-Item .env.example .env
npm install
npm run dev
```

Frontend chạy tại `http://localhost:5173`.

Biến môi trường mặc định:

```properties
VITE_API_BASE_URL=http://localhost:5080/api
```

Backend phải trả response theo envelope:

```json
{
  "success": true,
  "message": "Thành công",
  "data": {},
  "code": null,
  "timestamp": "2026-07-29T10:00:00Z"
}
```

Kết quả phân trang bên trong `data`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

Frontend cố ý không nhận dữ liệu trực tiếp ngoài envelope để lỗi lệch API được phát hiện sớm.

## Các trang chính

Trang công khai:

- `/`: landing page
- `/explore`: khám phá sách, chủ đề, câu lạc bộ và thử thách
- `/books`: catalog, tìm kiếm, lọc và phân trang
- `/books/:id`: chi tiết sách, thư viện, đánh giá và bình luận
- `/users/:id`: hồ sơ người đọc và theo dõi
- `/clubs`, `/clubs/:id`: danh sách và thảo luận câu lạc bộ
- `/clubs/:clubId/sprints/:sprintId`: tiến độ, leaderboard, timeline và cột mốc của đợt đọc
- `/challenges`, `/challenges/:id`: danh sách và chi tiết thử thách đọc
- `/login`, `/register`: xác thực

Trang yêu cầu đăng nhập:

- `/dashboard`: thống kê và tiến độ
- `/library`: kệ sách cá nhân
- `/journal`: phiên đọc và nhật ký
- `/feed`: bảng tin cộng đồng
- `/notifications`: thông báo
- `/settings`: hồ sơ và giao diện
- `/clubs/new`: tạo câu lạc bộ
- `/clubs/invitations`: chấp nhận hoặc từ chối lời mời

Trang `/clubs/:id` hiển thị thêm roster và khu quản lý theo quyền: owner có thể
sửa club/đổi vai trò, owner hoặc moderator có thể mời/thu hồi, loại member phù
hợp, chọn hoặc gỡ sách đọc chung và điều hành đợt đọc chung. Thành viên câu lạc
bộ có thể tham gia sprint, cập nhật tiến độ, xem leaderboard/timeline và phản
hồi cột mốc; UI chỉ hiện mutation mà quyền trong response cho phép.

Trang yêu cầu vai trò `ADMIN`:

- `/admin/books`: quản trị catalog
- `/admin/challenges`: quản trị thử thách

## Kiểm tra chất lượng

```powershell
npm run typecheck
npm test
npm run lint
npm run build
```

## Docker

Build ảnh với API URL được đóng vào bundle:

```powershell
docker build `
  --build-arg VITE_API_BASE_URL=http://localhost:5080/api `
  -t bookspace-web .
```

Chạy container:

```powershell
docker run --rm -p 5173:80 bookspace-web
```

Nginx phục vụ SPA với fallback về `index.html`, vì vậy truy cập trực tiếp các route của React Router vẫn hoạt động.

## Cấu trúc mã nguồn

```text
src/
├── app/          TanStack Query và cấu hình ứng dụng
├── components/   UI, layout, catalog, community và admin
├── contexts/     auth, theme và toast
├── hooks/        query và mutation theo domain
├── lib/          API client, refresh token và formatter
├── pages/        màn hình theo feature: auth, catalog, reading, community, clubs, challenges, account, admin
├── services/     endpoint tách theo domain
└── types/        API envelope và domain model
```

Khi backend thay đổi một endpoint nhỏ, ưu tiên sửa module tương ứng trong `src/services` thay vì đưa logic gọi API vào component.
