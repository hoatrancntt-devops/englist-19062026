import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Layers, Sparkles } from 'lucide-react'
import { api } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import type { VocabDeckSummary } from './vocab-types'

/**
 * Danh sách bộ từ vựng tần suất cao.
 *
 * Xếp theo bậc tần suất chứ không theo chủ đề: bậc 1 là nhóm thông dụng nhất, và riêng nó đã
 * chiếm khoảng một nửa số từ gặp trong lời nói hàng ngày. Học theo chủ đề nghe hợp lý hơn
 * nhưng học theo tần suất mới đưa người mất gốc tới ngưỡng nghe hiểu nhanh nhất.
 */
export function VocabDeckListPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['vocab', 'decks'],
    queryFn: () => api.get<VocabDeckSummary[]>('/api/v1/vocab'),
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  const decks = data ?? []
  const learned = decks.reduce((sum, d) => sum + d.learnedWords, 0)
  const total = decks.reduce((sum, d) => sum + d.totalWords, 0)

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Vốn từ thông dụng"
          description={
            total > 0
              ? `${learned} trên ${total} từ đã thuộc`
              : 'Bộ từ vựng đang được soạn.'
          }
          icon={<Sparkles className="size-5 text-brand-600" aria-hidden />}
        />

        <CardBody className="space-y-3">
          {total > 0 ? <ProgressBar value={learned} max={total} label="Đã thuộc" /> : null}

          <p className="text-sm text-secondary">
            Đây là vốn từ nền, học song song với lộ trình chứ không chặn bài nào. Mỗi từ nghe
            được bốn giọng, và bạn phải đọc to để máy chấm thì mới tính là thuộc.
          </p>
        </CardBody>
      </Card>

      {decks.length === 0 ? (
        <Card>
          <CardBody className="py-10 text-center text-secondary">
            Chưa có bộ nào. Bộ từ vựng được thêm dần theo từng bậc tần suất.
          </CardBody>
        </Card>
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {decks.map((deck) => (
            <li
              key={deck.code}
              className="rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-4"
            >
              <p className="flex flex-wrap items-center gap-2 font-medium">
                <Layers className="size-4 shrink-0 text-brand-600" aria-hidden />
                {deck.titleVi}
                <Badge>bậc {deck.band}</Badge>
                {deck.dueWords > 0 ? (
                  <Badge tone="warning">{deck.dueWords} từ cần ôn</Badge>
                ) : null}
              </p>

              <p className="mt-1.5 text-sm text-secondary">{deck.contextVi}</p>

              <div className="mt-3">
                <ProgressBar
                  value={deck.learnedWords}
                  max={deck.totalWords}
                  label={`${deck.learnedWords}/${deck.totalWords} từ`}
                />
              </div>

              <Link to={`/learn/tu-vung/${deck.code}`} className="mt-3 inline-block">
                <Button size="sm">
                  {deck.learnedWords === 0 ? 'Bắt đầu' : 'Học tiếp'}
                </Button>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
