import { Headphones, Mic, BookOpen, PenLine, Bell, Settings } from 'lucide-react'
import { UpcomingSection } from './upcoming-section'

/**
 * Trang cho từng mục trên thanh bên.
 *
 * Mỗi trang khai đúng phần còn thiếu của mục đó thay vì một câu "sắp có" dùng chung —
 * người học biết mình đang chờ cái gì, và người phát triển biết mình còn nợ cái gì.
 */

export function ListeningPage() {
  return (
    <UpcomingSection
      icon={Headphones}
      title="Luyện Nghe"
      description="Nghe hội thoại nghề theo tốc độ tăng dần: bậc L1 đọc chậm 0.85, lên B1 là 1.3 — bằng tốc độ người bản xứ nói trong họp."
      todo={[
        'Sinh audio bằng Piper lúc seed (chi phí runtime bằng 0)',
        'Trình phát có tua lại từng câu và hiện phụ đề EN/VI theo yêu cầu',
        'Câu hỏi nghe hiểu chấm tại máy chủ',
      ]}
    />
  )
}

export function SpeakingPage() {
  return (
    <UpcomingSection
      icon={Mic}
      title="Luyện Nói"
      description="Đọc theo mẫu rồi trả lời tình huống thật. Chấm ba trục: phát âm, độ trôi chảy, khả năng truyền đạt."
      todo={[
        'Dịch vụ nhận dạng giọng nói chạy tại chỗ (faster-whisper), giọng không rời máy chủ',
        'So khớp âm vị và nhận xét bằng tiếng Việt theo lỗi cụ thể',
        'Cần HTTPS: micro trình duyệt không chạy trên HTTP thuần',
      ]}
    />
  )
}

export function ReadingPage() {
  return (
    <UpcomingSection
      icon={BookOpen}
      title="Luyện Đọc"
      description="Đọc email, ticket, log, release notes và postmortem thật của nghề. Câu hỏi bằng tiếng Anh, chia hai loại: đọc lướt và tìm chi tiết."
      todo={[
        'Bài đọc theo 13 mục postmortem của Google SRE',
        'Phân biệt root cause với trigger — hai thứ hay bị lẫn',
        'Không sinh audio cho bài đọc: đây là kỹ năng đọc, không phải nghe',
      ]}
    />
  )
}

export function WritingPage() {
  return (
    <UpcomingSection
      icon={PenLine}
      title="Luyện Viết"
      description="Điền chỗ trống, sắp lại câu, và viết email có hướng dẫn. Chấm bằng luật ngay tại máy chủ, không cần khoá API."
      todo={[
        'Ba bộ luật chấm: điền chỗ trống (có bỏ qua lỗi chính tả nhẹ), sắp câu, email có hướng dẫn',
        'Ngưỡng đạt 80 điểm',
        'Mẫu incident report và change request theo chuẩn ngành',
      ]}
    />
  )
}


export function NotificationsPage() {
  return (
    <UpcomingSection
      icon={Bell}
      title="Thông báo"
      description="Nhắc học, cảnh báo đứt chuỗi, bài vừa mở khoá, câu tới hạn ôn, và báo cáo tuần."
      todo={[
        'Chín loại thông báo, có khoá gộp để không dội hàng loạt khi engine tính lại lộ trình',
        'Giờ không làm phiền theo múi giờ của từng người',
        'Gửi email qua hộp thư đi có cơ chế thử lại',
      ]}
    />
  )
}

export function SettingsPage() {
  return (
    <UpcomingSection
      icon={Settings}
      title="Cài đặt"
      description="Đổi chế độ học, mục tiêu phút mỗi ngày, múi giờ, giờ nhận nhắc học, và mật khẩu."
      todo={[
        'Đổi chế độ học không xoá tiến độ đã có',
        'Đổi mật khẩu làm mọi thiết bị khác đăng xuất ngay',
      ]}
    />
  )
}

