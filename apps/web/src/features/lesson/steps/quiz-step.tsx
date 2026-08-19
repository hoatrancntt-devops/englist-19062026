import { useState } from 'react'
import { Check, X, Play } from 'lucide-react'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { useSpeech } from '@/lib/use-speech'
import type { ActivityGrade, LessonActivity } from '../lesson-types'

interface QuizStepProps {
  activity: LessonActivity
  grade: ActivityGrade | null
  submitting: boolean
  onSubmit: (responses: { itemCode: string; chosenIndex: number }[]) => void
}

/**
 * Bước trắc nghiệm, dùng chung cho Nghe, Đọc và Kiểm tra.
 *
 * Ba bước đó khác nhau ở phần dẫn (đoạn nghe, bài đọc) chứ phần câu hỏi giống hệt nhau,
 * nên tách riêng ở đây thay vì chép ba lần.
 */
export function QuizStep({ activity, grade, submitting, onSubmit }: QuizStepProps) {
  const [chosen, setChosen] = useState<Record<string, number>>({})
  const speech = useSpeech()

  const answeredAll = activity.items.every((item) => chosen[item.code] !== undefined)
  const locked = grade !== null

  const gradeFor = (code: string) => grade?.items.find((g) => g.itemCode === code)

  return (
    <div className="space-y-5">
      {activity.items.map((item, index) => {
        const result = gradeFor(item.code)

        return (
          <fieldset key={item.code} className="space-y-2">
            <legend className="mb-2 text-sm font-medium">
              <span className="mr-2 text-muted">{index + 1}.</span>
              {item.prompt.PromptVi ?? item.prompt.PromptEn}
            </legend>

            {item.prompt.AudioText && (
              <div className="mb-2 flex items-center justify-between gap-3 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2">
                <Button
                  size="sm"
                  onClick={() => speech.speak(item.prompt.AudioText ?? '', 0.9)}
                  disabled={!speech.ready}
                  aria-label="Nghe câu hỏi"
                >
                  <Play className="size-4" aria-hidden />
                  Nghe
                </Button>

                {/* Văn bản chỉ hiện SAU khi đã chấm. Hiện trước là biến câu nghe thành câu đọc. */}
                {locked ? (
                  <span className="font-mono text-sm">{item.prompt.AudioText}</span>
                ) : (
                  <span className="text-xs text-muted">Nghe rồi chọn đáp án</span>
                )}
              </div>
            )}

            <div className="grid gap-2">
              {item.prompt.Choices.map((choice, choiceIndex) => {
                const selected = chosen[item.code] === choiceIndex
                const isCorrect = result && result.correctIndex === choiceIndex
                const isWrongPick = result && result.chosenIndex === choiceIndex && !result.correct

                return (
                  <label
                    key={choice}
                    className={cn(
                      'flex cursor-pointer items-center gap-3 rounded-[var(--radius-control)] border px-3 py-2 text-sm transition-colors',
                      !locked && selected && 'border-brand-500 bg-brand-50 dark:bg-brand-900/30',
                      !locked && !selected && 'border-[var(--border-subtle)] hover:bg-[var(--surface-hover)]',
                      isCorrect && 'border-[var(--color-success)] bg-[color-mix(in_oklch,var(--color-success)_12%,transparent)]',
                      isWrongPick && 'border-[var(--color-danger)] bg-[color-mix(in_oklch,var(--color-danger)_12%,transparent)]',
                      locked && !isCorrect && !isWrongPick && 'border-[var(--border-subtle)] opacity-60',
                      locked && 'cursor-default',
                    )}
                  >
                    <input
                      type="radio"
                      name={item.code}
                      className="sr-only"
                      disabled={locked}
                      checked={selected}
                      onChange={() => setChosen((prev) => ({ ...prev, [item.code]: choiceIndex }))}
                    />

                    <span
                      className={cn(
                        'flex size-5 shrink-0 items-center justify-center rounded-full border',
                        selected || isCorrect ? 'border-transparent' : 'border-[var(--border-strong)]',
                        selected && !locked && 'bg-brand-600 text-white',
                        isCorrect && 'bg-[var(--color-success)] text-white',
                        isWrongPick && 'bg-[var(--color-danger)] text-white',
                      )}
                      aria-hidden
                    >
                      {isCorrect && <Check className="size-3.5" />}
                      {isWrongPick && <X className="size-3.5" />}
                    </span>

                    <span>{choice}</span>
                  </label>
                )
              })}
            </div>
          </fieldset>
        )
      })}

      {!locked && (
        <Button
          onClick={() =>
            onSubmit(
              activity.items.map((item) => ({
                itemCode: item.code,
                // -1 nghĩa là bỏ trống; máy chủ tính là sai chứ không loại khỏi bài.
                chosenIndex: chosen[item.code] ?? -1,
              })),
            )
          }
          disabled={!answeredAll}
          loading={submitting}
        >
          {answeredAll ? 'Kiểm tra đáp án' : `Còn ${activity.items.length - Object.keys(chosen).length} câu chưa chọn`}
        </Button>
      )}
    </div>
  )
}
