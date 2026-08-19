import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import {
  ClipboardCheck,
  ArrowRight,
  ArrowLeft,
  Clock,
  CheckCircle2,
  AlertTriangle,
  Info,
} from 'lucide-react'
import { api, ApiError } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { Card, CardBody } from '@/components/ui/card'
import { ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import { SkillBadge, SKILL_META, SKILL_ORDER } from '@/components/skill-badge'
import { PlacementItem } from './placement-items'
import { promptLabelId } from './placement-types'
import type {
  PlacementCard,
  PlacementProgress,
  PlacementResponse,
  PlacementResult,
  PlacementSession,
} from './placement-types'

/**
 * Bài xếp lớp.
 *
 * Ba quyết định định hình component này:
 *
 * Một, mỗi câu gửi lên ngay khi rời khỏi nó, không gom cuối bài. Mười tám phút làm lại
 * từ đầu vì mất mạng là lý do chính khiến người ta bỏ bài xếp lớp giữa chừng.
 *
 * Hai, quay lại câu trước được. Bài thi không cho sửa làm người ta căng thẳng và
 * trả lời tệ hơn thực lực — mà đo thực lực mới là việc của bài này.
 *
 * Ba, không hiện đúng/sai sau từng câu. Không phải vì bí mật, mà vì biết mình vừa sai
 * ba câu liên tiếp sẽ làm người mất gốc bỏ ngang ở câu thứ tư.
 */
export function PlacementPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [session, setSession] = useState<PlacementSession | null>(null)
  const [result, setResult] = useState<PlacementResult | null>(null)
  const [index, setIndex] = useState(0)
  const [responses, setResponses] = useState<Record<string, PlacementResponse>>({})

  // Đã có kết quả từ trước thì hiện luôn, không bắt làm lại.
  const existing = useQuery({
    queryKey: ['placement', 'result'],
    queryFn: async () => {
      try {
        return await api.get<PlacementResult>('/api/v1/placement/result')
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null
        }
        throw error
      }
    },
  })

  const start = useMutation({
    mutationFn: () => api.post<PlacementSession>('/api/v1/placement/start'),
    onSuccess: (data) => {
      setSession(data)
      // Mở lại lượt dở thì nhảy tới câu chưa trả lời đầu tiên.
      const firstUnanswered = data.cards.findIndex((c) => !data.answeredItemCodes.includes(c.itemCode))
      setIndex(firstUnanswered === -1 ? 0 : firstUnanswered)
    },
  })

  const answer = useMutation({
    mutationFn: (body: {
      attemptId: string
      itemCode: string
      response: PlacementResponse
      responseSeconds: number
    }) => api.post<PlacementProgress>('/api/v1/placement/answer', body),
  })

  const submit = useMutation({
    mutationFn: (attemptId: string) =>
      api.post<PlacementResult>('/api/v1/placement/submit', { attemptId }),
    onSuccess: (data) => {
      setResult(data)
      // Bậc và tầng vừa đổi, nên lộ trình và bảng điều khiển phải đọc lại.
      queryClient.invalidateQueries({ queryKey: ['learning'] })
      queryClient.invalidateQueries({ queryKey: ['placement'] })
    },
  })

  if (result) {
    return <ResultView result={result} onGoLearn={() => navigate('/learn')} />
  }

  if (!session) {
    if (existing.isLoading) {
      return <SkeletonCard />
    }

    if (existing.data) {
      return <ResultView result={existing.data} onGoLearn={() => navigate('/learn')} retakeable onRetake={() => start.mutate()} />
    }

    return (
      <IntroView
        onStart={() => start.mutate()}
        starting={start.isPending}
        error={start.error instanceof ApiError ? start.error.message : null}
      />
    )
  }

  return (
    <QuestionView
      session={session}
      index={index}
      responses={responses}
      onSetIndex={setIndex}
      onAnswer={(card, response, seconds) => {
        setResponses((prev) => ({ ...prev, [card.itemCode]: response }))

        // Trả về promise để lúc nộp bài còn chờ được. Câu trả lời lưu hỏng thì
        // nuốt lỗi ở đây: máy chủ chấm theo những gì nó nhận được, và màn kết quả
        // hiện "làm X trên Y câu" nên học viên vẫn thấy có câu chưa được tính.
        return answer
          .mutateAsync({
            attemptId: session.attemptId,
            itemCode: card.itemCode,
            response,
            responseSeconds: seconds,
          })
          .then(() => undefined)
          .catch(() => undefined)
      }}
      onSubmit={() => submit.mutate(session.attemptId)}
      submitting={submit.isPending}
    />
  )
}

// ---------------------------------------------------------------
// Màn mở đầu
// ---------------------------------------------------------------

function IntroView({
  onStart,
  starting,
  error,
}: {
  onStart: () => void
  starting: boolean
  error: string | null
}) {
  return (
    <Card>
      <CardBody className="space-y-5">
        <div className="flex items-start gap-3">
          <ClipboardCheck className="mt-1 size-6 shrink-0 text-brand-600" aria-hidden />
          <div>
            <h1 className="text-xl font-semibold">Bài xếp lớp</h1>
            <p className="mt-1 text-sm text-secondary">
              Khoảng 18 phút. Kết quả quyết định bạn bắt đầu từ đâu — làm một lần, tiết kiệm
              hàng tuần học sai chỗ.
            </p>
          </div>
        </div>

        <ul className="space-y-2 text-sm">
          {[
            'Không hiện đúng sai sau từng câu. Xem toàn bộ kết quả lúc nộp bài.',
            'Quay lại câu trước và sửa được, không bị khoá một chiều.',
            'Đóng tab giữa chừng cũng không mất bài: mở lại là tiếp từ chỗ đang dở.',
            'Không biết thì cứ bỏ trống. Đoán bừa làm kết quả sai lệch và bạn bị đặt nhầm chỗ.',
          ].map((line) => (
            <li key={line} className="flex items-start gap-2">
              <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-brand-500" aria-hidden />
              {line}
            </li>
          ))}
        </ul>

        {error && (
          <p className="rounded-[var(--radius-control)] bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
            {error}
          </p>
        )}

        <div className="flex flex-wrap items-center gap-3">
          <Button onClick={onStart} loading={starting}>
            Bắt đầu
            <ArrowRight className="size-4" aria-hidden />
          </Button>

          <Link to="/learn" className="text-sm text-secondary underline-offset-4 hover:underline">
            Để sau, học bài đầu tiên trước
          </Link>
        </div>
      </CardBody>
    </Card>
  )
}

// ---------------------------------------------------------------
// Màn làm bài
// ---------------------------------------------------------------

function QuestionView({
  session,
  index,
  responses,
  onSetIndex,
  onAnswer,
  onSubmit,
  submitting,
}: {
  session: PlacementSession
  index: number
  responses: Record<string, PlacementResponse>
  onSetIndex: (index: number) => void
  /** Trả về promise đã lưu xong, để nút Nộp bài chờ được câu cuối. */
  onAnswer: (card: PlacementCard, response: PlacementResponse, seconds: number) => Promise<void>
  onSubmit: () => void
  submitting: boolean
}) {
  const card = session.cards[index]
  const total = session.cards.length
  const isLast = index === total - 1

  // Thời điểm câu này hiện ra. Hiệu số tới lúc trả lời là đầu vào của chỉ số đoán mò,
  // nên phải đặt lại mỗi khi đổi câu, không phải mỗi khi component vẽ lại.
  const shownAt = useRef(Date.now())
  useEffect(() => {
    shownAt.current = Date.now()
  }, [index])

  const [draft, setDraft] = useState<PlacementResponse | null>(responses[card.itemCode] ?? null)
  useEffect(() => {
    setDraft(responses[card.itemCode] ?? null)
    // Chỉ theo dõi mã câu: theo dõi cả responses sẽ ghi đè cái người dùng đang gõ.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [card.itemCode])

  const answeredCount = useMemo(
    () => session.cards.filter((c) => responses[c.itemCode] !== undefined).length,
    [session.cards, responses],
  )

  const commit = (): Promise<void> => {
    if (draft === null) {
      return Promise.resolve()
    }

    return onAnswer(card, draft, Math.round((Date.now() - shownAt.current) / 1000))
  }

  const goTo = (next: number) => {
    commit()
    onSetIndex(next)
  }

  return (
    <div className="space-y-4">
      <header className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
          <span className="font-medium">
            Câu {index + 1} trên {total}
          </span>

          <span className="flex items-center gap-3 text-secondary">
            {card.skill && <SkillBadge skill={card.skill} />}
            <Deadline at={session.deadlineAt} />
          </span>
        </div>

        <ProgressBar value={answeredCount} max={total} label="Đã trả lời" />
      </header>

      <Card>
        <CardBody className="space-y-4">
          <p id={promptLabelId(card.itemCode)} className="font-medium">
            {card.prompt.instructionVi}
          </p>

          <PlacementItem card={card} value={draft} onChange={setDraft} />
        </CardBody>
      </Card>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="ghost" onClick={() => goTo(index - 1)} disabled={index === 0}>
          <ArrowLeft className="size-4" aria-hidden />
          Câu trước
        </Button>

        {isLast ? (
          <Button
            // Phải CHỜ câu cuối lưu xong rồi mới nộp. Bắn hai request song song thì
            // máy chủ chấm trước khi câu cuối tới nơi và câu đó bị tính 0 điểm —
            // lỗi im lặng, học viên chỉ thấy điểm thấp hơn mà không hiểu vì sao.
            onClick={async () => {
              await commit()
              onSubmit()
            }}
            loading={submitting}
          >
            Nộp bài
            <CheckCircle2 className="size-4" aria-hidden />
          </Button>
        ) : (
          <Button onClick={() => goTo(index + 1)}>
            {draft === null ? 'Bỏ qua' : 'Câu tiếp'}
            <ArrowRight className="size-4" aria-hidden />
          </Button>
        )}
      </div>
    </div>
  )
}

/** Đồng hồ đếm ngược tới hạn nộp. Chỉ hiện khi còn dưới năm phút để không tạo áp lực suốt bài. */
function Deadline({ at }: { at: string }) {
  const [remaining, setRemaining] = useState(() => Date.parse(at) - Date.now())

  useEffect(() => {
    const timer = setInterval(() => setRemaining(Date.parse(at) - Date.now()), 1000)
    return () => clearInterval(timer)
  }, [at])

  if (remaining > 5 * 60 * 1000) {
    return null
  }

  const minutes = Math.max(0, Math.floor(remaining / 60000))
  const seconds = Math.max(0, Math.floor((remaining % 60000) / 1000))

  return (
    <span className={cn('flex items-center gap-1', remaining <= 60000 && 'text-amber-600')}>
      <Clock className="size-4" aria-hidden />
      còn {minutes}:{String(seconds).padStart(2, '0')}
    </span>
  )
}

// ---------------------------------------------------------------
// Màn kết quả
// ---------------------------------------------------------------

const LAYER_LABELS: Record<string, string> = {
  Life: 'Đời sống',
  Office: 'Văn phòng',
  Professional: 'Chuyên môn',
}

function ResultView({
  result,
  onGoLearn,
  retakeable,
  onRetake,
}: {
  result: PlacementResult
  onGoLearn: () => void
  retakeable?: boolean
  onRetake?: () => void
}) {

  return (
    <div className="space-y-4">
      <Card>
        <CardBody className="space-y-4">
          <div className="flex flex-wrap items-center gap-3">
            <span className="rounded-[var(--radius-card)] bg-brand-600 px-4 py-2 text-2xl font-bold text-white">
              {result.band}
            </span>

            <div>
              <h1 className="text-lg font-semibold">Bạn bắt đầu ở tầng {LAYER_LABELS[result.suggestedLayer]}</h1>
              <p className="text-sm text-secondary">
                Làm {result.answered} trên {result.total} câu · đề {result.formCode}
              </p>
            </div>
          </div>

          <p className="text-sm leading-relaxed">{result.summaryVi}</p>
        </CardBody>
      </Card>

      <Card>
        <CardBody className="space-y-3">
          <h2 className="font-semibold">Bốn trục kỹ năng</h2>

          {/* Thứ tự lấy từ SKILL_ORDER — ưu tiên kỹ năng của cả hệ thống nằm ở một chỗ duy nhất. */}
          {SKILL_ORDER.map((skill) => {
            const unmeasured = result.unmeasuredSkills.includes(skill)
            const score = result.skillScores[skill]

            return (
              <div key={skill} className="space-y-1">
                <div className="flex items-center justify-between text-sm">
                  <span>{SKILL_META[skill].labelVi}</span>
                  {/* Trục chưa đo hiện chữ, KHÔNG hiện 0 điểm: hai thứ đó khác nhau
                      và nhầm lẫn sẽ khiến học viên tưởng mình dốt hẳn một kỹ năng. */}
                  <span className={cn('font-medium', unmeasured && 'text-muted')}>
                    {unmeasured ? 'chưa đo được' : Math.round(score ?? 0)}
                  </span>
                </div>

                {!unmeasured && <ProgressBar value={score ?? 0} max={100} />}
              </div>
            )
          })}

          <div className="flex items-center justify-between border-t border-[var(--border-subtle)] pt-3 text-sm">
            <span className="text-secondary">Từ vựng và ngữ pháp</span>
            <span className="font-medium">{Math.round(result.vocabGrammarScore)}</span>
          </div>
        </CardBody>
      </Card>

      {result.notesVi.length > 0 && (
        <Card>
          <CardBody className="space-y-3">
            <h2 className="font-semibold">Vài điều về kết quả này</h2>

            {result.notesVi.map((note) => (
              <p key={note} className="flex items-start gap-2 text-sm leading-relaxed">
                {note.includes('rất nhanh') ? (
                  <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-600" aria-hidden />
                ) : (
                  <Info className="mt-0.5 size-4 shrink-0 text-brand-600" aria-hidden />
                )}
                {note}
              </p>
            ))}
          </CardBody>
        </Card>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={onGoLearn}>
          Bắt đầu học
          <ArrowRight className="size-4" aria-hidden />
        </Button>

        {retakeable && onRetake && (
          <Button variant="ghost" onClick={onRetake}>
            Thi lại bằng đề khác
          </Button>
        )}
      </div>
    </div>
  )
}
