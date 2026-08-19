/**
 * Bộ minh hoạ theo nhánh nghề.
 *
 * Vẽ tay bằng SVG inline thay vì tải ảnh: không phụ thuộc CDN, đổi màu theo theme,
 * và không tốn thêm một request nào. Mỗi hình dùng `currentColor` cho nét chính
 * nên chỉ cần đặt class text-* là đổi được tông.
 */

interface IllustrationProps {
  className?: string
  /** Kích thước tính bằng px cho cạnh dài. Mặc định 160. */
  size?: number
}

const base = 'text-brand-500'

/** Máy chủ và tủ rack — nhánh Infrastructure. */
export function InfraIllustration({ className = base, size = 160 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      <rect x="34" y="14" width="92" height="92" rx="6" stroke="currentColor" strokeWidth="2.5" opacity="0.9" />
      {[26, 48, 70].map((y, i) => (
        <g key={y}>
          <rect x="44" y={y} width="72" height="16" rx="3" fill="currentColor" opacity={0.12 + i * 0.04} />
          <circle cx="54" cy={y + 8} r="2.5" fill="currentColor" opacity="0.85">
            <animate attributeName="opacity" values="0.85;0.25;0.85" dur={`${1.8 + i * 0.4}s`} repeatCount="indefinite" />
          </circle>
          <circle cx="63" cy={y + 8} r="2.5" fill="currentColor" opacity="0.35" />
          <rect x="86" y={y + 6} width="24" height="4" rx="2" fill="currentColor" opacity="0.3" />
        </g>
      ))}
      <path d="M20 60h14M126 60h14" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      <circle cx="14" cy="60" r="5" stroke="currentColor" strokeWidth="2.5" />
      <circle cx="146" cy="60" r="5" stroke="currentColor" strokeWidth="2.5" />
    </svg>
  )
}

/** Đám mây và các nút triển khai — nhánh Cloud. */
export function CloudIllustration({ className = base, size = 160 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      <path
        d="M46 62a20 20 0 0 1 38-9 15 15 0 0 1 22 6 16 16 0 0 1-2 32H50a18 18 0 0 1-4-29Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path d="M62 30v-8M84 24l5-6M40 40l-6-5" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.45" />
      {[52, 80, 108].map((x, i) => (
        <g key={x}>
          <path d={`M${x} 91v10`} stroke="currentColor" strokeWidth="2" strokeLinecap="round" opacity="0.5" />
          <rect x={x - 9} y="101" width="18" height="13" rx="3" fill="currentColor" opacity="0.16" />
          <rect x={x - 9} y="101" width="18" height="13" rx="3" stroke="currentColor" strokeWidth="2" />
          <circle cx={x} cy="107.5" r="2" fill="currentColor">
            <animate attributeName="opacity" values="1;0.3;1" dur={`${2 + i * 0.5}s`} repeatCount="indefinite" />
          </circle>
        </g>
      ))}
    </svg>
  )
}

/** Khiên và khoá — nhánh Security. */
export function SecurityIllustration({ className = base, size = 160 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      <path
        d="M80 12 130 30v34c0 24-20 41-50 48-30-7-50-24-50-48V30L80 12Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path d="M80 22 120 36v28c0 19-16 33-40 39" fill="currentColor" opacity="0.08" />
      <rect x="64" y="58" width="32" height="26" rx="5" stroke="currentColor" strokeWidth="2.5" />
      <path d="M71 58v-9a9 9 0 0 1 18 0v9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="80" cy="70" r="3.5" fill="currentColor" />
      <path d="M80 73.5v5" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  )
}

/** Tai nghe và bong bóng thoại — nhánh Helpdesk. */
export function HelpdeskIllustration({ className = base, size = 160 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      <path d="M42 66V56a30 30 0 0 1 60 0v10" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="32" y="64" width="16" height="26" rx="6" stroke="currentColor" strokeWidth="2.5" />
      <rect x="96" y="64" width="16" height="26" rx="6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M104 90v4a10 10 0 0 1-10 10h-9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.6" />
      <rect x="60" y="98" width="24" height="13" rx="6" fill="currentColor" opacity="0.14" />
      <g opacity="0.75">
        <rect x="112" y="18" width="38" height="26" rx="7" stroke="currentColor" strokeWidth="2.5" />
        <path d="M122 44l-4 9 11-9" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
        {[122, 131, 140].map((cx, i) => (
          <circle key={cx} cx={cx} cy="31" r="2.5" fill="currentColor">
            <animate attributeName="opacity" values="0.3;1;0.3" dur="1.6s" begin={`${i * 0.25}s`} repeatCount="indefinite" />
          </circle>
        ))}
      </g>
    </svg>
  )
}

/** Mạng nơ-ron — nhánh AI. */
export function AiIllustration({ className = base, size = 160 }: IllustrationProps) {
  const layers = [
    { x: 34, ys: [38, 60, 82] },
    { x: 80, ys: [28, 50, 72, 94] },
    { x: 126, ys: [50, 72] },
  ]

  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      <g opacity="0.28" stroke="currentColor" strokeWidth="1.5">
        {layers[0].ys.map((y1) =>
          layers[1].ys.map((y2) => <line key={`a${y1}-${y2}`} x1="34" y1={y1} x2="80" y2={y2} />),
        )}
        {layers[1].ys.map((y1) =>
          layers[2].ys.map((y2) => <line key={`b${y1}-${y2}`} x1="80" y1={y1} x2="126" y2={y2} />),
        )}
      </g>
      {layers.map((layer, li) =>
        layer.ys.map((y, i) => (
          <circle key={`${layer.x}-${y}`} cx={layer.x} cy={y} r="6.5" fill="currentColor" opacity="0.9">
            <animate
              attributeName="opacity"
              values="0.9;0.35;0.9"
              dur="2.4s"
              begin={`${li * 0.3 + i * 0.15}s`}
              repeatCount="indefinite"
            />
          </circle>
        )),
      )}
    </svg>
  )
}

/** Tách cà phê, đồng hồ và bong bóng thoại — tầng Đời sống. */
export function LifeIllustration({ className = base, size = 160 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      {/* Tách cà phê */}
      <path d="M38 52h44v30a16 16 0 0 1-16 16H54a16 16 0 0 1-16-16V52Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M82 58h9a11 11 0 0 1 0 22h-9" stroke="currentColor" strokeWidth="2.5" />
      <path d="M38 52h44v8H38z" fill="currentColor" opacity="0.15" />
      {[50, 60, 70].map((x, i) => (
        <path key={x} d={`M${x} 42c0-5 5-5 5-10s-5-5-5-10`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5">
          <animate attributeName="opacity" values="0.2;0.65;0.2" dur="3s" begin={`${i * 0.5}s`} repeatCount="indefinite" />
        </path>
      ))}
      {/* Đồng hồ — số và giờ là bài đầu tiên của tầng này */}
      <circle cx="122" cy="40" r="20" stroke="currentColor" strokeWidth="2.5" />
      <path d="M122 28v12l8 5" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      {/* Bong bóng thoại */}
      <rect x="100" y="76" width="44" height="26" rx="7" stroke="currentColor" strokeWidth="2.5" />
      <path d="M110 102l-3 9 10-9" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[112, 122, 132].map((cx, i) => (
        <circle key={cx} cx={cx} cy="89" r="2.5" fill="currentColor">
          <animate attributeName="opacity" values="0.3;1;0.3" dur="1.6s" begin={`${i * 0.25}s`} repeatCount="indefinite" />
        </circle>
      ))}
    </svg>
  )
}

/** Bàn làm việc, lịch họp và tin nhắn nội bộ — tầng Văn phòng. */
export function OfficeIllustration({ className = base, size = 160 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.75} viewBox="0 0 160 120" fill="none" className={className} aria-hidden>
      {/* Màn hình và bàn */}
      <rect x="18" y="24" width="74" height="50" rx="6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M44 74v8h22v-8M34 82h42" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {[34, 44, 54].map((y, i) => (
        <rect key={y} x="28" y={y} width={i === 1 ? 40 : 54} height="5" rx="2.5" fill="currentColor" opacity="0.2" />
      ))}
      {/* Lịch họp */}
      <rect x="104" y="20" width="42" height="40" rx="6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M104 32h42M116 20v-6M134 20v-6" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="112" y="40" width="12" height="10" rx="2" fill="currentColor" opacity="0.55">
        <animate attributeName="opacity" values="0.55;0.15;0.55" dur="2.4s" repeatCount="indefinite" />
      </rect>
      {/* Tin nhắn nội bộ */}
      <rect x="96" y="72" width="34" height="20" rx="6" fill="currentColor" opacity="0.14" />
      <rect x="96" y="72" width="34" height="20" rx="6" stroke="currentColor" strokeWidth="2" />
      <rect x="112" y="96" width="34" height="18" rx="6" stroke="currentColor" strokeWidth="2" opacity="0.6" />
    </svg>
  )
}

/** Người đeo tai nghe trước màn hình — dùng cho trang chủ. */
export function HeroIllustration({ className = base, size = 320 }: IllustrationProps) {
  return (
    <svg width={size} height={size * 0.62} viewBox="0 0 320 200" fill="none" className={className} aria-hidden>
      <rect x="150" y="34" width="140" height="96" rx="8" stroke="currentColor" strokeWidth="2.5" />
      <rect x="150" y="34" width="140" height="96" rx="8" fill="currentColor" opacity="0.05" />
      <path d="M196 130v10h48v-10M180 140h80" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />

      {/* Dòng log chạy trên màn hình */}
      {[52, 66, 80, 94, 108].map((y, i) => (
        <rect
          key={y}
          x="164"
          y={y}
          width={i % 2 === 0 ? 96 : 66}
          height="6"
          rx="3"
          fill="currentColor"
          opacity="0.22"
        >
          <animate attributeName="opacity" values="0.22;0.5;0.22" dur="3s" begin={`${i * 0.4}s`} repeatCount="indefinite" />
        </rect>
      ))}

      {/* Người học */}
      <circle cx="74" cy="70" r="24" stroke="currentColor" strokeWidth="2.5" />
      <path d="M44 142a30 30 0 0 1 60 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M50 70v-6a24 24 0 0 1 48 0v6" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="42" y="68" width="11" height="20" rx="5" fill="currentColor" opacity="0.85" />
      <rect x="95" y="68" width="11" height="20" rx="5" fill="currentColor" opacity="0.85" />

      {/* Sóng âm giữa người và màn hình */}
      {[0, 1, 2].map((i) => (
        <path
          key={i}
          d={`M${116 + i * 10} ${86 - i * 6}a${8 + i * 4} ${8 + i * 4} 0 0 1 0 ${16 + i * 12}`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          opacity="0.5"
        >
          <animate attributeName="opacity" values="0.15;0.7;0.15" dur="2s" begin={`${i * 0.35}s`} repeatCount="indefinite" />
        </path>
      ))}
    </svg>
  )
}
