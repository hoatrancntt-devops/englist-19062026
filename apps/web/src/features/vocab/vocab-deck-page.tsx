import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Check, Clock, Lightbulb, Play, Volume2 } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { useSpeech } from '@/lib/use-speech'
import { VOCAB_SPEEDS as SPEEDS, VOCAB_VOICES as VOICES } from '@/lib/vocab-voices'
import type { VocabVoiceId } from '@/lib/vocab-voices'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import { SpeakingDrill } from '@/features/lesson/steps/speaking-drill'
import type { VocabDeckView, VocabWordResult, VocabWordView } from './vocab-types'

/**
 * Một bộ từ vựng.
 *
 * Mặc định chỉ hiện những từ CHƯA THUỘC và những từ TỚI HẠN ÔN — mở ra thấy trăm thẻ một lúc
 * thì học viên đóng lại ngay. Muốn xem hết thì có nút riêng.
 */
export function VocabDeckPage() {
  const { code = '' } = useParams()
  const [showAll, setShowAll] = useState(false)

  const { data: deck, isLoading } = useQuery({
    queryKey: ['vocab', 'deck', code],
    queryFn: () => api.get<VocabDeckView>(`/api/v1/vocab/${code}`),
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  if (!deck) {
    return (
      <Card>
        <CardBody className="py-10 text-center text-secondary">Không tìm thấy bộ này.</CardBody>
      </Card>
    )
  }

  const learned = deck.words.filter((w) => w.learned).length
  const todo = deck.words.filter((w) => !w.learned || w.due)
  const visible = showAll ? deck.words : todo

  return (
    <div className="space-y-4">
      <Link
        to="/learn/tu-vung"
        className="inline-flex items-center gap-1.5 text-sm text-secondary hover:underline"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Tất cả các bộ
      </Link>

      <Card>
        <CardHeader title={deck.titleVi} description={deck.contextVi} />

        <CardBody className="space-y-3">
          <ProgressBar
            value={learned}
            max={deck.words.length}
            label={`${learned}/${deck.words.length} từ đã thuộc`}
          />

          <div className="flex flex-wrap items-center gap-3">
            <p className="text-sm text-secondary">
              Cần {deck.passScore} điểm khi đọc to thì mới tính là thuộc.
            </p>

            <Button size="sm" variant="ghost" onClick={() => setShowAll((v) => !v)}>
              {showAll ? `Chỉ hiện ${todo.length} từ cần học` : `Xem cả ${deck.words.length} từ`}
            </Button>
          </div>
        </CardBody>
      </Card>

      {visible.length === 0 ? (
        <Card>
          <CardBody className="space-y-3 py-10 text-center">
            <Check className="mx-auto size-10 text-[var(--color-success)]" aria-hidden />
            <p className="font-medium">Xong cả bộ này rồi.</p>
            <p className="text-sm text-secondary">
              Những từ đã thuộc sẽ tự hiện lại khi tới hạn ôn — quay lại sau vài ngày.
            </p>
          </CardBody>
        </Card>
      ) : (
        <ul className="grid gap-3">
          {visible.map((word) => (
            <VocabDeckCard key={word.id} word={word} deckCode={code} passScore={deck.passScore} />
          ))}
        </ul>
      )}
    </div>
  )
}

function VocabDeckCard({
  word,
  deckCode,
  passScore,
}: {
  word: VocabWordView
  deckCode: string
  passScore: number
}) {
  const queryClient = useQueryClient()
  const speech = useSpeech()

  const [speed, setSpeed] = useState(1)
  const [voice, setVoice] = useState<VocabVoiceId>(VOICES[0].id)
  const [showMnemonic, setShowMnemonic] = useState(false)
  const [result, setResult] = useState<VocabWordResult | null>(null)

  const record = useMutation({
    mutationFn: () => api.post<VocabWordResult>(`/api/v1/vocab/words/${word.id}`),
    onSuccess: (data) => {
      setResult(data)

      if (data.passed) {
        void queryClient.invalidateQueries({ queryKey: ['vocab', 'deck', deckCode] })
        void queryClient.invalidateQueries({ queryKey: ['vocab', 'decks'] })
      }
    },
  })

  return (
    <li className="space-y-3 rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-4">
      <div className="flex items-start gap-3">
        {word.emoji ? (
          <span className="text-3xl leading-none" aria-hidden>
            {word.emoji}
          </span>
        ) : null}

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-2">
            <span className="text-lg font-semibold">{word.term}</span>
            <span className="font-mono text-xs text-muted">{word.ipa}</span>

            {word.learned ? (
              <Badge tone="success">
                <Check className="size-3" aria-hidden />
                đã thuộc
              </Badge>
            ) : null}

            {word.due ? (
              <Badge tone="warning">
                <Clock className="size-3" aria-hidden />
                tới hạn ôn
              </Badge>
            ) : null}
          </div>

          <p className="mt-0.5 text-sm text-secondary">{word.meaningVi}</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => speech.speak(word.term, speed, voice)}
          className="flex items-center gap-1.5 rounded-[var(--radius-control)] bg-brand-50 px-2.5 py-1.5 text-sm font-medium text-brand-700 hover:bg-brand-100 dark:bg-brand-900/40 dark:text-brand-200"
        >
          <Volume2 className={cn('size-4', speech.speaking && 'animate-pulse')} aria-hidden />
          Nghe từ
        </button>

        <button
          type="button"
          onClick={() => speech.speak(word.chunk, speed, voice)}
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

      <p className="rounded bg-[var(--surface-sunken)] px-2 py-1 font-mono text-xs">{word.chunk}</p>

      {word.mnemonicVi ? (
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
              {word.mnemonicVi}
            </p>
          ) : null}
        </div>
      ) : null}

      <ul className="border-t border-[var(--border-subtle)] pt-3">
        <SpeakingDrill
          expectedText={word.term}
          promptVi={`Đọc to: ${word.meaningVi}`}
          ipa={word.ipa}
          activityId={word.id}
          contextType="vocab_word"
          // Máy chủ đã chấm và lưu bản ghi; giờ hỏi nó xem từ này đã tính là thuộc chưa.
          onGraded={() => record.mutate()}
        />
      </ul>

      {result ? (
        <p
          className={cn(
            'rounded-[var(--radius-control)] p-2 text-sm',
            result.passed
              ? 'bg-[color-mix(in_oklch,var(--color-success)_14%,transparent)] text-[var(--color-success-text)]'
              : 'bg-[color-mix(in_oklch,var(--color-warning)_14%,transparent)] text-[var(--color-warning-text)]',
          )}
        >
          {result.messageVi}
        </p>
      ) : (
        <p className="text-xs text-muted">Cần {passScore} điểm để tính là thuộc.</p>
      )}
    </li>
  )
}
