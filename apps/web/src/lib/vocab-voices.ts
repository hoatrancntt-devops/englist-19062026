/**
 * Bộ giọng đọc dùng cho từ vựng.
 *
 * PHẢI khớp TtsCatalogue.VocabVoices ở máy chủ — máy chủ băm tên giọng vào khoá file, nên
 * một chữ lệch là nút nghe im lặng và lùi về giọng trình duyệt.
 *
 * Đặt ở một chỗ vì hai màn hình cùng dùng (thẻ từ trong bài học và trang bộ từ vựng). Trước
 * đây danh sách này bị chép ở cả hai nơi, và đổi giọng thì phải nhớ sửa hai lần.
 */
export const VOCAB_VOICES = [
  { id: 'en_US-lessac-medium', labelVi: 'Nữ · Mỹ' },
  { id: 'en_US-john-medium', labelVi: 'Nam · Mỹ' },
  { id: 'en_GB-alan-medium', labelVi: 'Nam · Anh' },
  { id: 'en_US-amy-medium', labelVi: 'Nữ · Mỹ 2' },
] as const

/**
 * Mã của một giọng.
 *
 * Cần khai riêng vì `as const` làm useState suy ra kiểu của ĐÚNG phần tử đầu tiên, nên đổi
 * giọng sẽ báo lỗi kiểu. Khai union này thì state nhận được cả bốn.
 */
export type VocabVoiceId = (typeof VOCAB_VOICES)[number]['id']

/**
 * Ba tốc độ nghe.
 *
 * Chậm để nghe rõ từng âm, thường để quen nhịp, nhanh để bắt kịp người bản xứ. Máy chủ chỉ
 * sinh một bản ở tốc độ chuẩn; nhanh chậm do trình duyệt chỉnh bằng playbackRate.
 */
export const VOCAB_SPEEDS = [
  { rate: 0.7, labelVi: 'Chậm' },
  { rate: 1, labelVi: 'Thường' },
  { rate: 1.25, labelVi: 'Nhanh' },
] as const
