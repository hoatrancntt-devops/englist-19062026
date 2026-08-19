import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Nổi bật thẻ quan trọng nhất màn hình (ô "Học tiếp"). Chỉ dùng cho đúng một thẻ. */
  emphasis?: boolean
}

export function Card({ emphasis = false, className, children, ...props }: CardProps) {
  return (
    <div
      className={cn(
        'surface-card',
        emphasis && 'border-brand-400 dark:border-brand-600 ring-1 ring-brand-400/30',
        className,
      )}
      {...props}
    >
      {children}
    </div>
  )
}

interface CardHeaderProps {
  title: ReactNode
  description?: ReactNode
  icon?: ReactNode
  action?: ReactNode
}

export function CardHeader({ title, description, icon, action }: CardHeaderProps) {
  return (
    <div className="flex items-start justify-between gap-4 p-5 pb-3">
      <div className="flex items-start gap-3 min-w-0">
        {icon && <div className="shrink-0 mt-0.5">{icon}</div>}
        <div className="min-w-0">
          <h2 className="font-semibold text-[var(--text-primary)] truncate">{title}</h2>
          {description && <p className="mt-1 text-sm text-secondary">{description}</p>}
        </div>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  )
}

export function CardBody({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn('px-5 pb-5', className)} {...props}>
      {children}
    </div>
  )
}
