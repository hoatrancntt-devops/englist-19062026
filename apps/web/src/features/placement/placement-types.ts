/** Hợp đồng dữ liệu bài xếp lớp. Khớp với PlacementService ở API. */

/** Chín dạng câu. Bốn dạng nói chưa được phục vụ cho tới khi có dịch vụ chấm phát âm. */
export type PlacementItemKind =
  | 'Mcq'
  | 'McqRead'
  | 'Likert'
  | 'ReadAloud'
  | 'Repeat'
  | 'FillBlank'
  | 'ErrorCorrection'
  | 'GuidedEmail'
  | 'ShortAnswer'

/**
 * Phần đề bài.
 *
 * Mọi trường ở đây đều đi ra client, nên đáp án đúng KHÔNG có mặt trong kiểu này.
 * Thêm trường mới vào đây phải kiểm lại là nó không gợi ý đáp án.
 */
export interface PlacementPrompt {
  instructionVi: string
  choices?: string[]
  audioText?: string
  speed?: number
  passageEn?: string
  questionEn?: string
  sentenceEn?: string
  scenarioVi?: string
  requiredPointsVi?: string[]
  targetEn?: string
}

export interface PlacementCard {
  itemCode: string
  kind: PlacementItemKind
  skill: 'Listening' | 'Speaking' | 'Reading' | 'Writing' | null
  prompt: PlacementPrompt
}

export interface PlacementSession {
  attemptId: string
  formCode: string
  titleVi: string
  cards: PlacementCard[]
  deadlineAt: string
  /** Mã các câu đã trả lời. Mở lại giữa chừng thì nhảy đúng chỗ đang dở. */
  answeredItemCodes: string[]
  resumed: boolean
  messageVi: string
}

export interface PlacementProgress {
  answered: number
  total: number
  deadlineAt: string
}

export interface PlacementResult {
  attemptId: string
  formCode: string
  /** L0 tới L4. Đây là thứ hiển thị cho học viên. */
  band: string
  /** Bậc engine dùng để mở khoá: PreA1, A1, A2, B1. */
  level: string
  suggestedLayer: 'Life' | 'Office' | 'Professional'
  skillScores: Record<string, number>
  /** Kỹ năng chưa đo được. Hiện rõ chứ không hiển thị 0 điểm. */
  unmeasuredSkills: string[]
  vocabGrammarScore: number
  overallScore: number
  fastAnswerRatio: number
  selfRatedScore: number
  answered: number
  total: number
  submittedAt: string
  notesVi: string[]
  summaryVi: string
}

/** Câu trả lời gửi lên: chọn đáp án thì có choiceIndex, viết tay thì có text. */
export type PlacementResponse = { choiceIndex: number } | { text: string }

/**
 * Id của đoạn đề bài hiện phía trên câu hỏi. Các ô nhập trỏ tới đây làm nhãn.
 *
 * Đặt ở file kiểu chứ không ở file component: file chỉ export component thì
 * fast refresh mới hoạt động, và một hàm phụ nằm nhầm chỗ là đủ để tắt nó.
 */
export function promptLabelId(itemCode: string): string {
  return `prompt-${itemCode}`
}