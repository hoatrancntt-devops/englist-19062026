import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Check, MessageSquare, Play, RotateCcw, TriangleAlert, X } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { useSpeech } from '@/lib/use-speech'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, SkeletonCard } from '@/components/ui/feedback'
import type {
  RoleplayAnswerResult,
  RoleplayResult,
  RoleplayStart,
  RoleplaySummary,
  RoleplayTurn,
} from './roleplay-types'

/**
 * Roleplay.
 *
 * Ba điểm khác màn học, đều cố ý:
 *
 * Một, nhận xét chỉ hiện SAU khi chọn. Gắn nhãn tốt/cộc lốc lên từng lựa chọn trước
 * biến bài này thành trò bấm nhãn màu xanh.
 *
 * Hai, không có nút hoàn tác. Nói ra rồi thì trong đời thật cũng không rút lại được,
 * và giá trị của bài nằm ở chỗ chịu hậu quả của lựa chọn.
 *
 * Ba, lời của nhân vật hiện cả tiếng Anh lẫn tiếng Việt. Học viên mất gốc không đoán được
 * ngữ cảnh từ tiếng Anh, mà đoán sai ngữ cảnh thì lựa chọn nào cũng vô nghĩa.
 */
export function RoleplayPage() {
  const queryClient = useQueryClient()

  const [session, setSession] = useState<RoleplayStart | null>(null)
  const [turn, setTurn] = useState<RoleplayTurn | null>(null)
  const [feedback, setFeedback] = useState<{ text: string; quality: string } | null>(null)
  const [result, setResult] = useState<RoleplayResult | null>(null)

  const { data: scenarios, isLoading } = useQuery({
    queryKey: ['roleplay', 'list'],
    queryFn: () => api.get<RoleplaySummary[]>('/api/v1/roleplay'),
  })

  const start = useMutation({
    mutationFn: (code: string) => api.post<RoleplayStart>(`/api/v1/roleplay/${code}/start`),
    onSuccess: (data) => {
      setSession(data)
      setTurn(data.turn)
      setFeedback(null)
      setResult(null)
    },
  })

  const choose = useMutation({
    mutationFn: (body: { attemptId: string; nodeCode: string; choiceIndex: number }) =>
      api.post<RoleplayAnswerResult>('/api/v1/roleplay/choose', body),
    onSuccess: (data) => {
      setTurn(data.next)
      setFeedback(data.feedbackVi ? { text: data.feedbackVi, quality: data.quality } : null)

      if (data.result) {
        setResult(data.result)
        void queryClient.invalidateQueries({ queryKey: ['roleplay'] })
      }
    },
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  if (session && turn) {
    return (
      <Conversation
        session={session}
        turn={turn}
        feedback={feedback}
        result={result}
        submitting={choose.isPending}
        onChoose={(choiceIndex) =>
          choose.mutate({ attemptId: session.attemptId, nodeCode: turn.nodeCode, choiceIndex })
        }
        onRestart={() => start.mutate(session.scenario.code)}
        onExit={() => {
          setSession(null)
          setTurn(null)
        }}
      />
    )
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Đóng vai"
          description="Tình huống thật của nghề: gọi vendor, xin duyệt thay đổi, bàn giao sự cố lúc 2 giờ sáng."
          icon={<MessageSquare className="size-5 text-brand-600" aria-hidden />}
        />
        <CardBody>
          <p className="text-sm text-secondary">
            Mỗi lượt bạn chọn một cách đáp. Không có nút hoàn tác — nói ra rồi thì phải chịu
            hậu quả của câu đó, giống ngoài đời.
          </p>
        </CardBody>
      </Card>

      <div className="grid gap-3 sm:grid-cols-2">
        {(scenarios ?? []).map((scenario) => (
          <ScenarioCard
            key={scenario.code}
            scenario={scenario}
            starting={start.isPending && start.variables === scenario.code}
            onStart={() => start.mutate(scenario.code)}
          />
        ))}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------

function ScenarioCard({
  scenario,
  starting,
  onStart,
}: {
  scenario: RoleplaySummary
  starting: boolean
  onStart: () => void
}) {
  return (
    <Card>
      <CardBody className="flex h-full flex-col gap-3">
        <div className="flex items-start justify-between gap-2">
          <div>
            <p className="font-medium">{scenario.titleVi}</p>
            <p className="mt-0.5 text-xs text-muted">
              {scenario.code} · {scenario.level} · {scenario.turnCount} lượt
            </p>
          </div>

          {scenario.lastOutcome && <OutcomeBadge outcome={scenario.lastOutcome} score={scenario.lastScore} />}
        </div>

        <p className="flex-1 text-sm text-secondary">{scenario.contextVi}</p>

        <p className="text-xs text-muted">Đối thoại với {scenario.partnerName}</p>

        <Button onClick={onStart} loading={starting} size="sm" className="self-start">
          <Play className="size-4" aria-hidden />
          {scenario.lastOutcome ? 'Chơi lại' : 'Bắt đầu'}
        </Button>
      </CardBody>
    </Card>
  )
}

function OutcomeBadge({ outcome, score }: { outcome: string; score: number | null }) {
  if (outcome === 'Completed') {
    return <Badge tone="success">{score !== null ? `${Math.round(score)} điểm` : 'Đã xong'}</Badge>
  }

  if (outcome === 'CompletedWithHints') {
    return <Badge tone="warning">{score !== null ? `${Math.round(score)} điểm` : 'Xong, còn vướng'}</Badge>
  }

  return <Badge tone="neutral">Chưa xong</Badge>
}

function Conversation({
  session,
  turn,
  feedback,
  result,
  submitting,
  onChoose,
  onRestart,
  onExit,
}: {
  session: RoleplayStart
  turn: RoleplayTurn
  feedback: { text: string; quality: string } | null
  result: RoleplayResult | null
  submitting: boolean
  onChoose: (choiceIndex: number) => void
  onRestart: () => void
  onExit: () => void
}) {
  const speech = useSpeech()

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title={session.scenario.titleVi}
          description={`Đối thoại với ${session.scenario.partnerName}`}
          icon={<MessageSquare className="size-5 text-brand-600" aria-hidden />}
          action={
            <Button variant="ghost" size="sm" onClick={onExit}>
              Thoát
            </Button>
          }
        />
      </Card>

      <Card>
        <CardBody className="space-y-4">
          {/* Lời của nhân vật: tiếng Anh để học, tiếng Việt để không đoán sai ngữ cảnh. */}
          <div className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4">
            <div className="flex items-start justify-between gap-3">
              <p className="text-base leading-relaxed">{turn.partnerLineEn}</p>

              <Button
                size="sm"
                variant="ghost"
                onClick={() => speech.speak(turn.partnerLineEn, 0.95)}
                disabled={!speech.ready}
                aria-label="Nghe câu này"
              >
                <Play className="size-4" aria-hidden />
              </Button>
            </div>

            <p className="mt-2 text-sm text-secondary">{turn.partnerLineVi}</p>
          </div>

          {feedback && <FeedbackNote text={feedback.text} quality={feedback.quality} />}

          {!turn.isTerminal && (
            <div className="space-y-2">
              <p className="text-sm font-medium">Bạn đáp thế nào?</p>

              {turn.choices.map((choice) => (
                <button
                  key={choice.index}
                  type="button"
                  onClick={() => onChoose(choice.index)}
                  disabled={submitting}
                  className={cn(
                    'w-full rounded-[var(--radius-control)] border border-[var(--border-subtle)] p-3 text-left transition-colors',
                    'hover:border-brand-400 hover:bg-[var(--surface-hover)] disabled:opacity-60',
                  )}
                >
                  <p className="text-sm">{choice.en}</p>
                  <p className="mt-1 text-xs text-muted">{choice.vi}</p>
                </button>
              ))}
            </div>
          )}

          {turn.isTerminal && turn.summaryVi && (
            <div
              className="rounded-[var(--radius-card)] p-4"
              style={{
                backgroundColor: `color-mix(in oklch, ${turn.success ? 'var(--color-success)' : 'var(--color-warning)'} 10%, transparent)`,
              }}
            >
              <p className="flex items-center gap-2 text-sm font-medium">
                {turn.success ? (
                  <Check className="size-4 text-[var(--color-success)]" aria-hidden />
                ) : (
                  <X className="size-4 text-[var(--color-warning)]" aria-hidden />
                )}
                {turn.success ? 'Xong việc' : 'Hội thoại dừng sớm'}
              </p>
              <p className="mt-2 text-sm leading-relaxed">{turn.summaryVi}</p>
            </div>
          )}

          {result && <ResultPanel result={result} onRestart={onRestart} onExit={onExit} />}
        </CardBody>
      </Card>
    </div>
  )
}

function FeedbackNote({ text, quality }: { text: string; quality: string }) {
  const isWrong = quality === 'wrong'

  return (
    <div
      className="flex items-start gap-2 rounded-[var(--radius-control)] p-3 text-sm leading-relaxed"
      style={{
        backgroundColor: `color-mix(in oklch, ${isWrong ? 'var(--color-danger)' : 'var(--color-warning)'} 10%, transparent)`,
      }}
    >
      <TriangleAlert
        className="mt-0.5 size-4 shrink-0"
        style={{ color: isWrong ? 'var(--color-danger)' : 'var(--color-warning)' }}
        aria-hidden
      />
      <span>{text}</span>
    </div>
  )
}

function ResultPanel({
  result,
  onRestart,
  onExit,
}: {
  result: RoleplayResult
  onRestart: () => void
  onExit: () => void
}) {
  return (
    <div className="space-y-3 border-t border-[var(--border-subtle)] pt-4">
      <p className="text-lg font-semibold">{result.score} điểm</p>
      <p className="text-sm text-secondary">{result.messageVi}</p>

      <div className="flex flex-wrap gap-2 text-xs">
        <Badge tone="success">{result.goodChoices} lượt đạt</Badge>
        {result.curtChoices > 0 && <Badge tone="warning">{result.curtChoices} lượt cộc lốc</Badge>}
        {result.wrongChoices > 0 && <Badge tone="danger">{result.wrongChoices} lượt sai hướng</Badge>}
      </div>

      <div className="flex flex-wrap gap-3">
        <Button onClick={onRestart}>
          <RotateCcw className="size-4" aria-hidden />
          Chơi lại
        </Button>

        <Button variant="secondary" onClick={onExit}>
          Chọn kịch bản khác
          <ArrowRight className="size-4" aria-hidden />
        </Button>
      </div>
    </div>
  )
}
