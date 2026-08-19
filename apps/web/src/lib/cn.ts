import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/**
 * Gộp class Tailwind và giải xung đột.
 *
 * Không có hàm này thì `cn('p-2', 'p-4')` để lại cả hai class và kết quả phụ thuộc
 * thứ tự trong file CSS đã build — một nguồn bug rất khó nhìn ra.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs))
}
