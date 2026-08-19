/** Một chương trong danh sách. Chương chưa mở KHÔNG kèm thân — máy chủ giữ lại. */
export interface StoryChapterSummary {
  code: string
  number: number
  titleVi: string
  hookVi: string
  track: string
  unlocked: boolean
  unlockedAt: string | null
  readAt: string | null
  /** Bài phải thông thạo để mở chương. Hiện nguyên văn khi chương còn khoá. */
  unlockAfterLessonCode: string
  unlockAfterLessonTitle: string | null
  newCharacters: string[]
}

/** Chương đã mở, có thân và câu kết. */
export interface StoryChapterDetail {
  code: string
  number: number
  titleVi: string
  hookVi: string
  bodyVi: string
  endsVi: string
  track: string
  readAt: string | null
  newCharacters: string[]
}
