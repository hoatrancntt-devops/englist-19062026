import type { QuizPrompt } from '@/features/lesson/lesson-types'

/** Hợp đồng dữ liệu màn ôn tập. Khớp với ReviewService ở API. */

export interface ReviewCard {
  itemCode: string
  lessonCode: string
  lessonTitleVi: string
  /** Chỉ phần đề bài. Đáp án ở lại máy chủ cho tới khi nộp. */
  prompt: QuizPrompt
  /** Quá hạn bao nhiêu ngày. 0 nghĩa là đến hạn hôm nay. */
  overdueDays: number
  repetitionCount: number
}

export interface ReviewSession {
  cards: ReviewCard[]
  totalDue: number
  /** Chỉ có giá trị khi hàng đợi rỗng — cho biết bao giờ quay lại. */
  nextDueAt: string | null
  messageVi: string
}

export interface ReviewAnswerResult {
  correct: boolean
  correctIndex: number
  /** Số ngày tới lần ôn kế tiếp của chính câu vừa trả lời. */
  nextIntervalDays: number
  remainingDue: number
  messageVi: string
}
