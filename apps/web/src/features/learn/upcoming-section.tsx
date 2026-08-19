import { Link } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/ui/feedback'

interface UpcomingSectionProps {
  icon: LucideIcon
  title: string
  /** Mô tả đúng thứ mục này sẽ làm, để người học biết chờ cái gì. */
  description: string
  /** Các việc cụ thể còn thiếu. Nói thật còn hơn để chữ "sắp có" chung chung. */
  todo: string[]
}

/**
 * Màn giữ chỗ cho các mục đang xây.
 *
 * Quan trọng: màn này nằm TRONG khung học viên, nên thanh bên và danh tính vẫn còn.
 * Trước đây các đường dẫn này không có route và rơi vào catch-all, khiến người đã đăng nhập
 * bị đá về trang tiếp thị và tưởng mình bị đăng xuất.
 */
export function UpcomingSection({ icon: Icon, title, description, todo }: UpcomingSectionProps) {
  return (
    <Card>
      <EmptyState
        illustration={<Icon className="size-12 text-brand-500" aria-hidden />}
        title={title}
        description={description}
        action={
          <Link to="/learn">
            <Button variant="secondary">Về bảng điều khiển</Button>
          </Link>
        }
      />

      <div className="border-t border-[var(--border-subtle)] px-6 py-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted">Còn thiếu</p>
        <ul className="mt-2 space-y-1">
          {todo.map((item) => (
            <li key={item} className="flex gap-2 text-sm text-secondary">
              <span className="mt-2 size-1.5 shrink-0 rounded-full bg-[var(--border-strong)]" aria-hidden />
              {item}
            </li>
          ))}
        </ul>
      </div>
    </Card>
  )
}
