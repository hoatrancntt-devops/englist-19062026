export interface WritingSetSummary {
  code: string
  titleVi: string
  contextVi: string
  track: string
  level: string
  taskCount: number
  passedCount: number
}

/** Đề của một bài. Không có đáp án — chúng nằm ở cột rubric phía máy chủ. */
export interface WritingTaskView {
  code: string
  kind: 'FillBlank' | 'Reorder' | 'GuidedEmail'
  passScore: number
  promptVi: string
  promptEn: string | null
  hintVi: string | null
  /** Dạng Reorder: các mảnh theo thứ tự đã xáo sẵn trong file nội dung. */
  fragments: string[]
  /** Dạng FillBlank: số ô cần nhập. Chỉ là con số. */
  blankCount: number
  lastScore: number | null
  lastPassed: boolean | null
}

export interface WritingSetDetail {
  code: string
  titleVi: string
  contextVi: string
  track: string
  level: string
  tasks: WritingTaskView[]
}

/** Câu mẫu chỉ có mặt ở đây, tức là sau khi đã nộp. */
export interface WritingSubmitResult {
  score: number
  passed: boolean
  feedbackVi: string
  sampleEn: string
}
