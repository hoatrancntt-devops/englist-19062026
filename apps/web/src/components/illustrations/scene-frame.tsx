/**
 * Khung chung cho mọi hình minh hoạ theo cảnh.
 *
 * Tách riêng khỏi tập hợp cảnh vì đã có hai tệp cảnh (đời sống/văn phòng và
 * chuyên môn) cùng dùng khung này. Để khung nằm trong một tệp cảnh thì tệp kia
 * phải import chéo, và chỉ cần thêm tệp thứ ba là thành vòng import.
 */

export interface SceneProps {
  className?: string
  size?: number
}

const defaults = { className: 'text-brand-500', size: 200 }

/** Tỷ lệ 5:3, nét 2.5, không nền — mọi cảnh vẽ trong khung 200×120 này. */
export function Scene({
  size = defaults.size,
  className = defaults.className,
  children,
}: SceneProps & { children: React.ReactNode }) {
  return (
    <svg
      width={size}
      height={size * 0.6}
      viewBox="0 0 200 120"
      fill="none"
      className={className}
      aria-hidden
    >
      {children}
    </svg>
  )
}
