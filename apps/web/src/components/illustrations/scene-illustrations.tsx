/**
 * Hình minh hoạ theo cảnh, tra bằng khoá đặt trong YAML của bài học.
 *
 * Vẽ SVG nhúng thẳng thay vì tải ảnh: không phụ thuộc CDN, tự đổi màu theo nền
 * sáng hoặc tối, và không tốn thêm request nào. Nét chính dùng currentColor,
 * mảng nền dùng opacity nên một hình dùng được ở mọi tông màu.
 *
 * Khoá phải khớp IllustrationCatalogue phía máy chủ. Khoá lạ rơi về hình mặc định
 * thay vì render trống — trống thì không ai nhận ra là thiếu.
 */

import { Scene, type SceneProps } from './scene-frame'
import { TECH_SCENES } from './tech-scene-illustrations'

/** Hai người đứng nói chuyện, có ly cà phê — pantry, small talk, rủ đi ăn. */
function CoffeeChat(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Người bên trái */}
      <circle cx="48" cy="38" r="13" stroke="currentColor" strokeWidth="2.5" />
      <path d="M31 96a17 17 0 0 1 34 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Người bên phải */}
      <circle cx="152" cy="38" r="13" stroke="currentColor" strokeWidth="2.5" />
      <path d="M135 96a17 17 0 0 1 34 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Bong bóng thoại qua lại */}
      <rect x="76" y="22" width="48" height="26" rx="8" stroke="currentColor" strokeWidth="2.5" />
      <path d="M88 48l-4 8 10-8" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[88, 100, 112].map((cx, i) => (
        <circle key={cx} cx={cx} cy="35" r="2.5" fill="currentColor">
          <animate attributeName="opacity" values="0.25;1;0.25" dur="1.8s" begin={`${i * 0.3}s`} repeatCount="indefinite" />
        </circle>
      ))}
      {/* Ly cà phê ở giữa */}
      <path d="M86 74h28v14a9 9 0 0 1-9 9h-10a9 9 0 0 1-9-9V74Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M114 78h6a6 6 0 0 1 0 12h-6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M86 74h28v5H86z" fill="currentColor" opacity="0.18" />
      {[94, 104].map((x, i) => (
        <path key={x} d={`M${x} 68c0-4 4-4 4-8s-4-4-4-8`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5">
          <animate attributeName="opacity" values="0.15;0.6;0.15" dur="3s" begin={`${i * 0.6}s`} repeatCount="indefinite" />
        </path>
      ))}
    </Scene>
  )
}

/** Người gọi điện, sóng âm — đánh vần tên, gọi tài xế, gọi vendor. */
function PhoneCall(props: SceneProps) {
  return (
    <Scene {...props}>
      <circle cx="70" cy="40" r="15" stroke="currentColor" strokeWidth="2.5" />
      <path d="M48 98a22 22 0 0 1 44 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="88" y="30" width="16" height="26" rx="5" fill="currentColor" opacity="0.85" />
      {[0, 1, 2].map((i) => (
        <path
          key={i}
          d={`M${114 + i * 12} ${52 - i * 7}a${9 + i * 5} ${9 + i * 5} 0 0 1 0 ${18 + i * 14}`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
        >
          <animate attributeName="opacity" values="0.15;0.8;0.15" dur="2s" begin={`${i * 0.35}s`} repeatCount="indefinite" />
        </path>
      ))}
    </Scene>
  )
}

/** Đồng hồ và lịch — giờ giấc, hẹn gặp, đổi lịch. */
function ClockCalendar(props: SceneProps) {
  return (
    <Scene {...props}>
      <circle cx="62" cy="60" r="32" stroke="currentColor" strokeWidth="2.5" />
      <path d="M62 40v22l14 9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      {[0, 90, 180, 270].map((deg) => (
        <line
          key={deg}
          x1="62" y1="32" x2="62" y2="37"
          stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"
          transform={`rotate(${deg} 62 60)`}
        />
      ))}
      <rect x="112" y="34" width="56" height="52" rx="7" stroke="currentColor" strokeWidth="2.5" />
      <path d="M112 50h56M128 34v-8M152 34v-8" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="122" y="58" width="14" height="12" rx="3" fill="currentColor" opacity="0.6">
        <animate attributeName="opacity" values="0.6;0.15;0.6" dur="2.6s" repeatCount="indefinite" />
      </rect>
      <rect x="144" y="58" width="14" height="12" rx="3" fill="currentColor" opacity="0.2" />
    </Scene>
  )
}

/** Người nghe không kịp, dấu hỏi — ba câu cứu hộ. */
function ConfusedListener(props: SceneProps) {
  return (
    <Scene {...props}>
      <circle cx="66" cy="46" r="16" stroke="currentColor" strokeWidth="2.5" />
      <path d="M42 100a24 24 0 0 1 48 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M56 40a10 10 0 1 1 12 12v6" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0" />
      {[
        { x: 112, y: 40, s: 1 },
        { x: 142, y: 28, s: 1.4 },
        { x: 168, y: 48, s: 1 },
      ].map((q, i) => (
        <text
          key={q.x}
          x={q.x}
          y={q.y}
          fontSize={22 * q.s}
          fontWeight="700"
          fill="currentColor"
          opacity="0.7"
        >
          ?
          <animate attributeName="opacity" values="0.2;0.9;0.2" dur="2.2s" begin={`${i * 0.4}s`} repeatCount="indefinite" />
        </text>
      ))}
      <path d="M92 56c8-6 14-6 18-2" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
    </Scene>
  )
}

/** Hai người bắt tay — giới thiệu bản thân, làm quen, giới thiệu người mới. */
function HandshakeIntro(props: SceneProps) {
  return (
    <Scene {...props}>
      <circle cx="54" cy="34" r="13" stroke="currentColor" strokeWidth="2.5" />
      <path d="M37 92a17 17 0 0 1 34 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="146" cy="34" r="13" stroke="currentColor" strokeWidth="2.5" />
      <path d="M129 92a17 17 0 0 1 34 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M70 64l18 8 24-8 18 8" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="100" cy="70" r="7" fill="currentColor" opacity="0.25" />
      <circle cx="100" cy="70" r="7" stroke="currentColor" strokeWidth="2.5" />
    </Scene>
  )
}

/** Đĩa thức ăn và quầy — gọi món, đi ăn trưa. */
function FoodOrder(props: SceneProps) {
  return (
    <Scene {...props}>
      <ellipse cx="72" cy="70" rx="34" ry="14" stroke="currentColor" strokeWidth="2.5" />
      <ellipse cx="72" cy="66" rx="22" ry="9" fill="currentColor" opacity="0.2" />
      <path d="M118 46v40M126 46v14a6 6 0 0 0 6 6v20" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M150 46c8 0 12 8 12 18s-4 10-4 10v12" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <rect x="34" y="92" width="132" height="6" rx="3" fill="currentColor" opacity="0.25" />
      {[62, 74, 86].map((x, i) => (
        <path key={x} d={`M${x} 48c0-4 4-4 4-8s-4-4-4-8`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.45">
          <animate attributeName="opacity" values="0.15;0.6;0.15" dur="3s" begin={`${i * 0.5}s`} repeatCount="indefinite" />
        </path>
      ))}
    </Scene>
  )
}

/** Xe và đường — đặt xe, đi lại, kẹt xe. */
function CityTransport(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M40 76h120" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.35" />
      <path d="M46 88h18M76 88h18M106 88h18M136 88h18" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.3" />
      <path d="M58 70v-12a6 6 0 0 1 6-6h30l12 18h18a8 8 0 0 1 8 8v6H58v-14Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M64 56h26v14H64z" fill="currentColor" opacity="0.18" />
      <circle cx="76" cy="76" r="8" stroke="currentColor" strokeWidth="2.5" />
      <circle cx="122" cy="76" r="8" stroke="currentColor" strokeWidth="2.5" />
      <path d="M148 40h16M154 30h16" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.4">
        <animate attributeName="opacity" values="0.15;0.6;0.15" dur="1.6s" repeatCount="indefinite" />
      </path>
    </Scene>
  )
}

/** Bản đồ và ghim vị trí — hỏi đường, chỉ đường. */
function MapDirections(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M40 34l38-12 44 14 38-12v66l-38 12-44-14-38 12V34Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M78 22v66M122 36v66" stroke="currentColor" strokeWidth="2.5" opacity="0.35" />
      <path d="M100 44a13 13 0 0 1 13 13c0 9-13 21-13 21s-13-12-13-21a13 13 0 0 1 13-13Z" fill="currentColor" opacity="0.2" />
      <path d="M100 44a13 13 0 0 1 13 13c0 9-13 21-13 21s-13-12-13-21a13 13 0 0 1 13-13Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <circle cx="100" cy="57" r="4.5" fill="currentColor">
        <animate attributeName="opacity" values="1;0.3;1" dur="2s" repeatCount="indefinite" />
      </circle>
    </Scene>
  )
}

/** Tiền và hoá đơn — mua bán, trả tiền, kiểm hoá đơn. */
function MoneyReceipt(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="30" y="40" width="76" height="42" rx="6" stroke="currentColor" strokeWidth="2.5" />
      <circle cx="68" cy="61" r="11" stroke="currentColor" strokeWidth="2.5" />
      <path d="M68 54v14M64 58h8M64 64h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M122 28h44v64l-8-6-7 6-7-6-7 6-7-6-8 6V28Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[44, 56, 68].map((y, i) => (
        <path key={y} d={`M132 ${y}h${i === 2 ? 16 : 24}`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.4" />
      ))}
    </Scene>
  )
}

/** Người ngồi bàn làm việc — văn phòng, viết lách. */
function DeskLaptop(props: SceneProps) {
  return (
    <Scene {...props}>
      <circle cx="62" cy="34" r="14" stroke="currentColor" strokeWidth="2.5" />
      <path d="M40 82a22 22 0 0 1 44 0" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M104 84V52a5 5 0 0 1 5-5h44a5 5 0 0 1 5 5v32" stroke="currentColor" strokeWidth="2.5" />
      <rect x="109" y="52" width="44" height="27" rx="2" fill="currentColor" opacity="0.15" />
      <path d="M94 84h74l6 10H88l6-10Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[58, 64, 70].map((y, i) => (
        <rect key={y} x="115" y={y} width={i === 1 ? 22 : 32} height="3" rx="1.5" fill="currentColor" opacity="0.45">
          <animate attributeName="opacity" values="0.2;0.6;0.2" dur="3s" begin={`${i * 0.4}s`} repeatCount="indefinite" />
        </rect>
      ))}
    </Scene>
  )
}

/** Ba người họp đứng — standup, họp team. */
function TeamStandup(props: SceneProps) {
  return (
    <Scene {...props}>
      {[
        { cx: 46, r: 12 },
        { cx: 100, r: 14 },
        { cx: 154, r: 12 },
      ].map((p) => (
        <g key={p.cx}>
          <circle cx={p.cx} cy={p.r === 14 ? 32 : 38} r={p.r} stroke="currentColor" strokeWidth="2.5" />
          <path
            d={`M${p.cx - p.r - 3} 92a${p.r + 3} ${p.r + 3} 0 0 1 ${(p.r + 3) * 2} 0`}
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
          />
        </g>
      ))}
      <rect x="84" y="58" width="32" height="18" rx="6" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      {[93, 100, 107].map((cx, i) => (
        <circle key={cx} cx={cx} cy="67" r="2" fill="currentColor">
          <animate attributeName="opacity" values="0.2;1;0.2" dur="1.6s" begin={`${i * 0.25}s`} repeatCount="indefinite" />
        </circle>
      ))}
    </Scene>
  )
}

/** Phong bì và danh sách thư — viết email, hộp thư. */
function EmailInbox(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="28" y="34" width="76" height="52" rx="6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M28 42l38 26 38-26" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[38, 54, 70].map((y, i) => (
        <g key={y}>
          <rect x="120" y={y} width="52" height="12" rx="4" stroke="currentColor" strokeWidth="2.5" opacity={i === 0 ? 1 : 0.45} />
          <circle cx="128" cy={y + 6} r="2.5" fill="currentColor" opacity={i === 0 ? 1 : 0.35}>
            {i === 0 && <animate attributeName="opacity" values="1;0.3;1" dur="2s" repeatCount="indefinite" />}
          </circle>
        </g>
      ))}
    </Scene>
  )
}

/** Hai bong bóng chat — nhắn tin Teams, Slack. */
function ChatMessage(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="26" y="26" width="86" height="44" rx="12" stroke="currentColor" strokeWidth="2.5" />
      <path d="M44 70l-6 14 18-14" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {[46, 58].map((y, i) => (
        <rect key={y} x="40" y={y} width={i === 0 ? 58 : 38} height="4" rx="2" fill="currentColor" opacity="0.4" />
      ))}
      <rect x="90" y="60" width="84" height="40" rx="12" stroke="currentColor" strokeWidth="2.5" opacity="0.7" fill="currentColor" fillOpacity="0.08" />
      <path d="M156 100l6 12-18-12" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" opacity="0.7" />
      {[128, 138, 148].map((cx, i) => (
        <circle key={cx} cx={cx} cy="80" r="2.5" fill="currentColor">
          <animate attributeName="opacity" values="0.25;1;0.25" dur="1.5s" begin={`${i * 0.22}s`} repeatCount="indefinite" />
        </circle>
      ))}
    </Scene>
  )
}

/** Màn hình họp có nhiều khung — họp online. */
function VideoCall(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="26" y="24" width="148" height="72" rx="8" stroke="currentColor" strokeWidth="2.5" />
      <path d="M78 96v8h44v-8M66 104h68" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {[
        { x: 38, y: 34 },
        { x: 106, y: 34 },
        { x: 38, y: 66 },
        { x: 106, y: 66 },
      ].map((p, i) => (
        <g key={`${p.x}-${p.y}`}>
          <rect x={p.x} y={p.y} width="56" height="22" rx="4" fill="currentColor" opacity={i === 0 ? 0.22 : 0.1} />
          <circle cx={p.x + 14} cy={p.y + 11} r="6" stroke="currentColor" strokeWidth="2" />
          <rect x={p.x + 26} y={p.y + 8} width="22" height="3" rx="1.5" fill="currentColor" opacity="0.5" />
          <rect x={p.x + 26} y={p.y + 14} width="14" height="3" rx="1.5" fill="currentColor" opacity="0.3" />
        </g>
      ))}
      <circle cx="52" cy="45" r="9" stroke="currentColor" strokeWidth="2.5">
        <animate attributeName="opacity" values="1;0.35;1" dur="2.4s" repeatCount="indefinite" />
      </circle>
    </Scene>
  )
}

/** Chồng ticket xếp hàng — helpdesk, hàng đợi. */
function TicketQueue(props: SceneProps) {
  return (
    <Scene {...props}>
      {[
        { y: 26, o: 1 },
        { y: 52, o: 0.65 },
        { y: 78, o: 0.35 },
      ].map((t) => (
        <g key={t.y} opacity={t.o}>
          <rect x="44" y={t.y} width="112" height="22" rx="6" stroke="currentColor" strokeWidth="2.5" />
          <circle cx="58" cy={t.y + 11} r="4" fill="currentColor" />
          <rect x="70" y={t.y + 6} width="46" height="4" rx="2" fill="currentColor" opacity="0.5" />
          <rect x="70" y={t.y + 14} width="28" height="3" rx="1.5" fill="currentColor" opacity="0.3" />
          <rect x="128" y={t.y + 7} width="20" height="9" rx="4" fill="currentColor" opacity="0.2" />
        </g>
      ))}
      <circle cx="58" cy="37" r="4" fill="currentColor">
        <animate attributeName="opacity" values="1;0.2;1" dur="1.8s" repeatCount="indefinite" />
      </circle>
    </Scene>
  )
}

/** Tủ rack máy chủ — hạ tầng. */
function ServerRack(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="54" y="16" width="92" height="92" rx="6" stroke="currentColor" strokeWidth="2.5" />
      {[26, 50, 74].map((y, i) => (
        <g key={y}>
          <rect x="64" y={y} width="72" height="18" rx="3" fill="currentColor" opacity={0.1 + i * 0.04} />
          <rect x="64" y={y} width="72" height="18" rx="3" stroke="currentColor" strokeWidth="2" />
          <circle cx="74" cy={y + 9} r="2.5" fill="currentColor">
            <animate attributeName="opacity" values="1;0.2;1" dur={`${1.8 + i * 0.4}s`} repeatCount="indefinite" />
          </circle>
          <circle cx="83" cy={y + 9} r="2.5" fill="currentColor" opacity="0.3" />
          <rect x="104" y={y + 7} width="24" height="4" rx="2" fill="currentColor" opacity="0.3" />
        </g>
      ))}
      <path d="M34 62h20M146 62h20" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
    </Scene>
  )
}

/** Tam giác cảnh báo trên máy chủ — sự cố, outage. */
function OutageAlert(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="30" y="52" width="66" height="56" rx="6" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
      {[62, 84].map((y) => (
        <rect key={y} x="40" y={y} width="46" height="14" rx="3" stroke="currentColor" strokeWidth="2" opacity="0.5" />
      ))}
      <path d="M136 20l40 70h-80l40-70Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M136 20l40 70h-80l40-70Z" fill="currentColor" opacity="0.12" />
      <path d="M136 44v22" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
      <circle cx="136" cy="76" r="3" fill="currentColor">
        <animate attributeName="opacity" values="1;0.2;1" dur="1.2s" repeatCount="indefinite" />
      </circle>
    </Scene>
  )
}

/** Sơ đồ mạng có nút và đường nối — VLAN, định tuyến, tường lửa. */
function NetworkDiagram(props: SceneProps) {
  return (
    <Scene {...props}>
      <g stroke="currentColor" strokeWidth="2.5" opacity="0.4">
        <line x1="42" y1="60" x2="100" y2="30" />
        <line x1="42" y1="60" x2="100" y2="90" />
        <line x1="100" y1="30" x2="158" y2="60" />
        <line x1="100" y1="90" x2="158" y2="60" />
        <line x1="100" y1="30" x2="100" y2="90" />
      </g>
      {[
        { cx: 42, cy: 60 },
        { cx: 100, cy: 30 },
        { cx: 100, cy: 90 },
        { cx: 158, cy: 60 },
      ].map((n, i) => (
        <g key={`${n.cx}-${n.cy}`}>
          <rect x={n.cx - 13} y={n.cy - 10} width="26" height="20" rx="5" fill="currentColor" opacity="0.15" />
          <rect x={n.cx - 13} y={n.cy - 10} width="26" height="20" rx="5" stroke="currentColor" strokeWidth="2.5" />
          <circle cx={n.cx} cy={n.cy} r="3" fill="currentColor">
            <animate attributeName="opacity" values="1;0.25;1" dur="2.2s" begin={`${i * 0.4}s`} repeatCount="indefinite" />
          </circle>
        </g>
      ))}
    </Scene>
  )
}

/** Đĩa cứng và mũi tên khôi phục — backup, DR. */
function BackupRestore(props: SceneProps) {
  return (
    <Scene {...props}>
      {[
        { y: 30, o: 1 },
        { y: 54, o: 0.6 },
        { y: 78, o: 0.3 },
      ].map((d) => (
        <g key={d.y} opacity={d.o}>
          <ellipse cx="62" cy={d.y} rx="30" ry="10" stroke="currentColor" strokeWidth="2.5" />
          <path d={`M32 ${d.y}v12c0 5.5 13.4 10 30 10s30-4.5 30-10V${d.y}`} stroke="currentColor" strokeWidth="2.5" />
        </g>
      ))}
      <path d="M116 62h34" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M140 52l12 10-12 10" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <animate attributeName="opacity" values="0.3;1;0.3" dur="2s" repeatCount="indefinite" />
      </path>
      <rect x="152" y="40" width="30" height="44" rx="5" stroke="currentColor" strokeWidth="2.5" />
      <circle cx="167" cy="62" r="5" fill="currentColor" opacity="0.4" />
    </Scene>
  )
}

/** Khiên và ổ khoá — bảo mật, quyền truy cập, cảnh báo. */
function ShieldLock(props: SceneProps) {
  return (
    <Scene {...props}>
      <path d="M100 14 152 32v34c0 24-20 41-52 48-32-7-52-24-52-48V32l52-18Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M100 24 142 38v28c0 19-16 33-42 39" fill="currentColor" opacity="0.1" />
      <rect x="84" y="58" width="32" height="26" rx="5" stroke="currentColor" strokeWidth="2.5" />
      <path d="M91 58v-9a9 9 0 0 1 18 0v9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="100" cy="70" r="3.5" fill="currentColor">
        <animate attributeName="opacity" values="1;0.3;1" dur="2.4s" repeatCount="indefinite" />
      </circle>
      <path d="M100 73.5v5" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </Scene>
  )
}

const SCENES: Record<string, (props: SceneProps) => React.ReactElement> = {
  'coffee-chat': CoffeeChat,
  'phone-call': PhoneCall,
  'clock-calendar': ClockCalendar,
  'confused-listener': ConfusedListener,
  'handshake-intro': HandshakeIntro,
  'food-order': FoodOrder,
  'city-transport': CityTransport,
  'map-directions': MapDirections,
  'money-receipt': MoneyReceipt,
  'desk-laptop': DeskLaptop,
  'team-standup': TeamStandup,
  'email-inbox': EmailInbox,
  'chat-message': ChatMessage,
  'video-call': VideoCall,
  'ticket-queue': TicketQueue,
  'server-rack': ServerRack,
  'outage-alert': OutageAlert,
  'network-diagram': NetworkDiagram,
  'backup-restore': BackupRestore,
  'shield-lock': ShieldLock,

  // Cloud, AI và đọc tài liệu kỹ thuật sống ở tệp riêng.
  ...TECH_SCENES,
}

interface LessonIllustrationProps extends SceneProps {
  /** Khoá lấy từ nội dung bài. Khoá lạ hoặc rỗng thì rơi về hình bàn làm việc. */
  name?: string | null
}

export function LessonIllustration({ name, ...props }: LessonIllustrationProps) {
  const Component = (name && SCENES[name]) || DeskLaptop
  return <Component {...props} />
}

export const ILLUSTRATION_KEYS = Object.keys(SCENES)
