import { forwardRef, useId } from 'react'
import type { InputHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  /** Gợi ý hiển thị dưới ô nhập khi chưa có lỗi. */
  hint?: string
}

/**
 * Ô nhập có nhãn, gợi ý và lỗi được nối đúng bằng aria.
 * Viết một lần ở đây để không màn hình nào phải tự nhớ nối id — đó là chỗ
 * khả năng tiếp cận hay bị bỏ sót nhất.
 */
export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, error, hint, className, id, ...props },
  ref,
) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const errorId = `${inputId}-error`
  const hintId = `${inputId}-hint`

  return (
    <div>
      <label htmlFor={inputId} className="mb-1.5 block text-sm font-medium">
        {label}
      </label>

      <input
        ref={ref}
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : hint ? hintId : undefined}
        className={cn(
          'w-full rounded-[var(--radius-control)] border bg-[var(--surface-raised)] px-3 py-2 text-sm',
          'placeholder:text-[var(--text-muted)]',
          'transition-colors',
          error
            ? 'border-[var(--color-danger)]'
            : 'border-[var(--border-strong)] focus:border-brand-500',
          className,
        )}
        {...props}
      />

      {error ? (
        <p id={errorId} role="alert" className="mt-1.5 text-xs text-[var(--color-danger)]">
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="mt-1.5 text-xs text-muted">
          {hint}
        </p>
      ) : null}
    </div>
  )
})
