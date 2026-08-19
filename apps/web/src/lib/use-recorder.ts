import { useCallback, useEffect, useRef, useState } from 'react'

export interface Recording {
  blob: Blob
  durationMs: number
  /** URL tạm để nghe lại. Người học phải nghe được chính mình thì mới tự sửa được. */
  objectUrl: string
}

export type RecorderState = 'idle' | 'requesting' | 'recording' | 'denied' | 'unsupported'

/**
 * Ghi âm bằng MediaRecorder.
 *
 * Hai điều dễ sai mà hook này lo hộ:
 *
 * Một, <b>phải tắt micro sau khi ghi</b>. Không gọi `track.stop()` thì đèn micro của
 * trình duyệt sáng mãi và người học tưởng ứng dụng đang nghe lén. Đây là lý do có cả
 * cleanup khi unmount chứ không chỉ khi bấm dừng.
 *
 * Hai, <b>URL tạm phải được thu hồi</b>. Mỗi lần ghi lại mà không `revokeObjectURL`
 * thì blob cũ nằm lại trong bộ nhớ đến khi đóng tab.
 *
 * Micro cần ngữ cảnh an toàn: HTTPS, hoặc localhost — trình duyệt xem localhost là an toàn
 * nên bản dev chạy HTTP vẫn ghi âm được.
 */
export function useRecorder() {
  const [state, setState] = useState<RecorderState>('idle')
  const [recording, setRecording] = useState<Recording | null>(null)
  const [elapsedMs, setElapsedMs] = useState(0)

  const recorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const startedAtRef = useRef(0)
  const chunksRef = useRef<Blob[]>([])
  const objectUrlRef = useRef<string | null>(null)

  const releaseMicrophone = useCallback(() => {
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
    recorderRef.current = null
  }, [])

  // Rời màn hình giữa chừng vẫn phải tắt micro và thu hồi URL.
  useEffect(() => {
    return () => {
      releaseMicrophone()
      if (objectUrlRef.current) {
        URL.revokeObjectURL(objectUrlRef.current)
      }
    }
  }, [releaseMicrophone])

  // Đồng hồ hiện thời lượng đang ghi. Không có nó người học không biết máy có nghe hay không.
  useEffect(() => {
    if (state !== 'recording') {
      return
    }

    const timer = window.setInterval(() => {
      setElapsedMs(Date.now() - startedAtRef.current)
    }, 100)

    return () => window.clearInterval(timer)
  }, [state])

  const start = useCallback(async () => {
    if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
      setState('unsupported')
      return
    }

    setState('requesting')

    let stream: MediaStream

    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    } catch {
      // Không phân biệt "từ chối" với "không có micro": cả hai đều dẫn tới cùng một việc
      // người học cần làm, là mở lại quyền hoặc cắm micro.
      setState('denied')
      return
    }

    if (objectUrlRef.current) {
      URL.revokeObjectURL(objectUrlRef.current)
      objectUrlRef.current = null
    }

    setRecording(null)
    chunksRef.current = []
    streamRef.current = stream

    const recorder = new MediaRecorder(stream)
    recorderRef.current = recorder

    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) {
        chunksRef.current.push(event.data)
      }
    }

    recorder.onstop = () => {
      const durationMs = Date.now() - startedAtRef.current
      const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' })
      const objectUrl = URL.createObjectURL(blob)

      objectUrlRef.current = objectUrl
      setRecording({ blob, durationMs, objectUrl })
      setState('idle')

      // Tắt micro NGAY khi dừng, không đợi unmount.
      releaseMicrophone()
    }

    startedAtRef.current = Date.now()
    setElapsedMs(0)
    recorder.start()
    setState('recording')
  }, [releaseMicrophone])

  const stop = useCallback(() => {
    if (recorderRef.current?.state === 'recording') {
      recorderRef.current.stop()
    }
  }, [])

  const reset = useCallback(() => {
    if (objectUrlRef.current) {
      URL.revokeObjectURL(objectUrlRef.current)
      objectUrlRef.current = null
    }

    setRecording(null)
    setElapsedMs(0)
  }, [])

  return { state, recording, elapsedMs, start, stop, reset }
}
