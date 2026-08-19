/** Đề bài của một câu. Máy chủ không bao giờ gửi kèm đáp án nên ở đây cũng không có trường nào cho nó. */
export interface ChallengePrompt {
  PromptVi?: string
  PromptEn?: string
  AudioText?: string
  Choices: string[]
}

export interface ChallengeItem {
  code: string
  skill: string
  difficulty: number
  prompt: ChallengePrompt
}

export interface ChallengeOffer {
  lessonCode: string
  titleVi: string
  objectiveVi: string
  passThreshold: number
  items: ChallengeItem[]
  /** False thì chỉ hiện `reasonVi`, không hiện đề. */
  eligible: boolean
  reasonVi: string
  retryAt: string | null
}

export interface ChallengeResult {
  passed: boolean
  score: number
  passThreshold: number
  correctCount: number
  totalCount: number
  wrongItemCodes: string[]
  skillsBelowThreshold: string[]
  reviewItemsScheduled: number
  retryAt: string | null
  messageVi: string
}
