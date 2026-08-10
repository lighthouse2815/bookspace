import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'

const effectiveDate = '10/08/2026'
const operatorName = import.meta.env.VITE_OPERATOR_NAME?.trim() || 'đơn vị vận hành BookSpace'
const supportEmail = import.meta.env.VITE_SUPPORT_EMAIL?.trim()

function OperatorContact() {
  return (
    <>
      {operatorName}
      {supportEmail ? (
        <>
          {' '}qua email{' '}
          <a className="font-semibold text-accent-strong hover:underline" href={`mailto:${supportEmail}`}>
            {supportEmail}
          </a>
        </>
      ) : (
        ' qua kênh hỗ trợ do quản trị viên của instance công bố'
      )}
    </>
  )
}

function LegalLayout({
  eyebrow,
  title,
  intro,
  children,
}: {
  eyebrow: string
  title: string
  intro: string
  children: ReactNode
}) {
  return (
    <div className="container-page section-space">
      <div className="grid gap-10 lg:grid-cols-[minmax(0,0.72fr)_minmax(0,1.28fr)] lg:gap-20">
        <header className="lg:sticky lg:top-28 lg:self-start">
          <p className="eyebrow">{eyebrow}</p>
          <h1 className="page-title mt-4">{title}</h1>
          <p className="section-copy mt-5">{intro}</p>
          <p className="mt-5 text-sm text-muted">Có hiệu lực từ {effectiveDate}</p>
          <Link to="/" className="mt-7 inline-flex text-sm font-semibold text-accent-strong hover:underline">
            Về trang chủ
          </Link>
        </header>
        <article className="legal-copy surface p-6 sm:p-9">{children}</article>
      </div>
    </div>
  )
}

export function PrivacyPage() {
  return (
    <LegalLayout
      eyebrow="Dữ liệu của bạn"
      title="Quyền riêng tư"
      intro="BookSpace chỉ dùng dữ liệu cần thiết để vận hành hành trình đọc và cộng đồng; Bookstore không sở hữu hay tự động nhận dữ liệu này."
    >
      <h2>1. Dữ liệu BookSpace lưu</h2>
      <p>
        Tài khoản gồm email, tên hiển thị, mật khẩu đã băm, ảnh đại diện và phần giới thiệu. Sản phẩm còn lưu thư viện, phiên đọc, mục tiêu, ghi chú riêng tư, đánh giá, quan hệ theo dõi, câu lạc bộ, thử thách, thông báo, tin nhắn và lựa chọn quyền riêng tư mà bạn tạo.
      </p>
      <p>
        Log vận hành có thể chứa thời điểm, route, mã trạng thái, thời lượng, correlation ID và ID tài khoản đã xác thực. BookSpace không chủ đích ghi mật khẩu, access token, refresh token hoặc nội dung request nhạy cảm vào log.
      </p>

      <h2>2. Mục đích sử dụng</h2>
      <p>
        Dữ liệu được dùng để xác thực phiên, khôi phục tài khoản, hiển thị thư viện và tiến độ, tính mục tiêu/insights, cung cấp gợi ý theo luật minh bạch, vận hành tính năng cộng đồng, chống lạm dụng và xử lý báo cáo.
      </p>

      <h2>3. Nội dung công khai và riêng tư</h2>
      <p>
        Hồ sơ, review, danh sách công khai và hoạt động bạn chủ động công khai có thể được khách hoặc thành viên khác xem. Ghi chú đọc, sở thích onboarding, token, tin nhắn và dữ liệu được đánh dấu riêng tư chỉ được trả theo quyền ở backend. Block và mute tiếp tục được áp dụng ở server.
      </p>

      <h2>4. Dịch vụ bên ngoài</h2>
      <p>
        BookSpace hoạt động độc lập. Tích hợp Bookstore mặc định tắt; khi quản trị viên bật, ứng dụng chỉ gửi truy vấn catalog cần thiết qua adapter có timeout. Ảnh bìa hoặc avatar từ URL bên ngoài có thể khiến trình duyệt kết nối tới máy chủ chứa ảnh đó. SMTP production chỉ nhận thông tin cần thiết để gửi email đặt lại mật khẩu.
      </p>

      <h2>5. Lưu giữ và bảo vệ</h2>
      <p>
        Dữ liệu có quan hệ lịch sử có thể được soft-delete để giữ tính toàn vẹn, audit kiểm duyệt và chống tạo trùng. Token đặt lại mật khẩu chỉ lưu dạng hash và hết hạn; refresh token cũng không được lưu dạng thô. Người vận hành instance chịu trách nhiệm sao lưu, kiểm soát truy cập, TLS, secret và thời hạn giữ log phù hợp.
      </p>

      <h2>6. Lựa chọn của bạn</h2>
      <p>
        Bạn có thể sửa hồ sơ, thay đổi quyền công khai hành trình đọc, tùy chọn thông báo, block/mute và xóa nội dung do mình sở hữu ở các màn hình tương ứng. Với yêu cầu truy cập, đính chính hoặc xóa dữ liệu ngoài khả năng tự phục vụ, hãy liên hệ quản trị viên của instance BookSpace đang sử dụng.
      </p>
      <p>
        Kênh tiếp nhận yêu cầu dữ liệu: <OperatorContact />.
      </p>

      <h2>7. Lưu trữ trên trình duyệt</h2>
      <p>
        Ứng dụng dùng local storage cho phiên đăng nhập và lựa chọn giao diện. Bản phát hành này không tích hợp quảng cáo hoặc analytics marketing và không cần cookie consent cho các mục đích đó. Nếu người vận hành thêm công cụ theo dõi, họ phải cập nhật thông báo và cơ chế đồng ý trước khi thu thập.
      </p>
    </LegalLayout>
  )
}

export function TermsPage() {
  return (
    <LegalLayout
      eyebrow="Cùng giữ không gian đọc lành mạnh"
      title="Điều khoản sử dụng"
      intro="Các quy tắc ngắn gọn để BookSpace vẫn là nơi đọc, trao đổi và kết nối an toàn."
    >
      <h2>1. Tài khoản</h2>
      <p>
        Bạn phải cung cấp thông tin đăng ký hợp lệ, giữ bí mật thông tin đăng nhập và chịu trách nhiệm cho hoạt động từ tài khoản của mình. Không dùng phiên, token hoặc danh tính của người khác. Hãy đặt lại mật khẩu ngay khi nghi ngờ tài khoản bị truy cập trái phép.
      </p>

      <h2>2. Nội dung và hành vi</h2>
      <p>
        Bạn giữ trách nhiệm với review, bình luận, bài viết, tin nhắn, ảnh và metadata mình đăng. Không spam, quấy rối, phát tán nội dung thù ghét, lừa đảo, xâm phạm quyền riêng tư/bản quyền hoặc cố vượt qua phân quyền. Không khai thác dữ liệu riêng tư của thành viên khác bằng tự động hóa.
      </p>

      <h2>3. Kiểm duyệt</h2>
      <p>
        Thành viên có thể báo cáo nội dung nhìn thấy. Quản trị viên có thể bác bỏ báo cáo, ẩn nội dung hoặc khóa tài khoản theo mức độ vi phạm; quyết định được lưu audit. Block/mute là công cụ cá nhân và không thay thế việc báo cáo nguy cơ an toàn.
      </p>

      <h2>4. Catalog và nội dung bên ngoài</h2>
      <p>
        Metadata sách phục vụ khám phá và có thể đến từ quản trị viên hoặc provider tùy chọn. Liên kết mua hàng, giá hoặc khả dụng từ bên ngoài có thể thay đổi; BookSpace không xử lý thanh toán và không bảo đảm giao dịch của nhà cung cấp.
      </p>

      <h2>5. Tính sẵn sàng</h2>
      <p>
        Người vận hành cố gắng giữ dữ liệu an toàn và dịch vụ ổn định nhưng có thể tạm dừng để bảo trì, sao lưu hoặc xử lý sự cố. Không dùng BookSpace làm nơi lưu duy nhất cho nội dung không thể thay thế; hãy giữ bản sao khi cần.
      </p>

      <h2>6. Thay đổi điều khoản</h2>
      <p>
        Khi điều khoản thay đổi đáng kể, người vận hành cần cập nhật ngày hiệu lực và thông báo phù hợp trước khi áp dụng. Việc tiếp tục sử dụng sau thời điểm có hiệu lực thể hiện bạn chấp nhận phiên bản mới.
      </p>

      <h2>7. Liên hệ</h2>
      <p>
        Câu hỏi, khiếu nại hoặc yêu cầu dữ liệu được gửi tới <OperatorContact />
        . Mỗi đơn vị triển khai phải công bố kênh liên hệ và thông tin pháp lý của mình trước khi mở đăng ký public.
      </p>
    </LegalLayout>
  )
}
