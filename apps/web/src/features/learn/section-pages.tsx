import { Bell, Settings } from 'lucide-react'
import { UpcomingSection } from './upcoming-section'
import { SkillPage } from './skill-page'

/**
 * Trang cho từng mục trên thanh bên.
 *
 * Bốn kỹ năng dùng chung một component thật, xem skill-page.tsx. Trước đây chúng là trang
 * giữ chỗ liệt kê "còn thiếu" — và danh sách đó đã lỗi thời từ lâu, khiến người học bấm vào
 * rồi tưởng cả mảng chưa làm được gì trong khi bài nằm ngay trong lộ trình.
 *
 * Những mục còn lại vẫn dùng UpcomingSection: chúng khai đúng phần còn thiếu THẬT.
 */

export function ListeningPage() {
  return <SkillPage skill="Listening" />
}

export function SpeakingPage() {
  return <SkillPage skill="Speaking" />
}

export function ReadingPage() {
  return <SkillPage skill="Reading" />
}

export function WritingPage() {
  return <SkillPage skill="Writing" />
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
      description="Đổi mục tiêu phút mỗi ngày, múi giờ, giờ nhận nhắc học, và mật khẩu."
      todo={[
        'Đổi mục tiêu phút mỗi ngày và giờ nhắc học',
        'Đổi mật khẩu làm mọi thiết bị khác đăng xuất ngay',
      ]}
    />
  )
}