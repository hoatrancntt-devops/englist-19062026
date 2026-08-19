import type { Skill } from '@/components/skill-badge'

/** Hợp đồng dữ liệu của màn học. Khớp với PlayerLesson ở API. */

export type ActivityKind = 'Listen' | 'Vocab' | 'Shadow' | 'Speak' | 'Read' | 'Write' | 'Quiz'

export interface QuizPrompt {
  Kind: string
  PromptVi?: string
  PromptEn?: string
  AudioText?: string
  Choices: string[]
  ReadingSkill?: string
  Skill: Skill
}

export interface LessonItem {
  code: string
  orderIndex: number
  difficulty: number
  /** Chỉ phần đề bài. Đáp án không bao giờ rời máy chủ. */
  prompt: QuizPrompt
}

export interface LessonActivity {
  id: string
  kind: ActivityKind
  skill: Skill
  orderIndex: number
  passScore: number
  payload: Record<string, unknown>
  items: LessonItem[]
}

export interface VocabularyEntry {
  Term: string
  Ipa: string
  MeaningVi: string
  Chunk: string
}

export interface DialogueTurn {
  Speaker: string
  En: string
  Vi: string
}

export interface LessonBody {
  Vocabulary?: VocabularyEntry[]
  SentencePatterns?: { Pattern: string; MeaningVi: string }[]
  Dialogue?: { ContextVi: string; Turns: DialogueTurn[] }
  MemoryTrickVi?: string
}

export interface Explanation {
  WhyVi: string
  HowVi: string
  ContrastVi?: string
}

export interface CommonMistake {
  Mistake: string
  WhyVi: string
  FixVi: string
}

export interface PlayerLesson {
  code: string
  titleVi: string
  titleEn: string
  level: string
  layer: string
  track: string
  estimatedMinutes: number
  objectiveVi: string
  /** Khoá hình minh hoạ, tra ra một component SVG nhúng sẵn. */
  illustration: string | null
  body: LessonBody
  explanation: Explanation
  commonMistakes: CommonMistake[]
  activities: LessonActivity[]
  state: string
  mastery: number
  lockExplanationVi: string
  resumeAtActivityIndex: number
}

export interface GradedItem {
  itemCode: string
  correct: boolean
  chosenIndex: number
  correctIndex: number
}

export interface ActivityGrade {
  score: number
  passed: boolean
  items: GradedItem[]
  feedbackVi: string
  /** False khi hệ thống chưa chấm được bước này (hiện là phần Nói). */
  graded: boolean
  sampleEn?: string | null
}

export interface LessonSubmissionResult {
  score: number
  state: string
  skillScores: Record<string, number>
  skillsBelowThreshold: string[]
  reviewItemsScheduled: number
  messageVi: string
}

export const ACTIVITY_LABEL: Record<ActivityKind, string> = {
  Listen: 'Nghe',
  Vocab: 'Từ vựng',
  Shadow: 'Nhắc lại',
  Speak: 'Nói',
  Read: 'Đọc',
  Write: 'Viết',
  Quiz: 'Kiểm tra',
}
