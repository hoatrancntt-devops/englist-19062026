import type { Skill } from '@/components/skill-badge'

/**
 * Hợp đồng dữ liệu của bảng điều khiển học viên.
 * Đây là hình dạng mà GET /api/v1/learning/dashboard trả về — giữ khớp với DashboardResponse ở API.
 */

export type StudyMode = 'Mixed' | 'ListeningOnly' | 'SpeakingOnly' | 'ReadingOnly' | 'WritingOnly'
export type ContextLayer = 'Life' | 'Office' | 'Professional'
export type CefrLevel = 'PreA1' | 'A1' | 'A2' | 'B1'

export interface NextLesson {
  code: string
  titleVi: string
  track: string
  layer: ContextLayer
  level: CefrLevel
  estimatedMinutes: number
  /** Vì sao hệ thống chọn đúng bài này. Hiển thị nguyên văn, không diễn giải lại ở client. */
  reasonVi: string
  supportedSkills: Skill[]
  /** Khoá hình minh hoạ, tra ra một component SVG nhúng sẵn. */
  illustration: string | null
}

export interface MilestoneProgress {
  key: string
  labelVi: string
  achieved: boolean
  /** 0-100. Cho người học thấy còn bao xa thay vì chỉ đạt hay chưa đạt. */
  progressPercent: number
  requirementVi: string
}

export interface DashboardData {
  displayName: string
  studyMode: StudyMode
  currentLevel: CefrLevel
  currentLayer: ContextLayer

  /** Chưa làm bài xếp lớp thì frontend đẩy thẳng sang trang xếp lớp. */
  placementCompleted: boolean

  streak: {
    current: number
    longest: number
    freezeTokens: number
    studiedToday: boolean
  }

  skillScores: Record<Skill, number>

  progress: {
    lessonsMastered: number
    lessonsTotal: number
    lessonsInProgress: number
    minutesStudiedLast7Days: number
    /** Ước lượng từ nhịp học thật, không phải từ hằng số. Null khi chưa đủ dữ liệu. */
    estimatedDaysRemaining: number | null
  }

  reviewDueCount: number
  nextLesson: NextLesson | null
  milestones: MilestoneProgress[]
}
