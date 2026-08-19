import { AlertTriangle, CheckCircle2 } from 'lucide-react'

import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge } from '@/components/ui/feedback'
import type { AdminOverview, ContentHealth } from './admin-types'

/**
 * Tổng quan hệ thống.
 *
 * Phần đáng giá nhất ở đây không phải các con số đếm, mà là khối sức khoẻ nội dung bên dưới:
 * nó bắt những hỏng hóc <b>không báo lỗi ở đâu cả</b> — bài không có câu hỏi nào, câu ôn trỏ
 * vào câu đã xoá, đáp án dồn hết về một vị trí.
 */
export function AdminOverviewPanel({ data }: { data: AdminOverview }) {
  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Stat label="Học viên" value={data.learners} />
        <Stat label="Phiên đang mở" value={data.activeSessions} />
        <Stat label="Bài đã xuất bản" value={data.lessonsPublished} />
        <Stat label="Câu hỏi" value={data.lessonItems} />
        <Stat label="Đề xếp lớp" value={data.placementForms} />
        <Stat label="Kịch bản đóng vai" value={data.roleplayScenarios} />
      </div>

      <ContentHealthCard health={data.content} />

      <Card>
        <CardHeader title="Hộp thư đi" description="Thư được xếp hàng rồi worker gửi, nên máy chủ mail chết không làm hỏng thao tác của người dùng." />
        <CardBody className="flex flex-wrap gap-4 text-sm">
          <span>Chờ gửi: <strong>{data.outbox.pending}</strong></span>
          <span>Đã gửi: <strong>{data.outbox.sent}</strong></span>
          <span className={data.outbox.failed > 0 ? 'text-[var(--color-danger)]' : undefined}>
            Thất bại: <strong>{data.outbox.failed}</strong>
          </span>

          {data.outbox.lastError && (
            <p className="w-full text-secondary">Lỗi gần nhất: {data.outbox.lastError}</p>
          )}
        </CardBody>
      </Card>
    </div>
  )
}

function ContentHealthCard({ health }: { health: ContentHealth }) {
  // Ngưỡng 40%: với bốn phương án, phân bố đều là 25%. Vượt 40% nghĩa là học viên
  // đoán theo vị trí cũng qua được kha khá câu.
  const skewRatio =
    health.totalAnswerableItems > 0 ? health.mostCommonAnswerCount / health.totalAnswerableItems : 0

  const issues = [
    health.lessonsWithoutItems > 0 && {
      text: `${health.lessonsWithoutItems} bài đã xuất bản nhưng không có câu hỏi nào. Học viên mở ra sẽ thấy màn trống.`,
    },
    health.orphanReviewItems > 0 && {
      text: `${health.orphanReviewItems} câu trong hàng đợi ôn trỏ vào câu hỏi đã bị xoá.`,
    },
    skewRatio > 0.4 && {
      text:
        `Đáp án đúng dồn về vị trí ${health.mostCommonAnswerPosition + 1}: `
        + `${health.mostCommonAnswerCount}/${health.totalAnswerableItems} câu `
        + `(${Math.round(skewRatio * 100)}%). Học viên chọn mãi ô đó là qua bài mà không cần đọc.`,
    },
  ].filter(Boolean) as { text: string }[]

  return (
    <Card>
      <CardHeader
        title="Sức khoẻ nội dung"
        description="Những hỏng hóc không báo lỗi ở đâu cả, chỉ lộ ra khi có người học tới đúng chỗ đó."
        icon={
          issues.length === 0 ? (
            <CheckCircle2 className="size-5 text-[var(--color-success)]" aria-hidden />
          ) : (
            <AlertTriangle className="size-5 text-[var(--color-warning)]" aria-hidden />
          )
        }
        action={<Badge>{issues.length === 0 ? 'Không có vấn đề' : `${issues.length} vấn đề`}</Badge>}
      />
      <CardBody>
        {issues.length === 0 ? (
          <p className="text-sm text-secondary">
            Mọi bài đã xuất bản đều có câu hỏi, hàng đợi ôn không có câu mồ côi, và đáp án
            phân bố đều giữa các vị trí.
          </p>
        ) : (
          <ul className="space-y-2 text-sm">
            {issues.map((issue, index) => (
              <li key={index} className="flex gap-2">
                <AlertTriangle className="mt-0.5 size-4 shrink-0 text-[var(--color-warning)]" aria-hidden />
                <span>{issue.text}</span>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  )
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-4">
      <p className="text-sm text-secondary">{label}</p>
      <p className="mt-1 text-2xl font-semibold tabular-nums">{value}</p>
    </div>
  )
}
