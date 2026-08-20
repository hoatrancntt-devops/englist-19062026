import { useState } from 'react'
import { Lightbulb, Play, Volume2 } from 'lucide-react'
import { cn } from '@/lib/cn'
import { useSpeech } from '@/lib/use-speech'
import { VOCAB_SPEEDS as SPEEDS, VOCAB_VOICES as VOICES } from '@/lib/vocab-voices'
import type { VocabVoiceId } from '@/lib/vocab-voices'
import { SpeakingDrill } from './speaking-drill'
import type { VocabularyEntry } from '../lesson-types'

/**
 * Một thẻ từ vựng.
 *
 * Bốn thứ trên thẻ đều có lý do, không phải trang trí:
 *
 * Emoji và mẹo nhớ đánh vào hai kênh ghi nhớ khác nhau — hình và chữ được xử lý độc lập, nên
 * từ được mã hoá cả hai đường thì bám lâu hơn hẳn một dòng nghĩa.
 *
 * Nghe được nhiều giọng là thành phần chính của luyện phân biệt âm: quen tai đúng một người
 * thì ra đời gặp người khác là chịu. Bốn giọng nam nữ Anh Mỹ trộn lẫn buộc tai bắt lấy cái
 * chung giữa chúng thay vì nhớ thuộc lòng một cách phát âm.
 *
 * Chỉnh tốc độ để nghe rõ từng âm trước, rồi mới nghe ở nhịp thật.
 *
 * Và cuối cùng phải NÓI LẠI có chấm. Đọc thầm rồi bấm "đã thuộc" thì không ai biết học viên
 * có phát âm được hay không, kể cả chính họ.
 */
export function VocabCard({
  entry,
  activityId,
}: {
  entry: VocabularyEntry
  activityId: string
}) {
  const [speed, setSpeed] = useState(1)
  const [voice, setVoice] = useState<VocabVoiceId>(VOICES[0].id)
  const [showMnemonic, setShowMnemonic] = useState(false)

  // Dùng lại hook đọc chung: nó đã có sẵn phần lùi về giọng trình duyệt khi máy chủ chưa
  // sinh đoạn đó, và phần dừng đoạn cũ khi học viên bấm nghe liên tục.
  const speech = useSpeech()

  return (
    <li className="space-y-3 rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-4">
      <div className="flex items-start gap-3">
        {entry.Emoji ? (
          <span className="text-3xl leading-none" aria-hidden>
            {entry.Emoji}
          </span>
        ) : null}

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-2">
            <span className="text-lg font-semibold">{entry.Term}</span>
            <span className="font-mono text-xs text-muted">{entry.Ipa}</span>
          </div>
          <p className="mt-0.5 text-sm text-secondary">{entry.MeaningVi}</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => speech.speak(entry.Term, speed, voice)}
          className="flex items-center gap-1.5 rounded-[var(--radius-control)] bg-brand-50 px-2.5 py-1.5 text-sm font-medium text-brand-700 hover:bg-brand-100 dark:bg-brand-900/40 dark:text-brand-200"
        >
          <Volume2 className={cn('size-4', speech.speaking && 'animate-pulse')} aria-hidden />
          Nghe từ
        </button>

        <button
          type="button"
          onClick={() => speech.speak(entry.Chunk, speed, voice)}
          className="flex items-center gap-1.5 rounded-[var(--radius-control)] border border-[var(--border-strong)] px-2.5 py-1.5 text-sm hover:bg-[var(--surface-hover)]"
        >
          <Play className="size-3.5" aria-hidden />
          Nghe cả cụm
        </button>
      </div>

      <div className="flex flex-wrap gap-4">
        <fieldset className="flex items-center gap-1.5">
          <legend className="sr-only">Tốc độ đọc</legend>
          <span className="text-xs text-muted">Tốc độ</span>
          {SPEEDS.map((s) => (
            <button
              key={s.rate}
              type="button"
              onClick={() => setSpeed(s.rate)}
              aria-pressed={speed === s.rate}
              className={cn(
                'rounded px-2 py-0.5 text-xs',
                speed === s.rate
                  ? 'bg-brand-600 text-white'
                  : 'border border-[var(--border-strong)] hover:bg-[var(--surface-hover)]',
              )}
            >
              {s.labelVi}
            </button>
          ))}
        </fieldset>

        <fieldset className="flex items-center gap-1.5">
          <legend className="sr-only">Giọng đọc</legend>
          <span className="text-xs text-muted">Giọng</span>
          {VOICES.map((v) => (
            <button
              key={v.id}
              type="button"
              onClick={() => setVoice(v.id)}
              aria-pressed={voice === v.id}
              title={v.labelVi}
              className={cn(
                'rounded px-2 py-0.5 text-xs',
                voice === v.id
                  ? 'bg-brand-600 text-white'
                  : 'border border-[var(--border-strong)] hover:bg-[var(--surface-hover)]',
              )}
            >
              {v.labelVi}
            </button>
          ))}
        </fieldset>
      </div>

      <p className="rounded bg-[var(--surface-sunken)] px-2 py-1 font-mono text-xs">{entry.Chunk}</p>

      {entry.MnemonicVi ? (
        <div>
          <button
            type="button"
            onClick={() => setShowMnemonic((v) => !v)}
            className="flex items-center gap-1.5 text-xs font-medium text-brand-600 hover:underline dark:text-brand-300"
          >
            <Lightbulb className="size-3.5" aria-hidden />
            {showMnemonic ? 'Ẩn mẹo nhớ' : 'Mẹo nhớ'}
          </button>

          {showMnemonic ? (
            <p className="mt-1.5 rounded bg-[color-mix(in_oklch,var(--color-warning)_12%,transparent)] p-2 text-sm">
              {entry.MnemonicVi}
            </p>
          ) : null}
        </div>
      ) : null}

      {/* Nói lại và được chấm. Đây là thứ quyết định bước từ vựng có đạt hay không. */}
      <ul className="border-t border-[var(--border-subtle)] pt-3">
        <SpeakingDrill
          expectedText={entry.Term}
          promptVi={`Đọc to: ${entry.MeaningVi}`}
          ipa={entry.Ipa}
          activityId={activityId}
        />
      </ul>
    </li>
  )
}
