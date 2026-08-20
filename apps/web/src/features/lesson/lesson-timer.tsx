import { useEffect, useRef, useState } from 'react'
import { TimerReset } from 'lucide-react'
import { cn } from '@/lib/cn'

/** Dưới ngưỡng này thì đồng hồ chuyển màu cảnh báo. */
const WarningSeconds = 5 * 60

/**
 * Đồng hồ đếm ngược cho một lượt làm bài.
 *
 * Mốc thật nằm ở máy chủ — <c>secondsRemaining</c> tính từ <c>LessonAttempt.StartedAt</c>.
 * Đồng hồ ở đây chỉ để học viên nhìn thấy: nó đếm từ con số máy chủ đưa xuống, và mỗi lần
 * tải lại bài lại lấy con số mới. Sửa đồng hồ trong trình duyệt không mua thêm được giây nào,
 * vì máy chủ vẫn từ chối bước nộp muộn.
 */
export function useLessonCountdown(secondsFromServer: number | undefined, onExpire: () => void) {
  const [left, setLeft] = useState(secondsFromServer ?? 0)

  // Giữ callback trong ref để không phải đưa nó vào mảng phụ thuộc: onExpire thường là một
  // hàm mới ở mỗi lần render, đưa vào sẽ làm interval bị dựng lại mỗi giây.
  const expireRef = useRef(onExpire)
  expireRef.current = onExpire

  // Chỉ gọi onExpire một lần cho mỗi lượt, kể cả khi component render lại ở giây 0.
  const firedRef = useRef(false)

  useEffect(() => {
    if (secondsFromServer === undefined) {
      return
    }

    firedRef.current = false
    setLeft(secondsFromServer)
  }, [secondsFromServer])

  useEffect(() => {
    if (secondsFromServer === undefined) {
      return
    }

    const id = setInterval(() => {
      setLeft((prev) => {
        const next = prev - 1

        if (next <= 0 && !firedRef.current) {
          firedRef.current = true
          expireRef.current()
        }

        return next <= 0 ? 0 : next
      })
    }, 1000)

    return () => clearInterval(id)
  }, [secondsFromServer])

  return left
}

export function LessonCountdown({
  secondsLeft,
  limitMinutes,
}: {
  secondsLeft: number
  limitMinutes: number
}) {
  const minutes = Math.floor(secondsLeft / 60)
  const seconds = secondsLeft % 60
  const low = secondsLeft <= WarningSeconds

  return (
    <div
      // Đọc lại mỗi phút thay vì mỗi giây: đọc từng giây biến trình đọc màn hình thành
      // tiếng ồn liên tục và học viên không nghe được gì khác.
      aria-live={low ? 'polite' : 'off'}
      className={cn(
        'flex items-center gap-1.5 rounded-[var(--radius-control)] px-2 py-1 text-sm tabular-nums',
        low
          ? 'bg-[color-mix(in_oklch,var(--color-warning)_18%,transparent)] text-[var(--color-warning-text)]'
          : 'text-secondary',
      )}
      title={`Mỗi lượt làm bài tối đa ${limitMinutes} phút. Hết giờ thì bài quay về đầu.`}
    >
      <TimerReset className="size-4 shrink-0" aria-hidden />
      <span className="sr-only">Thời gian còn lại</span>
      {String(minutes).padStart(2, '0')}:{String(seconds).padStart(2, '0')}
    </div>
  )
}
