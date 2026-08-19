/** Hình dạng dữ liệu của khu quản trị, khớp với `/api/v1/admin/*`. */

export interface AdminOverview {
  learners: number
  lessonsPublished: number
  placementForms: number
  roleplayScenarios: number
  lessonItems: number
  activeSessions: number
  outbox: { pending: number; sent: number; failed: number; lastError: string | null }
  content: ContentHealth
}

/** Những thứ hỏng âm thầm mà không màn nào khác cho thấy. */
export interface ContentHealth {
  lessonsWithoutItems: number
  orphanReviewItems: number
  /** Vị trí đáp án xuất hiện nhiều nhất, hoặc -1 khi chưa có câu nào có đáp án dạng chỉ số. */
  mostCommonAnswerPosition: number
  mostCommonAnswerCount: number
  totalAnswerableItems: number
}

export interface MailSettings {
  enabled: boolean
  provider: string
  fromAddress: string
  fromDisplayName: string
  smtpHost: string | null
  smtpPort: number | null
  smtpUseStartTls: boolean
  smtpUsername: string | null
  /** Chỉ cho biết CÓ mật khẩu hay không — máy chủ không bao giờ trả giá trị. */
  hasPassword: boolean
  lastTestAt: string | null
  lastTestSucceeded: boolean | null
  lastTestError: string | null
}

export interface AiStatus {
  budgetMode: string
  spentThisMonthUsd: number
  monthlyCapUsd: number
  providers: { provider: string; enabled: boolean; hasKey: boolean; baseUrl: string | null }[]
  cacheEntries: number
  callsThisMonth: number
  cacheHitsThisMonth: number
}

export interface SeedOutcome {
  inserted: number
  updated: number
  unchanged: number
  problems: string[]
}

export interface ReseedResult {
  lessons: SeedOutcome
  placement: SeedOutcome
  roleplay: SeedOutcome
}

export interface AuditEntry {
  createdAt: string
  action: string
  targetType: string | null
  targetId: string | null
  metadataJson: string | null
}

/** Một bài trong đồ thị, kèm số chỉ tính được khi nhìn cả đồ thị. */
export interface GraphNode {
  code: string
  titleVi: string
  track: string
  level: string
  layer: string
  status: string
  unitCode: string | null
  orderIndex: number
  isCheckpoint: boolean
  activities: number
  items: number
  /** Số bài phải học xong trước, tính theo đường DÀI nhất. */
  depth: number
  /** Số bài bị bài này chặn, tính cả gián tiếp. */
  gates: number
}

export interface GraphEdge {
  from: string
  to: string
  kind: string
  minMastery: number
}

export interface GraphProblem {
  code: string
  severity: 'error' | 'warning' | 'info'
  /** Rỗng khi vấn đề thuộc về cả đồ thị chứ không riêng bài nào. */
  lessonCode: string
  message: string
}

export interface ContentGraph {
  nodes: GraphNode[]
  edges: GraphEdge[]
  problems: GraphProblem[]
  maxDepth: number
}
