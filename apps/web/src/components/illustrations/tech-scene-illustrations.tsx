/**
 * Cảnh minh hoạ cho ba nhánh chuyên môn sâu: Cloud, AI và Đọc tài liệu kỹ thuật.
 *
 * Tách khỏi tệp cảnh đời sống/văn phòng vì hai lý do thực tế: một tệp chín trăm
 * dòng SVG rất khó dò khi cần sửa đúng một hình, và hai nhóm cảnh này gần như
 * không bao giờ được sửa cùng lúc.
 *
 * Quy ước giống hệt tệp kia: nét chính dùng currentColor, mảng nền dùng opacity,
 * mọi hình vẽ trong khung 200×120. Khoá phải khớp IllustrationCatalogue phía máy chủ.
 */

import { Scene, type SceneProps } from './scene-frame'

/** Ba tầng triển khai chồng lên nhau — chọn VM, container hay serverless. */
function CloudStack(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Đám mây bao ngoài */}
      <path
        d="M52 44a20 20 0 0 1 39-6 15 15 0 0 1 22 6 16 16 0 0 1-2 32H56a16 16 0 0 1-4-32Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
        opacity="0.35"
      />
      {/* Ba tầng: máy ảo, container, hàm */}
      {[
        { y: 52, w: 84, label: 8 },
        { y: 70, w: 68, label: 6 },
        { y: 88, w: 52, label: 4 },
      ].map((tier, i) => (
        <g key={tier.y}>
          <rect
            x={100 - tier.w / 2}
            y={tier.y}
            width={tier.w}
            height="14"
            rx="4"
            stroke="currentColor"
            strokeWidth="2.5"
          />
          <rect
            x={100 - tier.w / 2}
            y={tier.y}
            width={tier.w}
            height="14"
            rx="4"
            fill="currentColor"
            opacity={0.08 + i * 0.06}
          />
          <circle cx={100 - tier.w / 2 + 9} cy={tier.y + 7} r="2.5" fill="currentColor">
            <animate
              attributeName="opacity"
              values="0.3;1;0.3"
              dur="2.4s"
              begin={`${i * 0.4}s`}
              repeatCount="indefinite"
            />
          </circle>
        </g>
      ))}
      {/* Mũi tên: càng xuống càng ít phải tự quản */}
      <path d="M162 54v46" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      <path d="M157 94l5 6 5-6" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" opacity="0.5" />
    </Scene>
  )
}

/** Hai vùng chạy song song, một vùng đứt thì lưu lượng đổ sang vùng kia. */
function HaFailover(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Vùng chính */}
      <rect x="16" y="42" width="58" height="50" rx="8" stroke="currentColor" strokeWidth="2.5" />
      <rect x="16" y="42" width="58" height="50" rx="8" fill="currentColor" opacity="0.1" />
      {[54, 66, 78].map((y) => (
        <path key={y} d={`M28 ${y}h34`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      ))}
      {/* Vùng dự phòng */}
      <rect
        x="126"
        y="42"
        width="58"
        height="50"
        rx="8"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeDasharray="7 5"
      />
      {[54, 66, 78].map((y) => (
        <path
          key={y}
          d={`M138 ${y}h34`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          opacity="0.45"
        />
      ))}
      {/* Bộ cân tải phía trên chia lưu lượng */}
      <circle cx="100" cy="20" r="11" stroke="currentColor" strokeWidth="2.5" />
      <path d="M100 31v10" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M100 41H45v1M100 41h55v1" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Lưu lượng chuyển hướng khi vùng chính hỏng */}
      <circle cx="100" cy="41" r="4" fill="currentColor">
        <animate attributeName="cx" values="100;45;100;155;100" dur="4s" repeatCount="indefinite" />
        <animate attributeName="opacity" values="1;0.2;1;0.2;1" dur="4s" repeatCount="indefinite" />
      </circle>
    </Scene>
  )
}

/** Đường tải tăng dần và số phiên bản chạy tăng theo — scaling và chi phí. */
function ScaleGraph(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Trục */}
      <path d="M28 22v76h150" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Đường tải */}
      <path
        d="M28 88c22 0 26-28 44-28s24 22 40 22 22-34 42-34 14 12 24 12"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M28 88c22 0 26-28 44-28s24 22 40 22 22-34 42-34 14 12 24 12v38H28Z"
        fill="currentColor"
        opacity="0.1"
      />
      {/* Số phiên bản chạy dưới trục, mọc thêm khi tải lên */}
      {[52, 70, 88, 106, 124, 142].map((x, i) => (
        <rect key={x} x={x} y="80" width="10" height="18" rx="2" fill="currentColor" opacity="0.3">
          <animate
            attributeName="opacity"
            values="0.08;0.55;0.08"
            dur="3.6s"
            begin={`${i * 0.35}s`}
            repeatCount="indefinite"
          />
        </rect>
      ))}
      {/* Ngưỡng tự mở rộng */}
      <path d="M28 50h150" stroke="currentColor" strokeWidth="2" strokeDasharray="6 5" opacity="0.45" />
    </Scene>
  )
}

/** Bảng trắng có sơ đồ, hai người đứng chỉ và hỏi — buổi review kiến trúc. */
function ArchitectureReview(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Bảng */}
      <rect x="40" y="12" width="120" height="72" rx="6" stroke="currentColor" strokeWidth="2.5" />
      {/* Sơ đồ trên bảng */}
      <rect x="54" y="26" width="26" height="18" rx="3" stroke="currentColor" strokeWidth="2.5" />
      <rect x="98" y="26" width="26" height="18" rx="3" stroke="currentColor" strokeWidth="2.5" />
      <rect x="76" y="56" width="26" height="18" rx="3" stroke="currentColor" strokeWidth="2.5" />
      <path d="M80 35h18M89 44v12M111 44v6H102" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Dấu hỏi: chỗ chưa nêu được đánh đổi */}
      <circle cx="139" cy="62" r="11" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      <path
        d="M135 58a4 4 0 0 1 8 0c0 3-4 3-4 6"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
      >
        <animate attributeName="opacity" values="0.3;1;0.3" dur="2.2s" repeatCount="indefinite" />
      </path>
      <circle cx="139" cy="69" r="1.8" fill="currentColor" />
      {/* Hai người đứng nghe */}
      {[18, 182].map((cx) => (
        <g key={cx}>
          <circle cx={cx} cy="72" r="8" stroke="currentColor" strokeWidth="2.5" />
          <path
            d={`M${cx - 11} 104a11 11 0 0 1 22 0`}
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
          />
        </g>
      ))}
    </Scene>
  )
}

/** Hộp máy chủ tại chỗ chuyển dần lên đám mây — kế hoạch migration theo đợt. */
function CloudMigration(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Đám mây đích */}
      <path
        d="M120 34a17 17 0 0 1 33-5 13 13 0 0 1 19 5 14 14 0 0 1-2 27h-48a14 14 0 0 1-2-27Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      {/* Máy chủ tại chỗ */}
      <rect x="16" y="48" width="44" height="52" rx="5" stroke="currentColor" strokeWidth="2.5" />
      {[58, 72, 86].map((y) => (
        <g key={y}>
          <path d={`M24 ${y}h28`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
          <circle cx="56" cy={y} r="2" fill="currentColor" opacity="0.6" />
        </g>
      ))}
      {/* Ba đợt chuyển, không phải một lần */}
      <path
        d="M64 62c22-6 34-6 52-8"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeDasharray="6 6"
        strokeLinecap="round"
      />
      {[0, 1, 2].map((i) => (
        <rect key={i} x="64" y="56" width="12" height="12" rx="3" fill="currentColor" opacity="0.7">
          <animate
            attributeName="x"
            values="64;116"
            dur="3.2s"
            begin={`${i * 1.05}s`}
            repeatCount="indefinite"
          />
          <animate
            attributeName="y"
            values="58;50"
            dur="3.2s"
            begin={`${i * 1.05}s`}
            repeatCount="indefinite"
          />
          <animate
            attributeName="opacity"
            values="0;0.75;0"
            dur="3.2s"
            begin={`${i * 1.05}s`}
            repeatCount="indefinite"
          />
        </rect>
      ))}
      {/* Đường lui: đợt nào cũng phải quay về được */}
      <path
        d="M118 86c-18 4-32 4-50 6"
        stroke="currentColor"
        strokeWidth="2"
        strokeDasharray="4 6"
        strokeLinecap="round"
        opacity="0.45"
      />
      <path d="M73 90l-5 2 4 4" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" opacity="0.45" />
    </Scene>
  )
}

/** Bộ não mạch điện — mô hình AI, dùng cho bài trình bày use case. */
function AiBrain(props: SceneProps) {
  return (
    <Scene {...props}>
      <path
        d="M100 18c-16 0-28 10-28 22 0 4-6 8-6 16s6 12 6 16c0 12 12 22 28 22s28-10 28-22c0-4 6-8 6-16s-6-12-6-16c0-12-12-22-28-22Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path d="M100 18v76" stroke="currentColor" strokeWidth="2.5" opacity="0.35" />
      {/* Nút mạng, sáng lần lượt như đang suy luận */}
      {[
        [84, 40],
        [116, 40],
        [78, 60],
        [122, 60],
        [90, 78],
        [110, 78],
      ].map(([cx, cy], i) => (
        <circle key={`${cx}-${cy}`} cx={cx} cy={cy} r="4" fill="currentColor">
          <animate
            attributeName="opacity"
            values="0.2;1;0.2"
            dur="2.6s"
            begin={`${i * 0.32}s`}
            repeatCount="indefinite"
          />
        </circle>
      ))}
      <path
        d="M84 40 78 60l12 18M116 40l6 20-12 18"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        opacity="0.5"
      />
      {/* Đầu vào và đầu ra: có vào có ra thì mới là use case */}
      <path d="M18 58h44M138 58h44" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M175 53l7 5-7 5" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
    </Scene>
  )
}

/** Tài liệu đi qua các bước xử lý rồi vào kho vector — luồng RAG. */
function DataPipeline(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Nguồn tài liệu */}
      <rect x="14" y="38" width="30" height="40" rx="4" stroke="currentColor" strokeWidth="2.5" />
      <path d="M22 50h14M22 58h14M22 66h9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Hai bước xử lý */}
      {[62, 108].map((x, i) => (
        <g key={x}>
          <rect x={x} y="42" width="32" height="32" rx="6" stroke="currentColor" strokeWidth="2.5" />
          <rect x={x} y="42" width="32" height="32" rx="6" fill="currentColor" opacity="0.1" />
          <path
            d={`M${x + 9} 58h14`}
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            opacity={0.5 + i * 0.3}
          />
        </g>
      ))}
      {/* Kho vector */}
      <ellipse cx="172" cy="46" rx="16" ry="6" stroke="currentColor" strokeWidth="2.5" />
      <path d="M156 46v26c0 3 7 6 16 6s16-3 16-6V46" stroke="currentColor" strokeWidth="2.5" />
      <path d="M156 60c0 3 7 6 16 6s16-3 16-6" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
      {/* Dòng chảy */}
      <path d="M44 58h18M94 58h14M140 58h16" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {[0, 1, 2].map((i) => (
        <circle key={i} cy="58" r="3.5" fill="currentColor">
          <animate
            attributeName="cx"
            values="46;156"
            dur="3s"
            begin={`${i}s`}
            repeatCount="indefinite"
          />
          <animate
            attributeName="opacity"
            values="0;1;1;0"
            dur="3s"
            begin={`${i}s`}
            repeatCount="indefinite"
          />
        </circle>
      ))}
      {/* Câu hỏi đi ngược lên để tra */}
      <path
        d="M172 84c0 8-40 12-92 12"
        stroke="currentColor"
        strokeWidth="2"
        strokeDasharray="5 5"
        strokeLinecap="round"
        opacity="0.5"
      />
    </Scene>
  )
}

/** Bảng theo dõi ba số: độ trễ, độ chính xác, chi phí — không nhìn thì không biết mô hình trôi. */
function MetricsDashboard(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="14" y="16" width="172" height="88" rx="8" stroke="currentColor" strokeWidth="2.5" />
      <path d="M14 34h172" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
      {[26, 36, 46].map((cx) => (
        <circle key={cx} cx={cx} cy="25" r="2.5" fill="currentColor" opacity="0.5" />
      ))}
      {/* Ô thứ nhất: đường độ trễ */}
      <rect x="26" y="44" width="46" height="48" rx="5" stroke="currentColor" strokeWidth="2" opacity="0.6" />
      <path
        d="M32 80l10-12 8 8 10-16 6 6"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      {/* Ô thứ hai: cột độ chính xác */}
      <rect x="80" y="44" width="46" height="48" rx="5" stroke="currentColor" strokeWidth="2" opacity="0.6" />
      {[86, 96, 106, 116].map((x, i) => (
        <rect key={x} x={x} y={84 - i * 8} width="6" height={4 + i * 8} rx="1.5" fill="currentColor" opacity="0.6">
          <animate
            attributeName="opacity"
            values="0.2;0.75;0.2"
            dur="3s"
            begin={`${i * 0.3}s`}
            repeatCount="indefinite"
          />
        </rect>
      ))}
      {/* Ô thứ ba: một con số lớn và cảnh báo */}
      <rect x="134" y="44" width="46" height="48" rx="5" stroke="currentColor" strokeWidth="2" opacity="0.6" />
      <path d="M144 66h26M144 76h18" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M157 50l8 12h-16l8-12Z" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round">
        <animate attributeName="opacity" values="0.3;1;0.3" dur="1.8s" repeatCount="indefinite" />
      </path>
    </Scene>
  )
}

/** Lưới hai chiều khả năng xảy ra × mức tác động — bảng đánh giá rủi ro. */
function RiskMatrix(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Lưới 3×3 */}
      <rect x="46" y="14" width="120" height="90" rx="6" stroke="currentColor" strokeWidth="2.5" />
      {[44, 74].map((y) => (
        <path key={y} d={`M46 ${y}h120`} stroke="currentColor" strokeWidth="2" opacity="0.45" />
      ))}
      {[86, 126].map((x) => (
        <path key={x} d={`M${x} 14v90`} stroke="currentColor" strokeWidth="2" opacity="0.45" />
      ))}
      {/* Góc trên phải là ô nguy hiểm nhất */}
      <rect x="126" y="14" width="40" height="30" fill="currentColor" opacity="0.28" />
      <rect x="86" y="14" width="40" height="30" fill="currentColor" opacity="0.15" />
      <rect x="126" y="44" width="40" height="30" fill="currentColor" opacity="0.15" />
      {/* Các rủi ro đã chấm điểm */}
      {[
        [106, 30],
        [146, 28],
        [66, 88],
        [146, 60],
      ].map(([cx, cy], i) => (
        <circle key={`${cx}-${cy}`} cx={cx} cy={cy} r="5" fill="currentColor">
          <animate
            attributeName="opacity"
            values="0.35;1;0.35"
            dur="2.8s"
            begin={`${i * 0.45}s`}
            repeatCount="indefinite"
          />
        </circle>
      ))}
      {/* Hai trục */}
      <path d="M34 104V20" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M29 26l5-7 5 7" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M46 114h114" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M154 109l7 5-7 5" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
    </Scene>
  )
}

/** Cửa sổ log đang trôi, một dòng lỗi nổi bật — bài đọc log tìm nguyên nhân. */
function LogLines(props: SceneProps) {
  return (
    <Scene {...props}>
      <rect x="14" y="14" width="172" height="92" rx="8" stroke="currentColor" strokeWidth="2.5" />
      <path d="M14 32h172" stroke="currentColor" strokeWidth="2.5" opacity="0.5" />
      {[26, 36, 46].map((cx) => (
        <circle key={cx} cx={cx} cy="23" r="2.5" fill="currentColor" opacity="0.5" />
      ))}
      {/* Dòng bình thường: mốc thời gian ngắn rồi nội dung dài */}
      {[44, 56, 80, 92].map((y, i) => (
        <g key={y}>
          <path d={`M28 ${y}h16`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.35" />
          <path
            d={`M50 ${y}h${94 - i * 12}`}
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            opacity="0.35"
          />
        </g>
      ))}
      {/* Dòng lỗi: cái duy nhất cần tìm giữa hàng nghìn dòng */}
      <rect x="24" y="62" width="152" height="12" rx="3" fill="currentColor" opacity="0.2">
        <animate attributeName="opacity" values="0.1;0.32;0.1" dur="2.4s" repeatCount="indefinite" />
      </rect>
      <path d="M28 68h16M50 68h108" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <path d="M166 62v12" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </Scene>
  )
}

/** Trang ghi chú phát hành, có mục thay đổi phá vỡ tương thích được đánh dấu. */
function ReleaseNotes(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Trang tài liệu */}
      <path
        d="M42 10h84l32 30v70a6 6 0 0 1-6 6H42a6 6 0 0 1-6-6V16a6 6 0 0 1 6-6Z"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <path d="M126 10v30h32" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" />
      {/* Số phiên bản */}
      <rect x="50" y="28" width="42" height="14" rx="4" fill="currentColor" opacity="0.22" />
      <path d="M58 35h26" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Danh sách thay đổi */}
      {[56, 70, 84].map((y) => (
        <g key={y}>
          <circle cx="54" cy={y} r="2.5" fill="currentColor" opacity="0.55" />
          <path d={`M64 ${y}h74`} stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.4" />
        </g>
      ))}
      {/* Mục breaking change: thứ phải tìm trước khi nâng cấp */}
      <path d="M54 96l6 6 6-6" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" opacity="0" />
      <rect x="46" y="92" width="106" height="14" rx="4" fill="currentColor" opacity="0.2">
        <animate attributeName="opacity" values="0.08;0.3;0.08" dur="2.6s" repeatCount="indefinite" />
      </rect>
      <path d="M56 99h6M70 99h68" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </Scene>
  )
}

/** Khối tài liệu API: đường dẫn, phương thức và ví dụ phản hồi. */
function ApiDoc(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Cột mục lục bên trái */}
      <rect x="14" y="16" width="42" height="88" rx="6" stroke="currentColor" strokeWidth="2.5" opacity="0.55" />
      {[30, 42, 54, 66, 78].map((y, i) => (
        <path
          key={y}
          d={`M24 ${y}h${24 - (i % 2) * 8}`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          opacity={i === 2 ? 1 : 0.35}
        />
      ))}
      {/* Dòng endpoint */}
      <rect x="66" y="20" width="120" height="20" rx="5" stroke="currentColor" strokeWidth="2.5" />
      <rect x="72" y="25" width="26" height="10" rx="3" fill="currentColor" opacity="0.55" />
      <path d="M106 30h68" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" opacity="0.5" />
      {/* Khối ví dụ phản hồi, thụt đầu dòng như JSON */}
      <rect x="66" y="48" width="120" height="56" rx="5" stroke="currentColor" strokeWidth="2.5" opacity="0.6" />
      {[
        [76, 60, 20],
        [86, 72, 46],
        [86, 84, 36],
        [76, 96, 14],
      ].map(([x, y, w], i) => (
        <path
          key={y}
          d={`M${x} ${y}h${w}`}
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          opacity="0.45"
        >
          <animate
            attributeName="opacity"
            values="0.2;0.65;0.2"
            dur="3.2s"
            begin={`${i * 0.4}s`}
            repeatCount="indefinite"
          />
        </path>
      ))}
    </Scene>
  )
}

/** Mốc thời gian sự cố: điểm kích hoạt, điểm phát hiện, điểm khắc phục. */
function PostmortemTimeline(props: SceneProps) {
  return (
    <Scene {...props}>
      {/* Trục thời gian */}
      <path d="M18 62h164" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      {/* Bốn mốc, mốc thứ hai là lúc phát hiện */}
      {[
        { x: 42, up: true },
        { x: 86, up: false },
        { x: 130, up: true },
        { x: 168, up: false },
      ].map((mark, i) => (
        <g key={mark.x}>
          <circle cx={mark.x} cy="62" r="6" stroke="currentColor" strokeWidth="2.5" fill="none" />
          <circle cx={mark.x} cy="62" r="2.5" fill="currentColor">
            <animate
              attributeName="opacity"
              values="0.3;1;0.3"
              dur="3s"
              begin={`${i * 0.5}s`}
              repeatCount="indefinite"
            />
          </circle>
          <path
            d={`M${mark.x} ${mark.up ? 56 : 68}v${mark.up ? -14 : 14}`}
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            opacity="0.6"
          />
          <rect
            x={mark.x - 20}
            y={mark.up ? 18 : 84}
            width="40"
            height="24"
            rx="5"
            stroke="currentColor"
            strokeWidth="2.5"
          />
          <path
            d={`M${mark.x - 12} ${mark.up ? 27 : 93}h24M${mark.x - 12} ${mark.up ? 34 : 100}h14`}
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            opacity="0.45"
          />
        </g>
      ))}
      {/* Khoảng từ lúc hỏng tới lúc phát hiện — chỗ postmortem hay bỏ sót */}
      <path
        d="M42 62h44"
        stroke="currentColor"
        strokeWidth="6"
        strokeLinecap="round"
        opacity="0.2"
      />
    </Scene>
  )
}

/** Cảnh chuyên môn sâu, gộp vào bảng tra chung ở scene-illustrations.tsx. */
export const TECH_SCENES: Record<string, (props: SceneProps) => React.ReactElement> = {
  'cloud-stack': CloudStack,
  'ha-failover': HaFailover,
  'scale-graph': ScaleGraph,
  'architecture-review': ArchitectureReview,
  'cloud-migration': CloudMigration,
  'ai-brain': AiBrain,
  'data-pipeline': DataPipeline,
  'metrics-dashboard': MetricsDashboard,
  'risk-matrix': RiskMatrix,
  'log-lines': LogLines,
  'release-notes': ReleaseNotes,
  'api-doc': ApiDoc,
  'postmortem-timeline': PostmortemTimeline,
}
