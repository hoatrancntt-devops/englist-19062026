export interface VocabDeckSummary {
  code: string
  titleVi: string
  contextVi: string
  band: number
  totalWords: number
  learnedWords: number
  /** Số từ đã thuộc nhưng tới hạn ôn lại. */
  dueWords: number
}

export interface VocabWordView {
  id: string
  term: string
  ipa: string
  meaningVi: string
  chunk: string
  emoji: string | null
  mnemonicVi: string | null
  bestScore: number
  learned: boolean
  due: boolean
}

export interface VocabDeckView {
  code: string
  titleVi: string
  contextVi: string
  band: number
  passScore: number
  words: VocabWordView[]
}

export interface VocabWordResult {
  passed: boolean
  score: number
  passThreshold: number
  messageVi: string
  nextReviewInDays: number
}
