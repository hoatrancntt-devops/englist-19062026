import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Đọc văn bản tiếng Anh thành tiếng.
 *
 * Hai nguồn, theo thứ tự ưu tiên:
 *
 *  1. File sinh sẵn bằng Piper trên máy chủ. Mọi học viên nghe đúng một giọng, nghe được
 *     cả trên máy không có gói giọng nói, và chi phí lúc chạy bằng 0 vì file đã có sẵn.
 *
 *  2. Giọng tổng hợp của trình duyệt. Dùng khi đoạn đó chưa được sinh — nội dung mới thêm
 *     luôn có một khoảng thời gian như vậy, cho tới lượt chạy sinh giọng kế tiếp.
 *
 * Không đảo thứ tự này: giọng trình duyệt khác nhau giữa Windows, macOS và Android, nên
 * cùng một bài mà mỗi máy đọc một kiểu thì học viên không lấy đó làm mẫu phát âm được.
 *
 * Hai chỗ đã xử lý sẵn ở nhánh trình duyệt:
 *  - Danh sách giọng nạp bất đồng bộ, nên phải nghe sự kiện voiceschanged.
 *  - Chrome cắt ngang câu dài sau khoảng mười lăm giây, nên chia theo câu.
 */

/**
 * Những đoạn máy chủ đã trả 404. Để ở cấp module chứ không trong state vì nó đúng cho cả
 * phiên làm việc: hỏi lại một đoạn vừa báo không có chỉ tốn thêm một vòng mạng trước mỗi
 * lần bấm nghe, và học viên nghe thấy độ trễ đó.
 */
const missingOnServer = new Set<string>()

function serverAudioUrl(text: string) {
  return `/api/v1/media/tts?text=${encodeURIComponent(text)}`
}

export function useSpeech() {
  const [supported, setSupported] = useState(false)
  const [speaking, setSpeaking] = useState(false)
  const [voice, setVoice] = useState<SpeechSynthesisVoice | null>(null)
  const [unavailableVi, setUnavailableVi] = useState<string | null>(null)
  const queueRef = useRef<SpeechSynthesisUtterance[]>([])
  const audioRef = useRef<HTMLAudioElement | null>(null)

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
    if (audioRef.current) {
      audioRef.current.pause()
      audioRef.current.src = ''
      audioRef.current = null
    }

    queueRef.current = []

    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      window.speechSynthesis.cancel()
    }

    setSpeaking(false)
  }, [])

  /** Nhánh dự phòng: giọng của trình duyệt. */
  const speakWithBrowser = useCallback(
    (text: string, rate: number) => {
      if (!supported || voice === null) {
        // Không còn nguồn nào. Nói thẳng ra thay vì im lặng — bấm mà không có gì xảy ra
        // thì học viên tưởng tai nghe của mình hỏng.
        setSpeaking(false)
        setUnavailableVi(
          'Máy này chưa đọc được tiếng Anh: bản thu chưa sinh xong và trình duyệt cũng không có giọng tiếng Anh. Bạn vẫn đọc được lời thoại bên dưới.',
        )
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
        utterance.lang = voice.lang
        utterance.rate = Math.min(2, Math.max(0.5, rate))
        utterance.voice = voice

        return utterance
      })

      if (utterances.length === 0) {
        setSpeaking(false)
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

  /**
   * Đọc một đoạn. Tốc độ lấy từ bài học: bậc thấp 0.8, bậc cao 1.3.
   * Gọi lại khi đang đọc thì dừng đoạn cũ — học viên bấm nghe lại thường xuyên.
   */
  const speak = useCallback(
    (text: string, rate = 1) => {
      const trimmed = text.trim()

      if (trimmed.length === 0) {
        return
      }

      stop()
      setUnavailableVi(null)

      if (missingOnServer.has(trimmed)) {
        speakWithBrowser(trimmed, rate)
        return
      }

      const audio = new Audio(serverAudioUrl(trimmed))

      // Máy chủ chỉ sinh một bản ở tốc độ chuẩn; nhanh chậm do trình duyệt chỉnh. Các
      // trình duyệt hiện nay giữ nguyên cao độ khi đổi tốc độ, nên giọng không bị méo.
      audio.playbackRate = Math.min(2, Math.max(0.5, rate))
      audio.preservesPitch = true

      const fallBack = () => {
        missingOnServer.add(trimmed)
        audioRef.current = null
        speakWithBrowser(trimmed, rate)
      }

      audio.onended = () => {
        audioRef.current = null
        setSpeaking(false)
      }

      // 404 khi đoạn chưa được sinh, và lỗi mạng cũng vào đây. Cả hai đều lùi về giọng
      // trình duyệt chứ không báo hỏng: học viên chỉ cần nghe được.
      audio.onerror = fallBack

      audioRef.current = audio
      setSpeaking(true)

      audio.play().catch(fallBack)
    },
    [speakWithBrowser, stop],
  )

  // Rời màn hình mà giọng vẫn đọc là lỗi khó chịu nhất của API này.
  useEffect(() => stop, [stop])

  return {
    supported,
    speaking,
    speak,
    stop,
    unavailableVi,
    voiceName: voice?.name ?? null,

    /**
     * Sẵn sàng đọc hay chưa.
     *
     * Trước đây chỉ tính giọng của trình duyệt, nên trên máy không có gói giọng nói thì
     * nút nghe bị khoá — kể cả khi máy chủ đã có sẵn bản thu của chính đoạn đó. Giờ nút
     * luôn mở, và nếu cả hai nguồn đều hỏng thì <see cref="unavailableVi"/> nói rõ lý do
     * thay vì để học viên bấm vào khoảng không.
     */
    ready: true,
  }
}
