import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { RotateCcw, Check, X, Play, CalendarClock, ArrowRight, Flame } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { useSpeech } from '@/lib/use-speech'
import { Button } from '@/components/ui/button'
import { Card, CardBody } from '@/components/ui/card'
import { Badge, EmptyState, ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import { SkillBadge } from '@/components/skill-badge'
import { LessonIllustration } from '@/components/illustrations/scene-illustrations'
import type { ReviewAnswerResult, ReviewCard, ReviewSession } from './review-types'

/**
 * Buổi ôn tập giãn cách.
 *
 * Khác màn học ở một điểm quyết định cả cách viết component: mỗi câu chấm ngay khi trả lời
 * chứ không gom cuối buổi. Học viên đóng tab giữa chừng thì các câu đã làm vẫn được
 * xếp lịch xong, nên không có trạng thái "đang làm dở" nào cần lưu.
 */
export function ReviewPage() {
  const queryClient = useQueryClient()
  const [index, setIndex] = useState(0)
  const [chosen, setChosen] = useState<number | null>(null)
  const [result, setResult] = useState<ReviewAnswerResult | null>(null)
  const [correctCount, setCorrectCount] = useState(0)

  const { data, isLoading, isError } = useQuery({
    queryKey: ['learning', 'review'],
    queryFn: () => api.get<ReviewSession>('/api/v1/learning/review'),
    // Lịch ôn đổi sau mỗi câu; đọc lại từ cache cũ sẽ hiện câu đã trả lời.
    staleTime: 0,
  })

  const answer = useMutation({
    mutationFn: (body: { itemCode: string; chosenIndex: number }) =>
      api.post<ReviewAnswerResult>('/api/v1/learning/review/answer', body),
    onSuccess: (res) => {
      setResult(res)
      if (res.correct) {
        setCorrectCount((n) => n + 1)
      }
      // Số câu tới hạn hiện cả trên bảng điều khiển — cập nhật luôn để hai chỗ không lệch nhau.
      queryClient.invalidateQueries({ queryKey: ['learning', 'dashboard'] })
    },
  })

  if (isLoading) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    )
  }

  if (isError || !data) {
    return (
      <EmptyState
        title="Không tải được hàng đợi ôn tập"
        description="Thử tải lại trang. Nếu vẫn lỗi thì máy chủ đang có vấn đề, không phải máy bạn."
        action={<Button onClick={() => window.location.reload()}>Tải lại</Button>}
      />
    )
  }

  if (data.cards.length === 0) {
    return <ReviewEmpty session={data} />
  }

  const done = index >= data.cards.length
  if (done) {
    return (
      <ReviewSummary
        total={data.cards.length}
        correct={correctCount}
        remainingDue={result?.remainingDue ?? 0}
        onContinue={() => {
          setIndex(0)
          setChosen(null)
          setResult(null)
          setCorrectCount(0)
          queryClient.invalidateQueries({ queryKey: ['learning', 'review'] })
        }}
      />
    )
  }

  const card = data.cards[index]

  return (
    <div className="space-y-5">
      <ReviewHeader
        index={index}
        total={data.cards.length}
        totalDue={data.totalDue}
        messageVi={data.messageVi}
      />

      <ReviewQuestion
        key={card.itemCode}
        card={card}
        chosen={chosen}
        result={result}
        submitting={answer.isPending}
        onChoose={setChosen}
        onSubmit={() =>
          answer.mutate({ itemCode: card.itemCode, chosenIndex: chosen ?? -1 })
        }
        onNext={() => {
          setIndex((i) => i + 1)
          setChosen(null)
          setResult(null)
        }}
        isLast={index === data.cards.length - 1}
      />
    </div>
  )
}

function ReviewHeader({
  index,
  total,
  totalDue,
  messageVi,
}: {
  index: number
  total: number
  totalDue: number
  messageVi: string
}) {
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <RotateCcw className="size-5 text-brand-600" aria-hidden />
          <h1 className="text-lg font-semibold">Ôn tập</h1>
        </div>

        <span className="text-sm text-secondary">
          Câu {index + 1} trên {total}
          {totalDue > total && <span className="text-muted"> · còn {totalDue - total} câu trong hàng đợi</span>}
        </span>
      </div>

      {/* Không truyền label: ProgressBar in kèm giá trị thô bên cạnh nhãn, hợp với
          thang điểm 0-100 chứ không hợp với chỉ số câu. Câu dẫn để riêng bên dưới. */}
      <ProgressBar value={index} max={total} />

      <p className="text-xs text-muted">{messageVi}</p>
    </div>
  )
}

function ReviewQuestion({
  card,
  chosen,
  result,
  submitting,
  onChoose,
  onSubmit,
  onNext,
  isLast,
}: {
  card: ReviewCard
  chosen: number | null
  result: ReviewAnswerResult | null
  submitting: boolean
  onChoose: (index: number) => void
  onSubmit: () => void
  onNext: () => void
  isLast: boolean
}) {
  const speech = useSpeech()
  const locked = result !== null

  return (
    <Card>
      <CardBody className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <SkillBadge skill={card.prompt.Skill} />

          <Link
            to={`/learn/lesson/${card.lessonCode}`}
            className="text-xs text-secondary underline-offset-2 hover:underline"
          >
            {card.lessonTitleVi}
          </Link>

          {card.overdueDays > 0 && (
            <Badge tone="warning">Quá hạn {card.overdueDays} ngày</Badge>
          )}

          {card.repetitionCount >= 3 && (
            <span className="flex items-center gap-1 text-xs text-muted">
              <Flame className="size-3.5" aria-hidden />
              đúng {card.repetitionCount} lần liên tiếp
            </span>
          )}
        </div>

        <p className="font-medium">{card.prompt.PromptVi ?? card.prompt.PromptEn}</p>

        {card.prompt.AudioText && (
          <div className="flex items-center justify-between gap-3 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2">
            <Button
              size="sm"
              onClick={() => speech.speak(card.prompt.AudioText ?? '', 0.9)}
              disabled={!speech.ready}
              aria-label="Nghe câu hỏi"
            >
              <Play className="size-4" aria-hidden />
              Nghe
            </Button>

            {/* Chữ chỉ hiện sau khi chấm, cùng lý do như trong màn học:
                hiện trước là biến câu nghe thành câu đọc. */}
            {locked ? (
              <span className="font-mono text-sm">{card.prompt.AudioText}</span>
            ) : (
              <span className="text-xs text-muted">Nghe rồi chọn đáp án</span>
            )}
          </div>
        )}

        <div className="grid gap-2">
          {card.prompt.Choices.map((choice, choiceIndex) => {
            const selected = chosen === choiceIndex
            const isCorrect = result && result.correctIndex === choiceIndex
            const isWrongPick = result && chosen === choiceIndex && !result.correct

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
                  name={card.itemCode}
                  className="sr-only"
                  disabled={locked}
                  checked={selected}
                  onChange={() => onChoose(choiceIndex)}
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

        {result ? (
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-subtle)] pt-4">
            <p className={cn('text-sm', result.correct ? 'text-[var(--color-success)]' : 'text-secondary')}>
              {result.messageVi}
            </p>

            <Button onClick={onNext}>
              {isLast ? 'Xem kết quả' : 'Câu tiếp'}
              <ArrowRight className="size-4" aria-hidden />
            </Button>
          </div>
        ) : (
          <Button onClick={onSubmit} disabled={chosen === null} loading={submitting}>
            {chosen === null ? 'Chọn một đáp án' : 'Kiểm tra'}
          </Button>
        )}
      </CardBody>
    </Card>
  )
}

/** Hàng đợi rỗng. Nói rõ bao giờ quay lại thay vì chỉ báo là trống. */
function ReviewEmpty({ session }: { session: ReviewSession }) {
  return (
    <EmptyState
      illustration={<LessonIllustration name="clock-calendar" className="w-56" />}
      title="Chưa có câu nào tới hạn"
      description={session.messageVi}
      action={
        <Link to="/learn">
          <Button>
            Về bảng điều khiển
            <ArrowRight className="size-4" aria-hidden />
          </Button>
        </Link>
      }
    />
  )
}

function ReviewSummary({
  total,
  correct,
  remainingDue,
  onContinue,
}: {
  total: number
  correct: number
  remainingDue: number
  onContinue: () => void
}) {
  const percent = Math.round((correct * 100) / total)

  return (
    <Card>
      <CardBody className="space-y-5 text-center">
        <div className="flex justify-center">
          <CalendarClock className="size-10 text-brand-600" aria-hidden />
        </div>

        <div className="space-y-1.5">
          <h2 className="text-lg font-semibold">Xong buổi ôn</h2>
          <p className="text-sm text-secondary">
            Đúng {correct} trên {total} câu ({percent}%).
            {' '}
            {/* Nói thẳng cơ chế: câu sai quay lại sớm, không phải bị phạt điểm. */}
            Câu sai sẽ quay lại vào ngày mai, câu đúng giãn ra xa hơn.
          </p>
        </div>

        <ProgressBar value={correct} max={total} tone="brand" />

        {remainingDue > 0 ? (
          <div className="space-y-3">
            <p className="text-sm text-secondary">Còn {remainingDue} câu đang tới hạn.</p>
            <Button onClick={onContinue}>
              Ôn tiếp
              <RotateCcw className="size-4" aria-hidden />
            </Button>
          </div>
        ) : (
          <Link to="/learn">
            <Button variant="secondary">
              Về bảng điều khiển
              <ArrowRight className="size-4" aria-hidden />
            </Button>
          </Link>
        )}
      </CardBody>
    </Card>
  )
}
