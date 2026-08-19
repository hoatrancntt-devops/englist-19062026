import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Check, ChevronRight, Flag, Lock, Map as MapIcon, Rocket, Eye } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, ProgressBar, SkeletonCard } from '@/components/ui/feedback'

interface LessonCard {
  code: string
  titleVi: string
  track: string
  layer: string
  level: string
  unitCode: string
  estimatedMinutes: number
  isCheckpoint: boolean
  illustration: string | null
  state: string
  mastery: number
  supportedSkills: string[]
  lockExplanationVi: string
  unlockedByChallenge: boolean
}

interface RoadmapResult {
  lessons: LessonCard[]
  next: { card: LessonCard; reasonVi: string } | null
  totalPublished: number
  mastered: number
  inProgress: number
}

const LAYER_LABEL: Record<string, string> = {
  Life: 'Đời sống',
  Office: 'Văn phòng',
  Professional: 'Chuyên môn',
}

/** Thứ tự hiển thị ba tầng. Không dựa vào thứ tự API trả về. */
const LAYER_ORDER = ['Life', 'Office', 'Professional']

/**
 * Lộ trình đầy đủ.
 *
 * Điểm khác biệt so với một danh sách bài thường: mỗi bài đang khoá đều mang theo con số
 * còn thiếu, và bài nào cũng có đường thoát bằng thi vượt. Danh sách bài mà không nói được
 * vì sao bài đó khoá chỉ làm người học bực, không làm họ học được gì.
 */
export function RoadmapPage() {
  const [openLayer, setOpenLayer] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['learning', 'roadmap'],
    queryFn: () => api.get<RoadmapResult>('/api/v1/learning/roadmap'),
  })

  const byLayer = useMemo(() => {
    const groups = new Map<string, LessonCard[]>()

    for (const lesson of data?.lessons ?? []) {
      const list = groups.get(lesson.layer) ?? []
      list.push(lesson)
      groups.set(lesson.layer, list)
    }

    return LAYER_ORDER.filter((layer) => groups.has(layer)).map((layer) => ({
      layer,
      lessons: groups.get(layer) ?? [],
    }))
  }, [data])

  if (isLoading) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    )
  }

  if (!data) {
    return null
  }

  // Mở sẵn tầng chứa bài kế tiếp: đó là chỗ học viên cần nhìn trước nhất.
  const defaultLayer = data.next?.card.layer ?? byLayer[0]?.layer ?? null
  const expanded = openLayer ?? defaultLayer

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Lộ trình"
          description={`${data.mastered} trên ${data.totalPublished} bài đã thạo`}
          icon={<MapIcon className="size-5 text-brand-600" aria-hidden />}
        />
        <CardBody className="space-y-4">
          <ProgressBar value={data.mastered} max={data.totalPublished} label="Đã thạo" />

          {data.next && (
            <div className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4">
              <p className="text-xs font-medium uppercase tracking-wide text-muted">Bài kế tiếp</p>
              <p className="mt-1 font-medium">
                {data.next.card.code} · {data.next.card.titleVi}
              </p>
              <p className="mt-1 text-sm text-secondary">{data.next.reasonVi}</p>

              <Link to={`/learn/lesson/${data.next.card.code}`} className="mt-3 inline-block">
                <Button size="sm">
                  Học bài này
                  <ChevronRight className="size-4" aria-hidden />
                </Button>
              </Link>
            </div>
          )}
        </CardBody>
      </Card>

      {byLayer.map(({ layer, lessons }) => {
        // Cùng cách đếm với thanh "Đã thạo" ở trên: bài mới đánh dấu biết không tính.
        const mastered = lessons.filter((l) => l.state === 'Mastered' && !l.unlockedByChallenge).length
        const isOpen = expanded === layer

        return (
          <Card key={layer}>
            <button
              type="button"
              onClick={() => setOpenLayer(isOpen ? '' : layer)}
              aria-expanded={isOpen}
              className="flex w-full items-center justify-between gap-3 p-4 text-left hover:bg-[var(--surface-hover)]"
            >
              <span>
                <span className="font-medium">Tầng {LAYER_LABEL[layer] ?? layer}</span>
                <span className="ml-2 text-sm text-secondary">
                  {mastered}/{lessons.length} bài
                </span>
              </span>

              <ChevronRight
                className={cn('size-5 shrink-0 text-muted transition-transform', isOpen && 'rotate-90')}
                aria-hidden
              />
            </button>

            {isOpen && (
              <div className="border-t border-[var(--border-subtle)] p-4">
                <ul className="space-y-2">
                  {lessons.map((lesson) => (
                    <LessonRow key={lesson.code} lesson={lesson} />
                  ))}
                </ul>
              </div>
            )}
          </Card>
        )
      })}
    </div>
  )
}

function LessonRow({ lesson }: { lesson: LessonCard }) {
  // "Đã biết" và "đã thạo" là hai chuyện khác nhau và phải hiện khác nhau.
  //
  // Thi vượt chỉ đánh dấu khỏi phải học bài này, KHÔNG mở khoá bài sau. Gộp hai thứ vào một
  // dấu tích xanh thì học viên đọc "đã thạo" ở đây và "bạn mới thi vượt chứ chưa học" ở bài
  // ngay dưới — hai câu chọi nhau, và người ta kết luận hệ thống đếm sai chứ không hiểu là
  // mình cần quay lại học bài này thật.
  const markedKnown = lesson.unlockedByChallenge
  const mastered = lesson.state === 'Mastered' && !markedKnown
  const locked = lesson.state === 'Locked'
  const previewable = lesson.state === 'Previewable'

  return (
    <li className="rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
            <StateIcon state={lesson.state} markedKnown={markedKnown} />
            <span>
              {lesson.code} · {lesson.titleVi}
            </span>

            {lesson.isCheckpoint && (
              <Badge tone="brand">
                <Flag className="size-3" aria-hidden />
                Chốt chặng
              </Badge>
            )}

            {markedKnown && (
              <Badge tone="warning">
                <Rocket className="size-3" aria-hidden />
                Đã đánh dấu biết
              </Badge>
            )}
          </p>

          <p className="mt-1 text-xs text-muted">
            {lesson.level} · {lesson.estimatedMinutes} phút
            {mastered && ` · ${Math.round(lesson.mastery)} điểm`}
          </p>

          {/* Nói thẳng cái đường tắt này không làm được gì, ngay trên thẻ bài. Để học viên tự
              suy ra từ việc bài kế tiếp vẫn khoá là bắt họ đoán. */}
          {markedKnown && (
            <p className="mt-1.5 text-sm text-secondary">
              Bạn đã đánh dấu khỏi học bài này. Bài tiếp theo vẫn cần bạn học thật rồi mới mở.
            </p>
          )}

          {/* Con số cụ thể còn thiếu, không phải câu "chưa đủ điều kiện". */}
          {lesson.lockExplanationVi && (
            <p className="mt-1.5 text-sm text-secondary">{lesson.lockExplanationVi}</p>
          )}
        </div>

        <div className="flex shrink-0 flex-wrap gap-2">
          {/* Bài đã đánh dấu biết VẪN phải còn nút học.
              Ẩn nút ở đây là dựng ngõ cụt: hệ thống bảo "cần học bài đó rồi mới mở bài sau"
              trong khi không còn chỗ nào bấm vào để học. */}
          {!mastered && (
            <Link to={`/learn/lesson/${lesson.code}`}>
              <Button size="sm" variant={locked ? 'secondary' : 'primary'}>
                {previewable ? 'Xem trước' : locked ? 'Xem lý do' : markedKnown ? 'Học để mở bài sau' : 'Học'}
              </Button>
            </Link>
          )}

          {/* Thi vượt chỉ có nghĩa với bài chưa qua. Qua rồi thì nút này chỉ gây nhiễu. */}
          {!mastered && !markedKnown && (
            <Link to={`/learn/lesson/${lesson.code}/challenge`}>
              <Button size="sm" variant="ghost" aria-label={`Thi vượt ${lesson.code}`}>
                <Rocket className="size-4" aria-hidden />
                Thi vượt
              </Button>
            </Link>
          )}
        </div>
      </div>
    </li>
  )
}

function StateIcon({ state, markedKnown }: { state: string; markedKnown?: boolean }) {
  // Dấu tích xanh dành riêng cho bài học thật. Bài mới đánh dấu biết dùng biểu tượng khác,
  // để lướt mắt xuống danh sách là phân biệt được ngay hai loại.
  if (markedKnown) {
    return <Rocket className="size-4 shrink-0 text-[var(--color-warning)]" aria-label="Đã đánh dấu biết" />
  }

  if (state === 'Mastered') {
    return <Check className="size-4 shrink-0 text-[var(--color-success)]" aria-label="Đã thạo" />
  }

  if (state === 'Locked') {
    return <Lock className="size-4 shrink-0 text-muted" aria-label="Đang khoá" />
  }

  if (state === 'Previewable') {
    return <Eye className="size-4 shrink-0 text-[var(--color-warning)]" aria-label="Xem trước được" />
  }

  return <ChevronRight className="size-4 shrink-0 text-brand-600" aria-label="Đang mở" />
}
