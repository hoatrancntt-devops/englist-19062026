import { useState } from 'react'
import { Mic, Play, RotateCcw, Square } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { api, ApiError } from '@/lib/api-client'
import { useRecorder } from '@/lib/use-recorder'
import { useSpeech } from '@/lib/use-speech'

interface SpeechGrade {
  graded: boolean
  transcriptEn: string | null
  pronunciationScore: number
  fluencyScore: number
  communicationScore: number
  overallScore: number
  missedWords: string[]
  feedbackVi: string[]
  unavailableReasonVi: string | null
}

/**
 * Một câu đọc theo mẫu: nghe mẫu, ghi âm, nghe lại chính mình, rồi chấm.
 *
 * Bước "nghe lại chính mình" không thừa. Người học nghe được khoảng cách giữa mẫu và
 * bản thân thì tự sửa nhanh hơn nhiều so với chỉ nhìn con điểm.
 */
export function SpeakingDrill({
  expectedText,
  promptVi,
  ipa,
  activityId,
}: {
  expectedText: string
  promptVi: string
  ipa?: string
  activityId: string
}) {
  const speech = useSpeech()
  const recorder = useRecorder()

  const [grade, setGrade] = useState<SpeechGrade | null>(null)
  const [grading, setGrading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    if (!recorder.recording) {
      return
    }

    setGrading(true)
    setError(null)

    const form = new FormData()
    form.append('audio', recorder.recording.blob, 'loi-noi.webm')
    form.append('expectedText', expectedText)
    form.append('contextType', 'lesson_activity')
    form.append('contextId', activityId)
    form.append('durationMs', String(recorder.recording.durationMs))

    try {
      setGrade(await api.postForm<SpeechGrade>('/api/v1/speech/grade', form))
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : 'Không gửi được bản ghi âm. Kiểm tra kết nối rồi thử lại.',
      )
    } finally {
      setGrading(false)
    }
  }

  const retry = () => {
    recorder.reset()
    setGrade(null)
    setError(null)
  }

  return (
    <li className="rounded-[var(--radius-control)] border border-[var(--border-subtle)] p-3">
      <p className="text-sm text-secondary">{promptVi}</p>

      {expectedText && (
        <div className="mt-2 flex items-start justify-between gap-3">
          <p className="flex items-center gap-2 font-medium">
            <Mic className="size-4 shrink-0 text-[var(--color-skill-speaking)]" aria-hidden />
            {expectedText}
          </p>

          {/* Nghe câu mẫu trước khi đọc theo — không có mẫu thì drill nhắc lại vô nghĩa. */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => speech.speak(expectedText, 0.9)}
            disabled={!speech.ready}
            aria-label={`Nghe mẫu: ${expectedText}`}
          >
            <Play className="size-4" aria-hidden />
            Nghe mẫu
          </Button>
        </div>
      )}

      {ipa && <p className="mt-1 font-mono text-xs text-muted">{ipa}</p>}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        {recorder.state === 'recording' ? (
          <Button variant="danger" size="sm" onClick={recorder.stop}>
            <Square className="size-4" aria-hidden />
            Dừng ({(recorder.elapsedMs / 1000).toFixed(1)}s)
          </Button>
        ) : (
          <Button
            variant="secondary"
            size="sm"
            onClick={recorder.start}
            loading={recorder.state === 'requesting'}
            disabled={grading}
          >
            <Mic className="size-4" aria-hidden />
            {recorder.recording ? 'Ghi lại' : 'Ghi âm'}
          </Button>
        )}

        {recorder.recording && !grade && (
          <>
            {/* Điều khiển gốc của trình duyệt: đã có nút phát, thanh tua, chỉnh âm lượng. */}
            <audio src={recorder.recording.objectUrl} controls className="h-8 max-w-[16rem]" />

            <Button size="sm" onClick={submit} loading={grading}>
              Chấm câu này
            </Button>
          </>
        )}

        {grade && (
          <Button variant="ghost" size="sm" onClick={retry}>
            <RotateCcw className="size-4" aria-hidden />
            Đọc lại
          </Button>
        )}
      </div>

      {recorder.state === 'denied' && (
        <p className="mt-2 text-sm text-[var(--color-danger)]">
          Trình duyệt chưa cho dùng micro. Bấm vào biểu tượng khoá trên thanh địa chỉ để
          bật quyền micro cho trang này, rồi thử lại.
        </p>
      )}

      {recorder.state === 'unsupported' && (
        <p className="mt-2 text-sm text-secondary">
          Trình duyệt này không ghi âm được. Bạn vẫn nên đọc to câu trên — phần đọc quan
          trọng hơn con điểm.
        </p>
      )}

      {error && <p className="mt-2 text-sm text-[var(--color-danger)]">{error}</p>}

      {grade && <DrillResult grade={grade} />}
    </li>
  )
}

/** Kết quả chấm một câu. Điểm luôn đi kèm bản ghi chữ để người học đối chiếu được. */
function DrillResult({ grade }: { grade: SpeechGrade }) {
  if (!grade.graded) {
    return (
      <p className="mt-3 rounded-[var(--radius-control)] bg-[color-mix(in_oklch,var(--color-warning)_12%,transparent)] p-3 text-sm text-secondary">
        {grade.unavailableReasonVi ?? 'Chưa chấm được câu này.'}
      </p>
    )
  }

  return (
    <div className="mt-3 space-y-2 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] p-3">
      <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
        <span className="text-lg font-semibold">{grade.overallScore}</span>
        <ScoreChip label="Phát âm" value={grade.pronunciationScore} />
        <ScoreChip label="Trôi chảy" value={grade.fluencyScore} />
        <ScoreChip label="Truyền đạt" value={grade.communicationScore} />
      </div>

      {/* Bản ghi chữ là bằng chứng của điểm. Thiếu nó thì điểm chỉ là con số phán xuống. */}
      {grade.transcriptEn && (
        <p className="text-sm text-secondary">
          Máy nghe được: <span className="font-medium text-[var(--text-primary)]">{grade.transcriptEn}</span>
        </p>
      )}

      <ul className="space-y-1 text-sm text-secondary">
        {grade.feedbackVi.map((note, index) => (
          <li key={index}>{note}</li>
        ))}
      </ul>
    </div>
  )
}

function ScoreChip({ label, value }: { label: string; value: number }) {
  return (
    <span className="text-sm text-secondary">
      {label} <span className="font-medium text-[var(--text-primary)]">{value}</span>
    </span>
  )
}
