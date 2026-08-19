import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Check, Lightbulb, PenLine, RotateCcw, X } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, SkeletonCard } from '@/components/ui/feedback'
import type {
  WritingSetDetail,
  WritingSetSummary,
  WritingSubmitResult,
  WritingTaskView,
} from './writing-types'

/**
 * Bộ bài luyện viết.
 *
 * Khác bước viết trong bài học ở chỗ vào làm được ngay, không cần mở bài nào. Giống ở chỗ
 * quan trọng nhất: chấm bằng đúng bộ luật đó, nên điểm ở hai nơi có nghĩa như nhau.
 *
 * Câu mẫu chỉ hiện sau khi nộp. Trước đó nó nằm ở máy chủ cùng đáp án.
 */
export function WritingDrillPage() {
  const [openSet, setOpenSet] = useState<string | null>(null)

  const { data: sets, isLoading } = useQuery({
    queryKey: ['writing', 'list'],
    queryFn: () => api.get<WritingSetSummary[]>('/api/v1/writing'),
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  if (openSet) {
    return <SetRunner code={openSet} onBack={() => setOpenSet(null)} />
  }

  if (!sets || sets.length === 0) {
    return (
      <Card>
        <CardBody className="py-10 text-center text-secondary">Chưa có bộ bài viết nào.</CardBody>
      </Card>
    )
  }

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold">Luyện viết</h1>
        <p className="max-w-2xl text-secondary">
          Bộ bài viết riêng theo từng nhánh nghề. Chấm ngay tại máy chủ bằng luật — không chờ,
          không tốn phí, cùng một bài luôn ra cùng một điểm.
        </p>
      </header>

      <ul className="grid gap-3 sm:grid-cols-2">
        {sets.map((set) => (
          <li key={set.code}>
            <Card className="h-full">
              <CardBody className="flex h-full flex-col gap-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone="brand">{set.level}</Badge>
                  {set.passedCount >= set.taskCount ? (
                    <Badge tone="success">
                      <Check className="mr-1 inline size-3" aria-hidden />
                      Xong
                    </Badge>
                  ) : (
                    <Badge>
                      {set.passedCount}/{set.taskCount} bài đạt
                    </Badge>
                  )}
                </div>

                <div className="min-w-0 flex-1 space-y-1">
                  <h2 className="font-semibold">{set.titleVi}</h2>
                  <p className="text-sm text-secondary">{set.contextVi}</p>
                </div>

                <Button onClick={() => setOpenSet(set.code)} className="self-start">
                  <PenLine className="mr-2 size-4" aria-hidden />
                  {set.passedCount > 0 ? 'Làm tiếp' : 'Bắt đầu'}
                </Button>
              </CardBody>
            </Card>
          </li>
        ))}
      </ul>
    </div>
  )
}

function SetRunner({ code, onBack }: { code: string; onBack: () => void }) {
  const queryClient = useQueryClient()

  const { data: set, isLoading } = useQuery({
    queryKey: ['writing', 'set', code],
    queryFn: () => api.get<WritingSetDetail>(`/api/v1/writing/${code}`),
  })

  const goBack = () => {
    void queryClient.invalidateQueries({ queryKey: ['writing', 'list'] })
    onBack()
  }

  if (isLoading) {
    return <SkeletonCard />
  }

  if (!set) {
    return (
      <Card>
        <CardBody className="space-y-4 py-10 text-center">
          <p className="text-secondary">Không tìm thấy bộ bài này.</p>
          <Button onClick={goBack}>Quay lại</Button>
        </CardBody>
      </Card>
    )
  }

  return (
    <div className="space-y-6">
      <Button variant="ghost" onClick={goBack} className="-ml-2">
        <ArrowLeft className="mr-2 size-4" aria-hidden />
        Tất cả bộ bài
      </Button>

      <header className="space-y-2">
        <h1 className="text-2xl font-semibold">{set.titleVi}</h1>
        <p className="max-w-2xl text-secondary">{set.contextVi}</p>
      </header>

      <ol className="space-y-4">
        {set.tasks.map((task, index) => (
          <li key={task.code}>
            <TaskCard setCode={set.code} task={task} index={index} />
          </li>
        ))}
      </ol>
    </div>
  )
}

function TaskCard({
  setCode,
  task,
  index,
}: {
  setCode: string
  task: WritingTaskView
  index: number
}) {
  const [answers, setAnswers] = useState<string[]>(() => initialAnswers(task))
  const [result, setResult] = useState<WritingSubmitResult | null>(null)

  const submit = useMutation({
    mutationFn: () =>
      api.post<WritingSubmitResult>(`/api/v1/writing/${setCode}/submit`, {
        taskCode: task.code,
        answers,
      }),
    onSuccess: setResult,
  })

  const reset = () => {
    setAnswers(initialAnswers(task))
    setResult(null)
  }

  const empty = answers.every((a) => a.trim().length === 0)

  return (
    <Card>
      <CardHeader
        title={`Bài ${index + 1} — ${KIND_LABELS[task.kind]}`}
        description={task.promptVi}
        action={
          task.lastPassed ? (
            <Badge tone="success">
              <Check className="mr-1 inline size-3" aria-hidden />
              Đã đạt
            </Badge>
          ) : null
        }
      />

      <CardBody className="space-y-4 pt-0">
        {task.promptEn ? (
          <p className="rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2 font-mono text-sm">
            {task.promptEn}
          </p>
        ) : null}

        {task.kind === 'Reorder' ? (
          <FragmentPicker
            fragments={task.fragments}
            chosen={answers}
            locked={result !== null}
            onChange={setAnswers}
          />
        ) : task.kind === 'GuidedEmail' ? (
          <textarea
            value={answers[0] ?? ''}
            disabled={result !== null}
            rows={6}
            aria-label="Bài viết của bạn"
            placeholder="Viết bằng tiếng Anh…"
            onChange={(e) => setAnswers([e.target.value])}
            className="w-full rounded-[var(--radius-control)] border border-[var(--border-strong)] bg-[var(--surface-raised)] px-3 py-2 text-sm focus:border-brand-500 disabled:opacity-60"
          />
        ) : (
          <div className="grid gap-2">
            {answers.map((value, i) => (
              <input
                key={i}
                value={value}
                disabled={result !== null}
                aria-label={`Chỗ trống ${i + 1}`}
                placeholder={`Chỗ trống ${i + 1}`}
                onChange={(e) =>
                  setAnswers((prev) => prev.map((v, j) => (j === i ? e.target.value : v)))
                }
                className="w-full rounded-[var(--radius-control)] border border-[var(--border-strong)] bg-[var(--surface-raised)] px-3 py-2 text-sm focus:border-brand-500 disabled:opacity-60"
              />
            ))}
          </div>
        )}

        {task.hintVi && !result ? (
          <p className="flex gap-2 text-xs text-muted">
            <Lightbulb className="size-3.5 shrink-0" aria-hidden />
            {task.hintVi}
          </p>
        ) : null}

        {result ? (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone={result.passed ? 'success' : 'warning'}>
                {result.passed ? (
                  <Check className="mr-1 inline size-3" aria-hidden />
                ) : (
                  <X className="mr-1 inline size-3" aria-hidden />
                )}
                {Math.round(result.score)} điểm — cần {task.passScore}
              </Badge>
            </div>

            <p className="text-sm text-secondary">{result.feedbackVi}</p>

            {/* Câu mẫu chỉ tới được đây sau khi máy chủ đã chấm xong. */}
            {result.sampleEn ? (
              <div className="space-y-1 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] p-3">
                <p className="text-xs font-medium text-secondary">Câu mẫu</p>
                <p className="text-sm">{result.sampleEn}</p>
              </div>
            ) : null}

            <Button variant="ghost" onClick={reset} className="-ml-2">
              <RotateCcw className="mr-2 size-4" aria-hidden />
              Làm lại
            </Button>
          </div>
        ) : (
          <Button onClick={() => submit.mutate()} loading={submit.isPending} disabled={empty}>
            Nộp bài
          </Button>
        )}
      </CardBody>
    </Card>
  )
}

/**
 * Chọn mảnh theo thứ tự. Bấm mảnh để thêm vào cuối, bấm mảnh đã chọn để bỏ ra.
 *
 * Dùng nút bấm thay vì ô nhập chữ vì bài này đo khả năng sắp thứ tự, không đo khả năng
 * gõ lại đúng chính tả từng mảnh.
 */
function FragmentPicker({
  fragments,
  chosen,
  locked,
  onChange,
}: {
  fragments: string[]
  chosen: string[]
  locked: boolean
  onChange: (next: string[]) => void
}) {
  const picked = chosen.filter((c) => c.length > 0)
  const remaining = fragments.filter((f) => !picked.includes(f))

  return (
    <div className="space-y-3">
      <div className="min-h-12 rounded-[var(--radius-control)] border border-dashed border-[var(--border-strong)] p-2">
        {picked.length === 0 ? (
          <p className="p-1 text-sm text-muted">Bấm các mảnh bên dưới theo thứ tự bạn cho là đúng.</p>
        ) : (
          <ol className="flex flex-wrap gap-2">
            {picked.map((fragment, i) => (
              <li key={`${fragment}-${i}`}>
                <button
                  type="button"
                  disabled={locked}
                  onClick={() => onChange(picked.filter((_, j) => j !== i))}
                  className="rounded-[var(--radius-control)] bg-brand-50 px-2 py-1 text-sm text-brand-700 disabled:opacity-60 dark:bg-brand-900/40 dark:text-brand-200"
                >
                  {i + 1}. {fragment}
                </button>
              </li>
            ))}
          </ol>
        )}
      </div>

      <ul className="flex flex-wrap gap-2">
        {remaining.map((fragment) => (
          <li key={fragment}>
            <button
              type="button"
              disabled={locked}
              onClick={() => onChange([...picked, fragment])}
              className={cn(
                'rounded-[var(--radius-control)] border border-[var(--border-strong)] px-2 py-1 text-sm',
                'hover:bg-[var(--surface-hover)] disabled:opacity-60',
              )}
            >
              {fragment}
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

const KIND_LABELS: Record<WritingTaskView['kind'], string> = {
  FillBlank: 'Điền chỗ trống',
  Reorder: 'Sắp thứ tự',
  GuidedEmail: 'Viết email',
}

function initialAnswers(task: WritingTaskView): string[] {
  if (task.kind === 'FillBlank') {
    return Array(Math.max(1, task.blankCount)).fill('')
  }

  if (task.kind === 'Reorder') {
    return []
  }

  return ['']
}
