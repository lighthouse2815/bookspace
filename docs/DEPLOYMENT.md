# BookSpace — Phát hành production

Tài liệu này mô tả đường phát hành một instance BookSpace độc lập bằng Docker.
Bookstore không cần tồn tại và mặc định không được gọi.

## 1. Mô hình chạy

```text
Internet -> TLS reverse proxy cung host -> 127.0.0.1:BOOKSPACE_PORT -> web/nginx
                                                               -> /api, /hubs, /health -> BookSpace API -> SQLite volume
                               -> React static assets
```

Trình duyệt chỉ gọi cùng origin qua `/api` và `/hubs`; API không publish port ra host
trong file production. Cổng HTTP của web chỉ bind `127.0.0.1`, vì vậy không thể bỏ qua
TLS từ máy khác. Nginx hỗ trợ WebSocket cho Club Chat và Direct Messages, thêm security
headers và cache immutable cho asset đã hash.

SQLite phù hợp cho một instance API. Không tăng `replicas` khi chưa chuyển sang một
provider database production có migration và chiến lược concurrency riêng.

## 2. Chuẩn bị cấu hình

```powershell
cd T:\bookspace
Copy-Item .env.production.example .env.production
```

Sửa `.env.production`:

- `BOOKSPACE_PUBLIC_ORIGIN`: origin HTTPS public, ví dụ `https://bookspace.example.vn`.
- `BOOKSPACE_JWT_SECRET`: secret ngẫu nhiên riêng cho BookSpace, tối thiểu 32 byte.
- `BOOKSPACE_OPERATOR_NAME` và `BOOKSPACE_SUPPORT_EMAIL`: tên pháp lý/đơn vị vận hành
  và email hỗ trợ được hiển thị trên trang điều khoản.
- SMTP host/TLS/credential/from address thật. Production luôn dùng `Smtp`; không log
  link đặt lại mật khẩu.
- `BOOKSPACE_PORT`: port loopback trên host mà TLS reverse proxy chuyển tiếp tới, mặc
  định `8080`.
- `BOOKSPACE_DATA_VOLUME`: tên volume dữ liệu đang hoạt động. Giữ giá trị ổn định qua
  các lần nâng cấp; chỉ đổi sang volume mới sau khi hoàn tất restore drill.
- `BOOKSPACE_DOCKER_SUBNET`: subnet riêng bắt buộc cho trusted proxy; đổi khi giá trị
  mẫu xung đột với pool Docker hoặc mạng nội bộ hiện có. Prefix phải từ `/16` đến
  `/29` để còn đủ địa chỉ cho gateway, API và web.

Có thể tạo JWT secret trong PowerShell:

```powershell
$secretBytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($secretBytes)
[Convert]::ToBase64String($secretBytes)
```

Không commit `.env.production`. Script kiểm tra không in secret ra màn hình:

```powershell
.\scripts\validate-production.ps1 -EnvironmentFile .env.production
```

## 3. Build và khởi động

```powershell
docker compose `
  --env-file .env.production `
  --file docker-compose.production.yml `
  up --detach --build
```

API chạy migration EF trước khi nhận traffic. Production không seed tài khoản demo.
Web chỉ khởi động sau khi health check API thành công.

Xác minh trên host trước khi mở traffic:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/health
Invoke-WebRequest http://127.0.0.1:8080/ -UseBasicParsing
docker compose --env-file .env.production -f docker-compose.production.yml ps
```

Sau đó xác minh qua domain HTTPS: trang chủ, đăng ký/đăng nhập, đặt lại mật khẩu,
REST `/api`, Club Chat và Direct Messages realtime. OpenAPI chỉ được expose ở
Development.

## 4. TLS và reverse proxy ngoài

Terminate TLS ở reverse proxy/load balancer chạy trên cùng host, rồi chuyển tiếp tới
`http://127.0.0.1:BOOKSPACE_PORT`. Proxy phải:

- giữ `Host`;
- chuẩn hóa `X-Forwarded-For`: bỏ giá trị trực tiếp không tin cậy hoặc nối địa chỉ
  socket client vào cuối chuỗi, để phần tử ngoài cùng bên phải luôn là IP client mà
  proxy TLS quan sát được;
- gửi `X-Forwarded-Proto: https`;
- cho phép WebSocket và timeout dài cho `/hubs/*`;
- không expose trực tiếp container API.
- ghi access log chỉ bằng method và path đã bỏ query; không ghi `$request`,
  `$request_uri`, `$args`, `Referer`, `Authorization` hoặc `Cookie`. Điều này bắt buộc
  vì SignalR có thể truyền access token trong query và link đặt lại mật khẩu chứa token.
- không ghi request-bearing error log ở proxy. Nginx nội bộ tắt error log theo request;
  dùng health check, status access log và correlation ID để điều tra upstream. Proxy TLS
  ngoài cũng phải dùng cơ chế sanitize tương đương cho cả access/error log.

Nginx nội bộ là trusted proxy duy nhất trong subnet riêng khai báo bởi
`BOOKSPACE_ForwardedHeaders__KnownNetworks__0`. Nginx chuyển nguyên chuỗi
`X-Forwarded-For` đã được proxy TLS chuẩn hóa, không nối thêm IP của proxy TLS; API đọc
đúng một hop ngoài cùng bên phải để chia bucket login/reset theo client. Không mở rộng
CIDR trusted này tới mạng không tin cậy. Nếu đặt CDN hoặc load balancer khác trước proxy
TLS, proxy TLS phải tự xác thực dải IP của hạ tầng đó và tiếp tục chuẩn hóa header; không
được chuyển nguyên header tùy ý do client Internet gửi lên.

File production cố ý không hỗ trợ publish HTTP origin ra mọi interface. Nếu reverse
proxy chạy trong container hoặc trên host khác, hãy nối nó vào một Docker network riêng
và giữ web không public thay vì đổi bind thành `0.0.0.0`.

## 5. Backup, nâng cấp và rollback

Volume dữ liệu có tên ổn định trong `BOOKSPACE_DATA_VOLUME`. Trước mỗi release có
migration, dừng cả traffic lẫn API trong cửa sổ bảo trì rồi sao lưu đúng volume mà
Compose đã resolve:

Volume này đồng thời giữ Data Protection key trong `dataprotection-keys/`. Không
xóa thư mục đó giữa các lần recreate container; giới hạn quyền truy cập volume và
dùng mã hóa đĩa ở host vì key được lưu cùng dữ liệu ứng dụng.

```powershell
New-Item -ItemType Directory -Force .\backups | Out-Null
$composeArgs = @('--env-file', '.env.production', '-f', 'docker-compose.production.yml')
$rendered = docker compose @composeArgs config --format json | ConvertFrom-Json
$dataVolume = $rendered.volumes.'bookspace-production-data'.name
if ([string]::IsNullOrWhiteSpace($dataVolume)) { throw 'Khong xac dinh duoc data volume.' }
$backupFile = "bookspace-data-$((Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')).tgz"
$backupDir = (Resolve-Path .\backups).Path

docker compose @composeArgs stop web api
docker run --rm `
  -v "${dataVolume}:/data:ro" `
  -v "${backupDir}:/backup" `
  alpine:3.22 `
  sh -c "tar czf /backup/$backupFile -C /data ."
if ($LASTEXITCODE -ne 0) { throw 'Backup that bai; giu service dung de dieu tra.' }
docker run --rm -v "${backupDir}:/backup:ro" alpine:3.22 tar tzf "/backup/$backupFile"
if ($LASTEXITCODE -ne 0) { throw 'Archive backup khong doc duoc.' }
docker compose @composeArgs up -d
```

Giữ backup cùng image tag/commit đã phát hành. Rollback code không tự rollback schema.
Khôi phục luôn vào volume mới, không ghi đè volume đang hoạt động:

```powershell
$composeArgs = @('--env-file', '.env.production', '-f', 'docker-compose.production.yml')
$rendered = docker compose @composeArgs config --format json | ConvertFrom-Json
$currentVolume = $rendered.volumes.'bookspace-production-data'.name
$backupDir = (Resolve-Path .\backups).Path
$backupFile = 'bookspace-data-YYYYMMDDTHHMMSSZ.tgz' # thay bằng file đã kiểm tra
$restoreVolume = "bookspace-production-restore-$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"

$entries = docker run --rm -v "${backupDir}:/backup:ro" alpine:3.22 tar tzf "/backup/$backupFile"
if ($LASTEXITCODE -ne 0 -or -not $entries) { throw 'Archive backup rong hoac khong doc duoc.' }
if ($entries | Where-Object { $_ -match '(^/|(^|/)\.\.(/|$))' }) {
    throw 'Archive backup co path khong an toan.'
}
docker volume inspect $restoreVolume *> $null
if ($LASTEXITCODE -eq 0) { throw "Restore volume da ton tai: $restoreVolume" }

docker compose @composeArgs stop web api
docker volume create `
  --label com.docker.compose.project=bookspace-production `
  --label com.docker.compose.volume=bookspace-production-data `
  $restoreVolume
docker run --rm `
  -e "BACKUP_FILE=$backupFile" `
  -v "${restoreVolume}:/data" `
  -v "${backupDir}:/backup:ro" `
  alpine:3.22 `
  sh -c 'if find /data -mindepth 1 -maxdepth 1 -print -quit | grep -q .; then exit 10; fi; tar xzf /backup/$BACKUP_FILE -C /data'
if ($LASTEXITCODE -ne 0) { throw 'Restore that bai; volume cu chua bi thay doi.' }

$previousOverride = $env:BOOKSPACE_DATA_VOLUME
$env:BOOKSPACE_DATA_VOLUME = $restoreVolume
docker compose @composeArgs up -d --force-recreate
```

Xác minh `docker compose ... ps`, `/health`, đăng nhập và dữ liệu nghiệp vụ qua public
HTTPS origin. Thư mục `dataprotection-keys/` phải có trong volume mới. Nếu đạt, cập nhật
`BOOKSPACE_DATA_VOLUME=$restoreVolume` trong `.env.production`, bỏ biến override của
PowerShell và giữ volume cũ hết thời gian retention.

Nếu bất kỳ check nào thất bại, rollback không xóa volume nào:

```powershell
docker compose @composeArgs down
$env:BOOKSPACE_DATA_VOLUME = $currentVolume
docker compose @composeArgs up -d --force-recreate
$env:BOOKSPACE_DATA_VOLUME = $previousOverride
```

Chỉ xóa volume cũ bằng thao tác riêng sau khi backup, restore drill và thời gian retention
đều đã được xác nhận; quy trình trên không tự xóa dữ liệu.

## 6. Quan sát vận hành

- `/health` chỉ kiểm tra process và database BookSpace; provider ngoài không làm core
  health đỏ.
- Mỗi response có `X-Correlation-ID`; log request có route, status, thời gian và user ID
  nhưng không ghi token/body bí mật.
- Theo dõi restart count, dung lượng volume, latency/error 5xx, 401/429 bất thường,
  hàng đợi moderation và lỗi gửi SMTP.

## 7. Gate trước khi phát hành

```powershell
.\scripts\verify.ps1
.\scripts\validate-production.ps1 -EnvironmentFile .env.production
docker compose --env-file .env.production -f docker-compose.production.yml build
```

Sau khi container chạy, phải hoàn tất `scripts\smoke-api.ps1` với API nội bộ phù hợp
hoặc cùng bộ assertion qua `/api`, rồi QA browser ở 360px, 768px và 1280px. Không coi
release hoàn tất nếu Docker build, health, SMTP reset-password, realtime hoặc smoke
chưa có bằng chứng thực tế.

Kiểm tra thêm từ public origin: `/health` trả `200 Healthy`, API không publish port
trực tiếp, `/openapi/*` trả `404`, và response HTML có CSP, `nosniff`, frame/referrer/
permissions policy như cấu hình Nginx.
