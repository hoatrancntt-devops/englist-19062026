import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Check, Layers, X } from 'lucide-react'
import { api } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, SkeletonCard } from '@/components/ui/feedback'
import { ChallengeQuestion } from '@/features/lesson/challenge-page'
import type { ChallengeItem } from '@/features/lesson/challenge-types'

interface ConsolidationItem extends ChallengeItem {
  lessonCode: string
}

interface ConsolidationOffer {
  pending: boolean
  groupIndex: number
  lessonCodes: string[]
  lessonTitles: string[]
  passThreshold: number
  items: ConsolidationItem[]
  messageVi: string
}

interface ConsolidationResult {
  passed: boolean
  score: number
  passThreshold: number
  correctCount: number
  totalCount: number
  wrongItemCodes: string[]
  messageVi: string
}

/**
 * Bài tổng hợp: ôn lại đúng ba bài vừa thạo.
 *
 * Nộp trọn gói như bài thi vượt, cùng lý do — chấm dần sẽ cho học viên biết mình đang bao nhiêu
 * điểm rồi dừng ở câu vừa đủ.
 *
 * Khác thi vượt ở một chỗ quan trọng: trượt KHÔNG phải chờ. Đây là cổng bắt buộc trên đường học,
 * bắt chờ nửa ngày thì thành hình phạt cho việc học chậm.
 */
export function ConsolidationPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [chosen, setChosen] = useState<Record<string, number>>({})
  const [result, setResult] = useState<ConsolidationResult | null>(null)

  const { data: offer, isLoading } = useQuery({
    queryKey: ['learning', 'consolidation'],
    queryFn: () => api.get<ConsolidationOffer>('/api/v1/learning/consolidation'),
  })

  const submit = useMutation({
    mutationFn: (responses: { itemCode: string; chosenIndex: number }[]) =>
      api.post<ConsolidationResult>('/api/v1/learning/consolidation', { responses }),
    onSuccess: (data) => {
      setResult(data)

      if (data.passed) {
        // Lộ trình vừa mở lại nên mọi thẻ bài đang giữ trạng thái khoá cũ.
        void queryClient.invalidateQueries({ queryKey: ['learning'] })
      }
    },
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  if (!offer?.pending) {
    return (
      <Card>
        <CardBody className="space-y-4 py-10 text-center">
          <p className="text-secondary">{offer?.messageVi ?? 'Chưa tới lượt ôn tổng hợp.'}</p>
          <div>
            <Button onClick={() => navigate('/learn/roadmap')}>Về lộ trình</Button>
          </div>
        </CardBody>
      </Card>
    )
  }

  if (result) {
    return <ResultView result={result} onRetry={() => setResult(null)} />
  }

  const answered = offer.items.filter((i) => chosen[i.code] !== undefined).length

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title={`Bài tổng hợp ${offer.groupIndex}`}
          description={offer.messageVi}
          icon={<Layers className="size-5 text-brand-600" aria-hidden />}
        />

        <CardBody className="space-y-2 pt-0">
          <p className="text-sm font-medium">Ôn lại ba bài:</p>
          <ul className="space-y-1">
            {offer.lessonCodes.map((code, i) => (
              <li key={code} className="text-sm text-secondary">
                <Link
                  to={`/learn/lesson/${code}`}
                  className="font-medium text-brand-600 underline dark:text-brand-300"
                >
                  {code}
                </Link>{' '}
                — {offer.lessonTitles[i]}
              </li>
            ))}
          </ul>

          <p className="pt-2 text-sm text-secondary">
            Đạt {offer.passThreshold} điểm là mở tiếp lộ trình. Chưa đạt thì làm lại ngay,
            không phải chờ.
          </p>
        </CardBody>
      </Card>

      <Card>
        <CardBody className="space-y-6">
          {offer.items.map((item, index) => (
            <div key={item.code} className="space-y-1">
              <Badge>{item.lessonCode}</Badge>
              <ChallengeQuestion
                item={item}
                index={index}
                chosen={chosen[item.code]}
                onChoose={(choiceIndex) =>
                  setChosen((prev) => ({ ...prev, [item.code]: choiceIndex }))
                }
              />
            </div>
          ))}

          <div className="flex flex-wrap items-center gap-3 border-t border-[var(--border-subtle)] pt-4">
            <Button
              onClick={() =>
                submit.mutate(
                  offer.items.map((item) => ({
                    itemCode: item.code,
                    // -1 là bỏ trống, máy chủ tính sai. Loại câu bỏ trống khỏi bài thì
                    // bỏ trống hết sẽ ra 100 điểm.
                    chosenIndex: chosen[item.code] ?? -1,
                  })),
                )
              }
              loading={submit.isPending}
            >
              Nộp bài tổng hợp
              <ArrowRight className="size-4" aria-hidden />
            </Button>

            {answered < offer.items.length && (
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

function ResultView({
  result,
  onRetry,
}: {
  result: ConsolidationResult
  onRetry: () => void
}) {
  const navigate = useNavigate()

  return (
    <Card>
      <CardBody className="space-y-4 py-8 text-center">
        <div className="flex justify-center">
          {result.passed ? (
            <Check className="size-10 text-[var(--color-success)]" aria-hidden />
          ) : (
            <X className="size-10 text-[var(--color-warning)]" aria-hidden />
          )}
        </div>

        <p className="text-2xl font-semibold">
          {Math.round(result.score)} điểm
          <span className="text-base font-normal text-secondary">
            {' '}
            — cần {result.passThreshold}
          </span>
        </p>

        <p className="text-secondary">{result.messageVi}</p>

        <p className="text-sm text-muted">
          Đúng {result.correctCount}/{result.totalCount} câu.
        </p>

        <div className="flex flex-wrap justify-center gap-3 pt-2">
          {result.passed ? (
            <Button onClick={() => navigate('/learn/roadmap')}>Học tiếp</Button>
          ) : (
            <>
              <Button onClick={onRetry}>Làm lại</Button>
              <Button variant="ghost" onClick={() => navigate('/learn/roadmap')}>
                Xem lại ba bài
              </Button>
            </>
          )}
        </div>
      </CardBody>
    </Card>
  )
}
