/**
 * Cảnh cho nhánh đời sống: nhà hàng, siêu thị, sở thích.
 *
 * Tách khỏi scene-illustrations.tsx vì tệp đó đã hơn 480 dòng và trộn cả cảnh
 * văn phòng lẫn cảnh đời sống. Ba nhánh đời sống nay có 24 bài, và trước tệp này
 * chúng phải dùng chung vài khoá chung chung — money-receipt gánh cả hoá đơn nhà
 * hàng, bảng giá siêu thị lẫn chính sách đổi trả, nên mở bài nào cũng thấy một hình.
 *
 * Mọi cảnh vẽ trong khung 200×120 của Scene, nét chính dùng currentColor để tự đổi
 * theo nền sáng hoặc tối. Nền, vệt ấm và bóng đổ do Scene lo, cảnh chỉ vẽ nội dung.
 */

import { Scene, type SceneProps } from './scene-frame'

/** Giỏ hàng siêu thị đang đầy — tìm hàng, đi chợ. */
function ShoppingCart(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M44 34h12l8 14" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M64 48h74l-11 34H75L64 48Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M64 48h74l-3 9H67l-3-9Z" fill="currentColor" opacity="0.18" />
      <path d="M86 57v16M104 57v16M122 57v16" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.45" />
      <circle cx="84" cy="94" r="7" stroke="currentColor" strokeWidth="2.5" />
      <circle cx="124" cy="94" r="7" stroke="currentColor" strokeWidth="2.5" />
      {[0, 1].map((i) => (
        <rect key={i} x={90 + i * 22} y={28} width="16" height="14" rx="3" fill="currentColor" opacity="0.3">
          <animate attributeName="opacity" values="0.15;0.5;0.15" dur="2.4s" begin={`${i * 0.5}s`} repeatCount="indefinite" />
        </rect>
      ))}
    </Scene>
  )
}

/** Giá treo quần áo và một chiếc áo đang được chọn — thử đồ, hỏi cỡ. */
function ClothesRack(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M40 32h120" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {[70, 100, 130].map((x, i) => (
        <g key={x} opacity={i === 1 ? 1 : 0.45}>
          <path d={`M${x} 32v6`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
          <path
            d={`M${x} 38l-14 8v34h28V46l-14-8Z`}
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinejoin="round"
          />
          <path d={`M${x - 14} 46l-8 6M${x + 14} 46l8 6`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
        </g>
      ))}
      <rect x="118" y="86" width="26" height="16" rx="4" fill="currentColor" opacity="0.2" />
      <path d="M124 94h14" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </Scene>
  )
}

/** Nhãn giá có lỗ treo và dấu phần trăm — giá, khuyến mãi, so sánh. */
function PriceTag(props: SceneProps) {
  return (
    <Scene {...props}>
      <path
        d="M92 26H56a10 10 0 0 0-10 10v34a10 10 0 0 0 10 10h36l42-27-42-27Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <circle cx="64" cy="46" r="6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M78 74l30-32" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="82" cy="48" r="6" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      <circle cx="104" cy="68" r="6" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      <path d="M138 88h30" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.4" />
      <path d="M130 96h38" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.25" />
    </Scene>
  )
}

/** Kiện hàng đang chuyển động — đặt giao hàng, theo dõi đơn. */
function ParcelDelivery(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M72 44h58v42H72z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M72 44h58v11H72z" fill="currentColor" opacity="0.2" />
      <path d="M101 44v42" stroke="currentColor" strokeWidth="2.5" />
      <path d="M92 30h18l-9 14-9-14Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[0, 1, 2].map((i) => (
        <path
          key={i}
          d={`M${40 - i * 0} ${54 + i * 12}h${26 - i * 7}`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
        >
          <animate attributeName="opacity" values="0.15;0.75;0.15" dur="1.6s" begin={`${i * 0.25}s`} repeatCount="indefinite" />
        </path>
      ))}
      <path d="M140 86h24" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.35" />
    </Scene>
  )
}

/** Kiện hàng với mũi tên vòng ngược — đổi trả, hoàn tiền, bảo hành. */
function ReturnParcel(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M70 52h60v40H70z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M70 52h60v11H70z" fill="currentColor" opacity="0.2" />
      <path d="M100 52v40" stroke="currentColor" strokeWidth="2.5" />
      <path
        d="M64 40a42 42 0 0 1 76 6"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
      <path d="M64 40l-2-13M64 40l13-3" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="148" cy="80" r="12" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
      <path d="M143 80l4 4 7-8" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0.5" />
    </Scene>
  )
}

/** Đĩa ăn kèm biển cảnh báo — dị ứng, ăn chay, yêu cầu đặc biệt. */
function AllergyAlert(props: SceneProps) {
  return (
    <Scene {...props}>
      <ellipse cx="88" cy="76" rx="42" ry="16" stroke="currentColor" strokeWidth="2.5" />
      <ellipse cx="88" cy="74" rx="26" ry="9" stroke="currentColor" strokeWidth="2.5" opacity="0.45" />
      <path d="M34 52v22M34 52a5 5 0 0 1 10 0v22" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      <path
        d="M136 30l24 42h-48l24-42Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path d="M136 30l24 42h-48l24-42Z" fill="var(--color-warning)" opacity="0.22" />
      <path d="M136 44v14" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="136" cy="64" r="2.5" fill="currentColor">
        <animate attributeName="opacity" values="0.3;1;0.3" dur="1.6s" repeatCount="indefinite" />
      </circle>
    </Scene>
  )
}

/** Ly giấy có nắp và hơi bốc lên — quán cà phê, đồ uống mang đi. */
function CoffeeToGo(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M74 44h52l-7 52H81l-7-52Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M70 34h60v10H70z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M70 34h60v10H70z" fill="currentColor" opacity="0.22" />
      <path d="M78 62h44l-3 20H81l-3-20Z" fill="currentColor" opacity="0.14" />
      <path d="M79 62h42M81 82h38" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.55" />
      {[88, 100, 112].map((x, i) => (
        <path
          key={x}
          d={`M${x} 28c0-5 5-5 5-10s-5-5-5-10`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
        >
          <animate attributeName="opacity" values="0.15;0.7;0.15" dur="3s" begin={`${i * 0.5}s`} repeatCount="indefinite" />
        </path>
      ))}
    </Scene>
  )
}

/** Tạ tay — thể thao, phòng tập, lịch tập. */
function GymDumbbell(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M66 60h68" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="52" y="44" width="14" height="32" rx="4" stroke="currentColor" strokeWidth="2.5" />
      <rect x="36" y="52" width="12" height="16" rx="4" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      <rect x="134" y="44" width="14" height="32" rx="4" stroke="currentColor" strokeWidth="2.5" />
      <rect x="152" y="52" width="12" height="16" rx="4" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      <rect x="52" y="44" width="14" height="32" rx="4" fill="currentColor" opacity="0.18" />
      <rect x="134" y="44" width="14" height="32" rx="4" fill="currentColor" opacity="0.18" />
      <path d="M72 92h56" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.3" />
      <path d="M84 30l8 10 8-16 8 12" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0.45" />
    </Scene>
  )
}

/** Nút phát, nốt nhạc và quyển sách — phim, nhạc, sách, game. */
function MediaPlay(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="58" y="34" width="60" height="46" rx="10" stroke="currentColor" strokeWidth="2.5" />
      <path d="M80 48l20 9-20 9V48Z" fill="currentColor" opacity="0.75" />
      <path d="M74 92h28" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.35" />
      <path d="M136 28v32" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M136 28l16 5v10l-16-5" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <circle cx="130" cy="62" r="7" fill="currentColor" opacity="0.6">
        <animate attributeName="opacity" values="0.3;0.85;0.3" dur="2.2s" repeatCount="indefinite" />
      </circle>
      <path d="M126 78h34v20h-34z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" opacity="0.5" />
      <path d="M143 78v20" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
    </Scene>
  )
}

/** Sách mở và ba mốc trên một đường — kể chuyện có mở, quặt và kết. */
function StoryTimeline(props: SceneProps) {
  return (
    <Scene {...props}>
      <path
        d="M36 34c14-6 28-6 42 0v40c-14-6-28-6-42 0V34Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path
        d="M78 34c14-6 28-6 42 0v40c-14-6-28-6-42 0V34Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path d="M78 34v40" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
      <path d="M46 46h22M46 56h22M90 46h22M90 56h22" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.3" />
      <path d="M132 62h44" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      {[132, 154, 176].map((x, i) => (
        <circle key={x} cx={x} cy="62" r="6" fill="currentColor" opacity="0.55">
          <animate attributeName="opacity" values="0.2;0.9;0.2" dur="2.4s" begin={`${i * 0.6}s`} repeatCount="indefinite" />
        </circle>
      ))}
    </Scene>
  )
}

/** Bàn ăn hai người có nến — bữa ăn công việc, mời đối tác. */
function DiningTable(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M28 80h144" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M28 80h144v8H28z" fill="currentColor" opacity="0.14" />
      <ellipse cx="62" cy="72" rx="22" ry="7" stroke="currentColor" strokeWidth="2.5" />
      <ellipse cx="138" cy="72" rx="22" ry="7" stroke="currentColor" strokeWidth="2.5" />
      <path d="M36 56v12M36 56a4 4 0 0 1 8 0v12" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      <path d="M164 56v12M164 56a4 4 0 0 0-8 0v12" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      <rect x="94" y="48" width="12" height="24" rx="3" stroke="currentColor" strokeWidth="2.5" />
      <path d="M100 48c0-8 6-8 6-14-8 2-12 6-6 14Z" fill="var(--color-warning)" opacity="0.55">
        <animate attributeName="opacity" values="0.3;0.8;0.3" dur="2.6s" repeatCount="indefinite" />
      </path>
    </Scene>
  )
}

/** Laptop mở kèm bảng thông số — mua đồ điện tử, so sánh cấu hình. */
function LaptopSpecs(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M50 32h72v46H50z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M40 78h92l8 12H32l8-12Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M40 78h92l8 12H32l8-12Z" fill="currentColor" opacity="0.16" />
      {[44, 54, 64].map((y, i) => (
        <g key={y}>
          <path d={`M60 ${y}h18`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.4" />
          <path d={`M84 ${y}h${18 + i * 8}`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.7">
            <animate attributeName="opacity" values="0.3;0.9;0.3" dur="2.4s" begin={`${i * 0.4}s`} repeatCount="indefinite" />
          </path>
        </g>
      ))}
      <circle cx="156" cy="46" r="16" stroke="currentColor" strokeWidth="2.5" opacity="0.55" />
      <path d="M150 46l5 5 9-11" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0.55" />
    </Scene>
  )
}

export const LIFE_SCENES: Record<string, (props: SceneProps) => React.ReactElement> = {
  'shopping-cart': ShoppingCart,
  'clothes-rack': ClothesRack,
  'price-tag': PriceTag,
  'parcel-delivery': ParcelDelivery,
  'return-parcel': ReturnParcel,
  'allergy-alert': AllergyAlert,
  'coffee-togo': CoffeeToGo,
  'gym-dumbbell': GymDumbbell,
  'media-play': MediaPlay,
  'story-timeline': StoryTimeline,
  'dining-table': DiningTable,
  'laptop-specs': LaptopSpecs,
}
