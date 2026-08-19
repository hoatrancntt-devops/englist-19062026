import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Đọc văn bản tiếng Anh bằng giọng có sẵn trong trình duyệt.
 *
 * Đây là giải pháp TẠM cho bước Nghe, không phải thiết kế cuối. Bản phát hành
 * dùng audio sinh sẵn bằng Piper lúc seed, vì ba lý do: giọng đồng nhất giữa mọi
 * học viên, chi phí runtime bằng 0, và nghe được cả khi trình duyệt không hỗ trợ
 * tổng hợp giọng nói. Nhưng để bước Nghe dùng được ngay từ hôm nay thì đây là
 * cách duy nhất không cần thêm hạ tầng nào.
 *
 * Hai chỗ đã xử lý sẵn:
 *  - Danh sách giọng nạp bất đồng bộ, nên phải nghe sự kiện voiceschanged.
 *  - Chrome cắt ngang câu dài sau khoảng mười lăm giây, nên chia theo câu.
 */
export function useSpeech() {
  const [supported, setSupported] = useState(false)
  const [speaking, setSpeaking] = useState(false)
  const [voice, setVoice] = useState<SpeechSynthesisVoice | null>(null)
  const queueRef = useRef<SpeechSynthesisUtterance[]>([])

  useEffect(() => {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) {
      return
    }

    setSupported(true)

    const pickVoice = () => {
      const voices = window.speechSynthesis.getVoices()
      if (voices.length === 0) {
        return
      }

      // Ưu tiên giọng Anh Mỹ, rồi tới bất kỳ giọng tiếng Anh nào.
      // Giọng của hệ điều hành khác nhau giữa Windows, macOS và Android,
      // nên không thể chỉ định đích danh một tên giọng.
      const english = voices.filter((v) => v.lang.toLowerCase().startsWith('en'))
      const preferred =
        english.find((v) => v.lang.toLowerCase() === 'en-us' && v.localService) ??
        english.find((v) => v.lang.toLowerCase() === 'en-us') ??
        english[0] ??
        null

      setVoice(preferred)
    }

    pickVoice()
    window.speechSynthesis.addEventListener('voiceschanged', pickVoice)

    return () => {
      window.speechSynthesis.removeEventListener('voiceschanged', pickVoice)
      window.speechSynthesis.cancel()
    }
  }, [])

  const stop = useCallback(() => {
    if (!supported) {
      return
    }

    queueRef.current = []
    window.speechSynthesis.cancel()
    setSpeaking(false)
  }, [supported])

  /**
   * Đọc một đoạn. Tốc độ lấy từ bài học: bậc thấp 0.8, bậc cao 1.3.
   * Gọi lại khi đang đọc thì dừng đoạn cũ — học viên bấm nghe lại thường xuyên.
   */
  const speak = useCallback(
    (text: string, rate = 1) => {
      if (!supported || text.trim().length === 0) {
        return
      }

      window.speechSynthesis.cancel()

      // Chia theo câu để Chrome không cắt ngang đoạn dài.
      const sentences = text
        .split(/(?<=[.!?])\s+/)
        .map((s) => s.trim())
        .filter(Boolean)

      const utterances = sentences.map((sentence) => {
        const utterance = new SpeechSynthesisUtterance(sentence)
        utterance.lang = voice?.lang ?? 'en-US'
        utterance.rate = Math.min(2, Math.max(0.5, rate))

        if (voice) {
          utterance.voice = voice
        }

        return utterance
      })

      if (utterances.length === 0) {
        return
      }

      queueRef.current = utterances
      setSpeaking(true)

      utterances[utterances.length - 1].onend = () => setSpeaking(false)
      utterances[utterances.length - 1].onerror = () => setSpeaking(false)

      for (const utterance of utterances) {
        window.speechSynthesis.speak(utterance)
      }
    },
    [supported, voice],
  )

  // Rời màn hình mà giọng vẫn đọc là lỗi khó chịu nhất của API này.
  useEffect(() => stop, [stop])

  return {
    supported,
    speaking,
    speak,
    stop,
    voiceName: voice?.name ?? null,

    /**
     * Sẵn sàng đọc hay chưa.
     *
     * Có speechSynthesis KHÔNG có nghĩa là đọc được: nhiều môi trường
     * (trình duyệt nhúng, Linux thiếu gói giọng nói, một số máy ảo) trả về
     * danh sách giọng rỗng. Lúc đó bấm nghe sẽ im lặng mà không báo lỗi —
     * kiểu hỏng tệ nhất với học viên, vì họ tưởng tai mình có vấn đề.
     */
    ready: supported && voice !== null,
  }
}
