import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Check, Clock, Play, Rocket, TriangleAlert, X } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { useSpeech } from '@/lib/use-speech'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import { SKILL_META } from '@/components/skill-badge'
import type { ChallengeItem, ChallengeOffer, ChallengeResult } from './challenge-types'

/**
 * Thi vượt một bài.
 *
 * Khác màn học ở ba chỗ, và cả ba đều cố ý:
 *
 * Một, nộp trọn gói chứ không chấm từng câu. Chấm dần sẽ cho học viên biết mình đang bao nhiêu
 * điểm rồi dừng lại ở câu vừa đủ — thứ đo được khi đó không còn là năng lực.
 *
 * Hai, không có gợi ý, không có phần giải thích, không nghe lại lời thoại sau khi chấm.
 * Đây là bài kiểm tra, không phải bài học.
 *
 * Ba, nói rõ cái giá trước khi bắt đầu: trượt là phải chờ. Giấu điều đó đi rồi mới báo
 * sau khi họ trượt là kiểu thiết kế làm người ta mất lòng tin.
 */
export function ChallengePage() {
  const { code = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [started, setStarted] = useState(false)
  const [chosen, setChosen] = useState<Record<string, number>>({})
  const [result, setResult] = useState<ChallengeResult | null>(null)

  const { data: offer, isLoading } = useQuery({
    queryKey: ['learning', 'challenge', code],
    queryFn: () => api.get<ChallengeOffer>(`/api/v1/learning/lessons/${code}/challenge`),
  })

  const submit = useMutation({
    mutationFn: (responses: { itemCode: string; chosenIndex: number }[]) =>
      api.post<ChallengeResult>(`/api/v1/learning/lessons/${code}/challenge`, { responses }),
    onSuccess: (data) => {
      setResult(data)
      // Qua được thì lộ trình và bảng điều khiển đổi hẳn, phải nạp lại.
      void queryClient.invalidateQueries({ queryKey: ['learning'] })
    },
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  if (!offer) {
    return (
      <Card className="p-6 text-center">
        <p className="text-secondary">Không tìm thấy bài học này.</p>
        <Link to="/learn" className="mt-4 inline-block">
          <Button variant="secondary">Về bảng điều khiển</Button>
        </Link>
      </Card>
    )
  }

  if (result) {
    return <ChallengeResultView lesson={offer} result={result} onGoLearn={() => navigate('/learn')} code={code} />
  }

  if (!offer.eligible) {
    return <NotEligible offer={offer} code={code} />
  }

  if (!started) {
    return <Intro offer={offer} onStart={() => setStarted(true)} code={code} />
  }

  const answered = offer.items.filter((item) => chosen[item.code] !== undefined).length
  const allAnswered = answered === offer.items.length

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title={`Thi vượt ${offer.lessonCode}`}
          description={offer.titleVi}
          icon={<Rocket className="size-5 text-brand-600" aria-hidden />}
          action={<Badge tone="brand">{`Cần ${offer.passThreshold} điểm`}</Badge>}
        />
        <CardBody>
          <ProgressBar
            value={answered}
            max={offer.items.length}
            label={`Đã trả lời ${answered} trên ${offer.items.length} câu`}
          />
        </CardBody>
      </Card>

      <Card>
        <CardBody className="space-y-6">
          {offer.items.map((item, index) => (
            <ChallengeQuestion
              key={item.code}
              item={item}
              index={index}
              chosen={chosen[item.code]}
              onChoose={(choiceIndex) => setChosen((prev) => ({ ...prev, [item.code]: choiceIndex }))}
            />
          ))}

          <div className="flex flex-wrap items-center gap-3 border-t border-[var(--border-subtle)] pt-4">
            <Button
              onClick={() =>
                submit.mutate(
                  offer.items.map((item) => ({
                    itemCode: item.code,
                    // -1 là bỏ trống. Máy chủ tính sai chứ không loại câu khỏi bài,
                    // nếu không thì bỏ trống hết sẽ ra 100 điểm.
                    chosenIndex: chosen[item.code] ?? -1,
                  })),
                )
              }
              loading={submit.isPending}
            >
              Nộp bài thi vượt
              <ArrowRight className="size-4" aria-hidden />
            </Button>

            {!allAnswered && (
              <span className="text-sm text-muted">
                Còn {offer.items.length - answered} câu chưa chọn. Bỏ trống tính là sai.
              </span>
            )}
          </div>
        </CardBody>
      </Card>
    </div>
  )
}

// ---------------------------------------------------------------

function Intro({ offer, onStart, code }: { offer: ChallengeOffer; onStart: () => void; code: string }) {
  return (
    <Card>
      <CardBody className="space-y-5">
        <div className="flex items-start gap-3">
          <Rocket className="mt-1 size-6 shrink-0 text-brand-600" aria-hidden />
          <div>
            <h1 className="text-xl font-semibold">Thi vượt {offer.lessonCode}</h1>
            <p className="mt-1 text-sm text-secondary">{offer.titleVi}</p>
          </div>
        </div>

        <p className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4 text-sm leading-relaxed">
          {offer.objectiveVi}
        </p>

        <ul className="space-y-2 text-sm">
          {[
            `${offer.items.length} câu, nộp một lần. Không chấm từng câu để bạn không dừng lại ở mức vừa đủ điểm.`,
            `Đúng từ ${offer.passThreshold} điểm trở lên là qua, cao hơn ngưỡng học thường vì bạn bỏ qua cả bài giảng.`,
            'Một kỹ năng hổng vẫn trượt, dù điểm tổng cao.',
            'Trượt thì phải chờ nửa ngày mới thi lại được bài này.',
          ].map((line) => (
            <li key={line} className="flex items-start gap-2">
              <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-brand-500" aria-hidden />
              {line}
            </li>
          ))}
        </ul>

        <div className="flex flex-wrap items-center gap-3">
          <Button onClick={onStart}>
            Bắt đầu thi
            <ArrowRight className="size-4" aria-hidden />
          </Button>

          <Link to={`/learn/lesson/${code}`} className="text-sm text-secondary underline-offset-4 hover:underline">
            Học bài theo cách thường
          </Link>
        </div>
      </CardBody>
    </Card>
  )
}

function NotEligible({ offer, code }: { offer: ChallengeOffer; code: string }) {
  return (
    <Card className="p-6">
      <div className="flex flex-col items-center text-center">
        <Clock className="size-10 text-[var(--text-muted)]" aria-hidden />
        <h1 className="mt-3 font-semibold">Chưa thi vượt {offer.lessonCode} được</h1>
        <p className="mt-2 max-w-md text-sm text-secondary">{offer.reasonVi}</p>

        <div className="mt-5 flex flex-wrap justify-center gap-3">
          <Link to={`/learn/lesson/${code}`}>
            <Button>Học bài này</Button>
          </Link>
          <Link to="/learn">
            <Button variant="secondary">Về bảng điều khiển</Button>
          </Link>
        </div>
      </div>
    </Card>
  )
}

/**
 * Một câu trắc nghiệm có đề, lựa chọn và nút nghe.
 *
 * Xuất ra ngoài để bài tổng hợp dùng lại: hai màn hình đều là "chọn một đáp án, nộp trọn gói",
 * và dựng hai bản sẽ khiến chúng trôi dần khỏi nhau ở đúng những chi tiết khó thấy nhất —
 * cách phát âm câu hỏi, cách đánh dấu câu chưa chọn.
 */
export function ChallengeQuestion({
  item,
  index,
  chosen,
  onChoose,
}: {
  item: ChallengeItem
  index: number
  chosen: number | undefined
  onChoose: (choiceIndex: number) => void
}) {
  const speech = useSpeech()
  const meta = SKILL_META[item.skill as keyof typeof SKILL_META]

  return (
    <fieldset className="space-y-2">
      <legend className="mb-2 text-sm font-medium">
        <span className="mr-2 text-muted">{index + 1}.</span>
        {item.prompt.PromptVi ?? item.prompt.PromptEn}
        {meta && <span className="ml-2 text-xs text-muted">· {meta.labelVi}</span>}
      </legend>

      {item.prompt.AudioText && (
        <div className="mb-2 flex items-center justify-between gap-3 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2">
          <Button
            size="sm"
            onClick={() => speech.speak(item.prompt.AudioText ?? '', 0.9)}
            disabled={!speech.ready}
            aria-label={`Nghe câu ${index + 1}`}
          >
            <Play className="size-4" aria-hidden />
            Nghe
          </Button>

          {/* Không bao giờ hiện lời thoại: đây là bài thi, hiện chữ là biến câu nghe thành câu đọc. */}
          <span className="text-xs text-muted">Nghe rồi chọn đáp án</span>
        </div>
      )}

      <div className="grid gap-2">
        {item.prompt.Choices.map((choice, choiceIndex) => {
          const selected = chosen === choiceIndex

          return (
            <label
              key={choice}
              className={cn(
                'flex cursor-pointer items-center gap-3 rounded-[var(--radius-control)] border px-3 py-2 text-sm transition-colors',
                selected
                  ? 'border-brand-500 bg-brand-50 dark:bg-brand-900/30'
                  : 'border-[var(--border-subtle)] hover:bg-[var(--surface-hover)]',
              )}
            >
              <input
                type="radio"
                name={item.code}
                className="sr-only"
                checked={selected}
                onChange={() => onChoose(choiceIndex)}
              />

              <span
                className={cn(
                  'flex size-5 shrink-0 items-center justify-center rounded-full border',
                  selected ? 'border-transparent bg-brand-600 text-white' : 'border-[var(--border-strong)]',
                )}
                aria-hidden
              >
                {selected && <Check className="size-3.5" />}
              </span>

              <span>{choice}</span>
            </label>
          )
        })}
      </div>
    </fieldset>
  )
}

function ChallengeResultView({
  lesson,
  result,
  onGoLearn,
  code,
}: {
  lesson: ChallengeOffer
  result: ChallengeResult
  onGoLearn: () => void
  code: string
}) {
  const tone = result.passed ? 'var(--color-success)' : 'var(--color-warning)'

  return (
    <Card>
      <CardBody className="space-y-5">
        <div className="text-center">
          <div
            className="mx-auto flex size-14 items-center justify-center rounded-full"
            style={{ backgroundColor: `color-mix(in oklch, ${tone} 18%, transparent)`, color: tone }}
          >
            {result.passed ? <Check className="size-7" aria-hidden /> : <X className="size-7" aria-hidden />}
          </div>

          <h1 className="mt-3 text-2xl font-semibold">{result.score} điểm</h1>
          <p className="mt-1 text-sm text-secondary">
            Đúng {result.correctCount} trên {result.totalCount} câu · cần {result.passThreshold} điểm ·{' '}
            {lesson.lessonCode}
          </p>
        </div>

        <p
          className="rounded-[var(--radius-card)] p-4 text-sm leading-relaxed"
          style={{ backgroundColor: `color-mix(in oklch, ${tone} 10%, transparent)` }}
        >
          {result.messageVi}
        </p>

        {result.wrongItemCodes.length > 0 && (
          <div className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4">
            <p className="flex items-center gap-2 text-sm font-medium">
              <TriangleAlert className="size-4 text-[var(--color-warning)]" aria-hidden />
              {result.wrongItemCodes.length} câu sai
            </p>
            {/* Chỉ nêu mã câu, KHÔNG hiện đáp án đúng: bài thi này còn thi lại được. */}
            <p className="mt-1 text-sm text-secondary">
              {result.passed
                ? 'Những câu này đã vào hàng ôn tập ngày mai.'
                : 'Học bài rồi thi lại, hoặc học hẳn theo cách thường cho chắc.'}
            </p>
          </div>
        )}

        {result.reviewItemsScheduled > 0 && (
          <p className="text-sm text-secondary">
            {result.reviewItemsScheduled} câu đã xếp vào hàng ôn tập. Câu đúng hẹn xa hơn, câu sai ôn ngay ngày mai.
          </p>
        )}

        <div className="flex flex-wrap gap-3">
          <Button onClick={onGoLearn}>
            {result.passed ? 'Học bài tiếp theo' : 'Về bảng điều khiển'}
            <ArrowRight className="size-4" aria-hidden />
          </Button>

          {!result.passed && (
            <Link to={`/learn/lesson/${code}`}>
              <Button variant="secondary">Học bài này</Button>
            </Link>
          )}
        </div>
      </CardBody>
    </Card>
  )
}
