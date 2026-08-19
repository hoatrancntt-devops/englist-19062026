/** Một lựa chọn hiện cho học viên. Máy chủ cố ý KHÔNG gửi kèm chất lượng. */
export interface RoleplayChoice {
  index: number
  en: string
  vi: string
}

export interface RoleplayTurn {
  nodeCode: string
  partnerLineEn: string
  partnerLineVi: string
  choices: RoleplayChoice[]
  isTerminal: boolean
  summaryVi: string | null
  success: boolean | null
}

export interface RoleplaySummary {
  code: string
  titleVi: string
  contextVi: string
  partnerName: string
  track: string
  level: string
  turnCount: number
  lastOutcome: string | null
  lastScore: number | null
}

export interface RoleplayStart {
  attemptId: string
  scenario: RoleplaySummary
  turn: RoleplayTurn
}

export interface RoleplayResult {
  outcome: string
  score: number
  goodChoices: number
  curtChoices: number
  wrongChoices: number
  messageVi: string
}

export interface RoleplayAnswerResult {
  /** Rỗng khi lựa chọn đạt. Có giá trị khi cộc lốc hoặc sai hướng. */
  feedbackVi: string | null
  quality: string
  next: RoleplayTurn
  result: RoleplayResult | null
}
