/**
 * Khung chung cho mọi hình minh hoạ theo cảnh.
 *
 * Tách riêng khỏi tập hợp cảnh vì đã có hai tệp cảnh (đời sống/văn phòng và
 * chuyên môn) cùng dùng khung này. Để khung nằm trong một tệp cảnh thì tệp kia
 * phải import chéo, và chỉ cần thêm tệp thứ ba là thành vòng import.
 *
 * Khung vẽ sẵn một tấm nền bo góc, một vệt nền ấm và một bóng đổ dưới chân cảnh.
 * Ba lớp đó nằm ở đây chứ không nằm trong từng cảnh: 33 cảnh chỉ vẽ nét chính
 * bằng currentColor, nên sửa một chỗ là cả bộ cùng đổi. Trước khi có tấm nền,
 * hình chỉ có nét mảnh một màu trôi trên nền trang và người học lướt qua không
 * nhận ra đó là hình gì.
 */

export interface SceneProps {
  className?: string
  size?: number
  /** 'bare' bỏ tấm nền — dùng cho hình nhỏ đặt sẵn trong thẻ đã có nền riêng. */
  variant?: 'panel' | 'bare'
}

const defaults = { className: 'text-brand-500', size: 200 }

/** Tỷ lệ 5:3, nét 2.5 — mọi cảnh vẽ trong khung 200×120 này. */
export function Scene({
  size = defaults.size,
  className = defaults.className,
  variant = 'panel',
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
      {variant === 'panel' && (
        <>
          {/* Tấm nền tách cảnh khỏi nền trang, ở cả chế độ sáng lẫn tối. */}
          <rect x="1.25" y="1.25" width="197.5" height="117.5" rx="16" fill="currentColor" opacity="0.07" />
          <rect
            x="1.25"
            y="1.25"
            width="197.5"
            height="117.5"
            rx="16"
            stroke="currentColor"
            strokeWidth="2.5"
            opacity="0.16"
          />
          {/* Màu thứ hai vào bằng hai nét cung mảnh ở góc, không bằng mảng tròn đặc.
              Mảng đặc trên nền tối biến thành đốm xám đục nằm đè lên nét chính —
              nhìn ra ngay là vết bẩn chứ không phải điểm nhấn. */}
          <path
            d="M150 8a44 44 0 0 1 42 30"
            stroke="var(--color-warning)"
            strokeWidth="3"
            strokeLinecap="round"
            opacity="0.5"
            fill="none"
          />
          <path
            d="M8 84a30 30 0 0 0 22 28"
            stroke="var(--color-warning)"
            strokeWidth="3"
            strokeLinecap="round"
            opacity="0.28"
            fill="none"
          />
          {/* Bóng dưới chân cảnh để nhân vật không lơ lửng. */}
          <ellipse cx="100" cy="107" rx="66" ry="6" fill="currentColor" opacity="0.10" />
        </>
      )}
      {children}
    </svg>
  )
}
