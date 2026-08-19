import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

/**
 * Khung xương lúc tải. Dùng khối có kích thước gần đúng nội dung thật,
 * để layout không nhảy khi dữ liệu về.
 */
export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn('animate-pulse rounded-md bg-[var(--surface-sunken)]', className)}
      aria-hidden
    />
  )
}

export function SkeletonCard() {
  return (
    <div className="surface-card p-5 space-y-3">
      <Skeleton className="h-5 w-1/3" />
      <Skeleton className="h-4 w-2/3" />
      <Skeleton className="h-4 w-1/2" />
    </div>
  )
}

interface EmptyStateProps {
  illustration?: ReactNode
  title: string
  /** Nói người dùng phải làm gì tiếp, không chỉ nói là trống. */
  description: string
  action?: ReactNode
}

export function EmptyState({ illustration, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-12 px-6 text-center">
      {illustration && <div className="mb-4 opacity-90">{illustration}</div>}
      <h3 className="font-semibold text-[var(--text-primary)]">{title}</h3>
      <p className="mt-1.5 max-w-sm text-sm text-secondary">{description}</p>
      {action && <div className="mt-5">{action}</div>}
    </div>
  )
}

interface ProgressBarProps {
  value: number
  max?: number
  label?: string
  /** Màu theo kỹ năng, để thanh tiến độ khớp với hệ màu của nhãn kỹ năng. */
  tone?: 'brand' | 'listening' | 'speaking' | 'reading' | 'writing'
  className?: string
}

const toneVar: Record<NonNullable<ProgressBarProps['tone']>, string> = {
  brand: 'var(--color-brand-500)',
  listening: 'var(--color-skill-listening)',
  speaking: 'var(--color-skill-speaking)',
  reading: 'var(--color-skill-reading)',
  writing: 'var(--color-skill-writing)',
}

export function ProgressBar({ value, max = 100, label, tone = 'brand', className }: ProgressBarProps) {
  const percent = Math.min(100, Math.max(0, (value / max) * 100))

  return (
    <div className={className}>
      {label && (
        <div className="mb-1.5 flex items-baseline justify-between text-xs">
          <span className="text-secondary">{label}</span>
          <span className="font-medium tabular-nums text-[var(--text-primary)]">
            {Math.round(value)}
          </span>
        </div>
      )}
      <div
        role="progressbar"
        aria-valuenow={Math.round(value)}
        aria-valuemin={0}
        aria-valuemax={max}
        aria-label={label}
        className="h-2 w-full overflow-hidden rounded-full bg-[var(--surface-sunken)]"
      >
        <div
          className="h-full rounded-full transition-[width] duration-500 ease-out"
          style={{ width: `${percent}%`, backgroundColor: toneVar[tone] }}
        />
      </div>
    </div>
  )
}

type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'brand'

const badgeTone: Record<BadgeTone, string> = {
  neutral: 'bg-[var(--surface-sunken)] text-[var(--text-secondary)]',
  // Nền dùng màu trạng thái pha loãng, chữ dùng bản -text đậm hơn.
  // Dùng chung một màu cho cả hai thì trên nền sáng chỉ còn 2.0-3.5:1, dưới mức 4.5 mà chữ
  // 12px cần. Token -text tự đổi theo chế độ sáng/tối, xem app.css.
  success: 'bg-[color-mix(in_oklch,var(--color-success)_15%,transparent)] text-[var(--color-success-text)]',
  warning: 'bg-[color-mix(in_oklch,var(--color-warning)_18%,transparent)] text-[var(--color-warning-text)]',
  danger: 'bg-[color-mix(in_oklch,var(--color-danger)_15%,transparent)] text-[var(--color-danger-text)]',
  brand: 'bg-brand-50 text-brand-700 dark:bg-brand-900/40 dark:text-brand-200',
}

export function Badge({
  tone = 'neutral',
  children,
  className,
}: {
  tone?: BadgeTone
  children: ReactNode
  className?: string
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
        badgeTone[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}
