import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

interface RouteMeta {
  match: RegExp
  title: string
  description: string
}

const defaultDescription =
  'BookSpace là không gian đọc sách, lưu hành trình đọc và kết nối cộng đồng độc lập.'

const routes: RouteMeta[] = [
  { match: /^\/$/, title: 'Hành trình đọc của bạn', description: defaultDescription },
  { match: /^\/explore/, title: 'Khám phá sách', description: 'Tìm sách và gợi ý đọc phù hợp trên BookSpace.' },
  { match: /^\/books\//, title: 'Chi tiết sách', description: 'Thông tin sách, đánh giá và thảo luận từ cộng đồng BookSpace.' },
  { match: /^\/books/, title: 'Catalog sách', description: 'Khám phá catalog sách độc lập của BookSpace.' },
  { match: /^\/authors/, title: 'Tác giả', description: 'Khám phá tác giả và các tác phẩm trên BookSpace.' },
  { match: /^\/categories/, title: 'Thể loại', description: 'Khám phá sách theo chủ đề trên BookSpace.' },
  { match: /^\/people/, title: 'Cộng đồng độc giả', description: 'Tìm và kết nối với những người đọc cùng sở thích.' },
  { match: /^\/users\//, title: 'Hồ sơ độc giả', description: 'Hành trình đọc công khai của một thành viên BookSpace.' },
  { match: /^\/clubs/, title: 'Câu lạc bộ sách', description: 'Cùng đọc và trò chuyện trong các câu lạc bộ BookSpace.' },
  { match: /^\/challenges/, title: 'Thử thách đọc', description: 'Tham gia thử thách và theo dõi tiến độ đọc.' },
  { match: /^\/lists\//, title: 'Bộ sưu tập sách', description: 'Một bộ sưu tập sách được chia sẻ trên BookSpace.' },
  { match: /^\/login/, title: 'Đăng nhập', description: 'Đăng nhập vào hành trình đọc BookSpace của bạn.' },
  { match: /^\/register/, title: 'Tạo tài khoản', description: 'Tạo không gian đọc độc lập của bạn trên BookSpace.' },
  { match: /^\/forgot-password/, title: 'Quên mật khẩu', description: 'Yêu cầu liên kết đặt lại mật khẩu BookSpace.' },
  { match: /^\/reset-password/, title: 'Đặt lại mật khẩu', description: 'Đặt mật khẩu mới cho tài khoản BookSpace.' },
  { match: /^\/dashboard/, title: 'Tổng quan đọc', description: 'Nhịp đọc và mục tiêu gần đây của bạn.' },
  { match: /^\/library/, title: 'Thư viện của tôi', description: 'Quản lý sách muốn đọc, đang đọc và đã đọc.' },
  { match: /^\/lists/, title: 'Bộ sưu tập của tôi', description: 'Tạo và sắp xếp các bộ sưu tập sách cá nhân.' },
  { match: /^\/journal/, title: 'Nhật ký đọc', description: 'Ghi phiên đọc và khôi phục bộ hẹn giờ tập trung.' },
  { match: /^\/goals/, title: 'Mục tiêu đọc', description: 'Theo dõi mục tiêu từ hoạt động đọc thực tế.' },
  { match: /^\/notes/, title: 'Ghi chú sách', description: 'Tìm và quản lý ghi chú đọc riêng tư.' },
  { match: /^\/insights/, title: 'Phân tích đọc', description: 'Xem nhịp đọc, chuỗi ngày và dự báo cá nhân.' },
  { match: /^\/feed/, title: 'Bảng tin cộng đồng', description: 'Hoạt động đọc mới từ cộng đồng bạn theo dõi.' },
  { match: /^\/messages/, title: 'Tin nhắn', description: 'Trò chuyện riêng với những độc giả theo dõi lẫn nhau.' },
  { match: /^\/notifications/, title: 'Thông báo', description: 'Cập nhật liên quan đến hành trình và cộng đồng của bạn.' },
  { match: /^\/settings/, title: 'Cài đặt', description: 'Quản lý hồ sơ, quyền riêng tư, thông báo và giao diện.' },
  { match: /^\/onboarding/, title: 'Sở thích đọc', description: 'Chọn chủ đề và sách tham chiếu cho gợi ý cá nhân.' },
  { match: /^\/admin/, title: 'Quản trị', description: 'Công cụ vận hành và an toàn cộng đồng BookSpace.' },
  { match: /^\/privacy/, title: 'Quyền riêng tư', description: 'Cách BookSpace thu thập, sử dụng và bảo vệ dữ liệu.' },
  { match: /^\/terms/, title: 'Điều khoản sử dụng', description: 'Quy tắc sử dụng BookSpace và trách nhiệm cộng đồng.' },
]

export function RouteMetadata() {
  const { pathname } = useLocation()

  useEffect(() => {
    const meta = routes.find((route) => route.match.test(pathname))
    const pageTitle = meta?.title || 'Không tìm thấy trang'
    const description = meta?.description || defaultDescription
    document.title = `${pageTitle} | BookSpace`
    updateMeta('name', 'description', description)
    updateMeta('property', 'og:title', `${pageTitle} | BookSpace`)
    updateMeta('property', 'og:description', description)
  }, [pathname])

  return null
}

function updateMeta(attribute: 'name' | 'property', value: string, content: string) {
  const element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${value}"]`)
  element?.setAttribute('content', content)
}
