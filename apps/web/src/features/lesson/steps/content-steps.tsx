import { useState } from 'react'
import { Volume2, Lightbulb, FileText, Play, Square } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/feedback'
import { useSpeech } from '@/lib/use-speech'
import { SpeakingDrill } from './speaking-drill'
import type { ActivityGrade, LessonActivity, VocabularyEntry } from '../lesson-types'

/** Phần dẫn của bước Nghe: bối cảnh và bản ghi lời thoại. */
export function ListeningIntro({ payload }: { payload: Record<string, unknown> }) {
  const contextVi = payload.ContextVi as string | undefined
  const transcriptEn = payload.TranscriptEn as string | undefined
  const transcriptVi = payload.TranscriptVi as string | undefined
  const speed = payload.Speed as number | undefined

  const [showTranscript, setShowTranscript] = useState(false)
  const speech = useSpeech()

  return (
    <div className="mb-5 space-y-3">
      {contextVi && <p className="text-sm text-secondary">{contextVi}</p>}

      <div className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <span className="flex items-center gap-2 text-sm font-medium">
            <Volume2 className="size-4 text-brand-600" aria-hidden />
            Đoạn nghe
            {speed && <Badge>tốc độ {speed}×</Badge>}
          </span>

          <div className="flex items-center gap-2">
            <Button
              size="sm"
              onClick={() =>
                speech.speaking ? speech.stop() : speech.speak(transcriptEn ?? '', speed ?? 1)
              }
              disabled={!speech.ready || !transcriptEn}
            >
              {speech.speaking ? (
                <>
                  <Square className="size-4" aria-hidden />
                  Dừng
                </>
              ) : (
                <>
                  <Play className="size-4" aria-hidden />
                  Nghe
                </>
              )}
            </Button>

            {/* Nghe trước, đọc sau. Hiện phụ đề ngay từ đầu thì học viên chỉ đọc chứ không luyện tai. */}
            <Button variant="ghost" size="sm" onClick={() => setShowTranscript((v) => !v)}>
              {showTranscript ? 'Ẩn lời thoại' : 'Xem lời thoại'}
            </Button>
          </div>
        </div>

        {showTranscript ? (
          <div className="mt-3 space-y-2 text-sm">
            <p className="font-mono">{transcriptEn}</p>
            <p className="text-secondary">{transcriptVi}</p>
          </div>
        ) : (
          <p className="mt-3 text-xs text-muted">
            {/* Chỉ báo hỏng SAU khi đã thử phát và cả hai nguồn đều không ra tiếng.
                Báo trước khi bấm là đoán mò: bản thu trên máy chủ có thể vẫn dùng được
                ngay cả khi máy của học viên không có gói giọng nói nào. */}
            {speech.unavailableVi ??
              'Nghe trước ít nhất hai lần rồi mới mở lời thoại — mở sớm thì bạn đang luyện đọc, không phải luyện nghe.'}
          </p>
        )}
      </div>
    </div>
  )
}

/** Phần dẫn của bước Đọc: văn bản gốc và bản dịch. */
export function ReadingIntro({ payload }: { payload: Record<string, unknown> }) {
  const contextVi = payload.ContextVi as string | undefined
  const textEn = payload.TextEn as string | undefined
  const textVi = payload.TextVi as string | undefined

  const [showVi, setShowVi] = useState(false)

  return (
    <div className="mb-5 space-y-3">
      {contextVi && <p className="text-sm text-secondary">{contextVi}</p>}

      <div className="rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-4">
        <div className="mb-3 flex items-center justify-between gap-3">
          <span className="flex items-center gap-2 text-sm font-medium">
            <FileText className="size-4 text-brand-600" aria-hidden />
            Văn bản
          </span>
          <Button variant="ghost" size="sm" onClick={() => setShowVi((v) => !v)}>
            {showVi ? 'Ẩn bản dịch' : 'Xem bản dịch'}
          </Button>
        </div>

        <pre className="overflow-x-auto whitespace-pre-wrap font-mono text-sm">{textEn}</pre>

        {showVi && (
          <pre className="mt-3 overflow-x-auto whitespace-pre-wrap border-t border-[var(--border-subtle)] pt-3 text-sm text-secondary">
            {textVi}
          </pre>
        )}
      </div>
    </div>
  )
}

/** Bước từ vựng: xem là xong, không có gì để chấm. */
export function VocabStep({
  activity,
  submitting,
  onDone,
}: {
  activity: LessonActivity
  submitting: boolean
  onDone: () => void
}) {
  const vocabulary = (activity.payload.Vocabulary as VocabularyEntry[] | undefined) ?? []

  return (
    <div className="space-y-4">
      <ul className="grid gap-2 sm:grid-cols-2">
        {vocabulary.map((entry) => (
          <li key={entry.Term} className="rounded-[var(--radius-control)] border border-[var(--border-subtle)] p-3">
            <div className="flex flex-wrap items-baseline gap-2">
              <span className="font-semibold">{entry.Term}</span>
              <span className="font-mono text-xs text-muted">{entry.Ipa}</span>
            </div>
            <p className="mt-0.5 text-sm text-secondary">{entry.MeaningVi}</p>
            {/* Cụm dùng được ngay quan trọng hơn nghĩa của từ đứng một mình. */}
            <p className="mt-1.5 rounded bg-[var(--surface-sunken)] px-2 py-1 font-mono text-xs">{entry.Chunk}</p>
          </li>
        ))}
      </ul>

      <Button onClick={onDone} loading={submitting}>
        Đã thuộc, đi tiếp
      </Button>
    </div>
  )
}

/**
 * Bước nói: ghi âm và chấm thật, từng câu một.
 *
 * Chấm từng câu chứ không gộp cả bước, vì nhận xét chỉ hữu ích khi gắn với đúng câu vừa đọc.
 *
 * Nút đi tiếp KHÔNG đòi phải chấm hết. Micro hỏng, phòng ồn, hay đang ngồi chỗ đông người
 * đều là lý do chính đáng để bỏ qua — chặn bước sẽ khoá luôn cả bài học.
 */
export function SpeakingStep({
  activity,
  grade,
  submitting,
  onDone,
}: {
  activity: LessonActivity
  grade: ActivityGrade | null
  submitting: boolean
  onDone: () => void
}) {
  const drills = (activity.payload.Drills as { ExpectedText: string; PromptVi: string; Ipa?: string }[]) ?? []

  return (
    <div className="space-y-4">
      <p className="text-sm text-secondary">
        Bấm ghi âm rồi đọc to từng câu. Máy đối chiếu với câu mẫu và chỉ ra từ nào chưa rõ.
      </p>

      <ul className="space-y-3">
        {drills.map((drill, index) => (
          <SpeakingDrill
            key={index}
            expectedText={drill.ExpectedText}
            promptVi={drill.PromptVi}
            ipa={drill.Ipa}
            activityId={activity.id}
          />
        ))}
      </ul>

      <p className="text-xs text-muted">
        Máy chấm ở mức từ: nó biết bạn có đọc ra đúng từ hay không, chưa phân tích được từng âm.
        Bản ghi âm chỉ nằm trên máy chủ này và tự xoá sau 45 ngày.
      </p>

      {!grade && (
        <Button variant="secondary" onClick={onDone} loading={submitting}>
          Đi tiếp
        </Button>
      )}
    </div>
  )
}

/** Bước viết: chấm bằng luật ngay tại máy chủ. */
export function WritingStep({
  activity,
  grade,
  submitting,
  onSubmit,
}: {
  activity: LessonActivity
  grade: ActivityGrade | null
  submitting: boolean
  onSubmit: (answers: string[]) => void
}) {
  const kind = activity.payload.Kind as string | undefined
  const promptVi = activity.payload.PromptVi as string | undefined
  const promptEn = activity.payload.PromptEn as string | undefined
  const hintVi = activity.payload.HintVi as string | undefined
  const blanks = (activity.payload.Blanks as string[][] | undefined) ?? []
  const correctOrder = (activity.payload.CorrectOrder as string[] | undefined) ?? []

  // Số ô nhập phụ thuộc dạng bài: điền chỗ trống có bao nhiêu chỗ thì bấy nhiêu ô,
  // sắp câu thì bấy nhiêu mảnh, email có hướng dẫn thì một ô lớn.
  const fieldCount = kind === 'FillBlank' ? blanks.length : kind === 'Reorder' ? correctOrder.length : 1
  const [answers, setAnswers] = useState<string[]>(() => Array(Math.max(1, fieldCount)).fill(''))

  const locked = grade !== null

  return (
    <div className="space-y-4">
      {promptVi && <p className="text-sm text-secondary">{promptVi}</p>}
      {promptEn && (
        <p className="rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2 font-mono text-sm">
          {promptEn}
        </p>
      )}

      {kind === 'Reorder' && (
        <p className="text-xs text-muted">Nhập lần lượt từng mảnh theo đúng thứ tự bạn cho là đúng.</p>
      )}

      <div className="grid gap-2">
        {answers.map((value, index) => (
          <input
            key={index}
            value={value}
            disabled={locked}
            aria-label={`Ô nhập ${index + 1}`}
            placeholder={kind === 'FillBlank' ? `Chỗ trống ${index + 1}` : `Mảnh ${index + 1}`}
            onChange={(e) =>
              setAnswers((prev) => prev.map((v, i) => (i === index ? e.target.value : v)))
            }
            className="w-full rounded-[var(--radius-control)] border border-[var(--border-strong)] bg-[var(--surface-raised)] px-3 py-2 text-sm focus:border-brand-500 disabled:opacity-60"
          />
        ))}
      </div>

      {hintVi && !locked && (
        <p className="flex gap-2 text-xs text-muted">
          <Lightbulb className="size-3.5 shrink-0" aria-hidden />
          {hintVi}
        </p>
      )}

      {grade?.sampleEn && (
        <p className="rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2 text-sm">
          <span className="text-muted">Mẫu: </span>
          <span className="font-mono">{grade.sampleEn}</span>
        </p>
      )}

      {!locked && (
        <Button
          onClick={() => onSubmit(answers)}
          disabled={answers.every((a) => a.trim().length === 0)}
          loading={submitting}
        >
          Nộp bài viết
        </Button>
      )}
    </div>
  )
}
