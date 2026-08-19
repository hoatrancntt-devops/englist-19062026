import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Headphones, Mic, BookOpen, PenLine, Check, Lock, ChevronRight } from 'lucide-react'
import { api } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import { LessonIllustration } from '@/components/illustrations/scene-illustrations'

type Skill = 'Listening' | 'Speaking' | 'Reading' | 'Writing'

interface LessonCard {
  code: string
  titleVi: string
  level: string
  estimatedMinutes: number
  state: string
  supportedSkills: string[]
  lockExplanationVi: string
  unlockedByChallenge: boolean
  illustration: string | null
}

interface RoadmapResult {
  lessons: LessonCard[]
}

interface Dashboard {
  skillScores: Record<string, number>
  studyMode: string
}

const SKILLS: Record<
  Skill,
  { label: string; icon: typeof Headphones; mode: string; hint: string; scene: string }
> = {
  Listening: {
    label: 'Nghe',
    icon: Headphones,
    mode: 'ListeningOnly',
    hint: 'Hội thoại nghề theo tốc độ tăng dần, có phụ đề Anh và Việt bật tắt được.',
    scene: 'phone-call',
  },
  Speaking: {
    label: 'Nói',
    icon: Mic,
    mode: 'SpeakingOnly',
    hint: 'Đọc theo mẫu và trả lời tình huống. Giọng chấm ngay tại máy chủ, không gửi đi đâu.',
    scene: 'coffee-chat',
  },
  Reading: {
    label: 'Đọc',
    icon: BookOpen,
    mode: 'ReadingOnly',
    hint: 'Email, ticket, log, hoá đơn, hợp đồng — văn bản thật của nghề và của đời sống.',
    scene: 'email-inbox',
  },
  Writing: {
    label: 'Viết',
    icon: PenLine,
    mode: 'WritingOnly',
    hint: 'Điền chỗ trống, sắp câu, viết email có hướng dẫn. Chấm bằng luật tại máy chủ.',
    scene: 'chat-message',
  },
}

/**
 * Trang cho một kỹ năng.
 *
 * Trước đây bốn trang này chỉ liệt kê "còn thiếu" — và danh sách đó đã lỗi thời từ lâu:
 * phần chấm viết, nội dung đọc và bộ nhận dạng giọng đều đã chạy. Người học bấm vào và
 * tưởng cả mảng chưa có gì, trong khi bài nằm ngay trong lộ trình.
 *
 * Giờ trang này trả lời đúng ba câu người học cần: mình đang ở đâu, học chỗ nào,
 * và làm sao để chỉ tập trung vào kỹ năng này.
 */
export function SkillPage({ skill }: { skill: Skill }) {
  const queryClient = useQueryClient()
  const meta = SKILLS[skill]

  const roadmap = useQuery({
    queryKey: ['roadmap'],
    queryFn: () => api.get<RoadmapResult>('/api/v1/learning/roadmap'),
  })

  const dashboard = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => api.get<Dashboard>('/api/v1/learning/dashboard'),
  })

  const setMode = useMutation({
    mutationFn: (studyMode: string) =>
      api.put('/api/v1/learning/preferences', { primaryTrack: 'All', studyMode }),
    onSuccess: () => queryClient.invalidateQueries(),
  })

  if (roadmap.isLoading || !roadmap.data || !dashboard.data) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    )
  }

  const lessons = roadmap.data.lessons.filter((l) => l.supportedSkills.includes(skill))
  const score = Math.round(dashboard.data.skillScores?.[skill] ?? 0)
  const onlyThisSkill = dashboard.data.studyMode === meta.mode

  // Bài mở được đứng trước. Danh sách dài mà toàn bài khoá ở đầu thì người học
  // phải cuộn qua hàng chục dòng mới thấy chỗ bắt đầu.
  const open = lessons.filter((l) => l.state !== 'Locked')
  const locked = lessons.filter((l) => l.state === 'Locked')

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title={`Luyện ${meta.label}`}
          description={meta.hint}
          icon={<meta.icon className="size-5 text-brand-600" aria-hidden />}
        />
        <CardBody className="space-y-4">
          <div className="flex justify-center">
            <LessonIllustration name={meta.scene} size={240} className="text-brand-500" />
          </div>

          <ProgressBar value={score} max={100} label={`Điểm ${meta.label} hiện tại`} />

          <p className="text-sm text-secondary">
            {lessons.length} bài có phần {meta.label.toLowerCase()}, trong đó{' '}
            <strong>{open.length} bài đang mở</strong>.
          </p>

          {/* Đổi chế độ ngay tại đây thay vì bắt người học đi tìm trong Cài đặt. */}
          {onlyThisSkill ? (
            <p className="text-sm text-secondary">
              Đang ở chế độ chỉ {meta.label.toLowerCase()} — bài chỉ hiện bước của kỹ năng này.{' '}
              <button
                type="button"
                className="text-brand-600 underline dark:text-brand-300"
                onClick={() => setMode.mutate('Mixed')}
              >
                Quay lại đủ bốn kỹ năng
              </button>
            </p>
          ) : (
            <Button
              variant="secondary"
              onClick={() => setMode.mutate(meta.mode)}
              loading={setMode.isPending}
            >
              Chỉ học {meta.label.toLowerCase()} trong mọi bài
            </Button>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader
          title="Bài đang mở"
          description={`Bấm vào để học. Mỗi bài đều có bước ${meta.label.toLowerCase()}.`}
        />
        <CardBody>
          {open.length === 0 ? (
            <p className="text-sm text-secondary">
              Chưa có bài nào mở. Bắt đầu từ <Link to="/learn/roadmap" className="text-brand-600 underline dark:text-brand-300">Lộ trình</Link>.
            </p>
          ) : (
            <ul className="space-y-2">
              {open.map((lesson) => (
                <LessonRow key={lesson.code} lesson={lesson} />
              ))}
            </ul>
          )}
        </CardBody>
      </Card>

      {locked.length > 0 && (
        <Card>
          <CardHeader
            title={`Còn khoá (${locked.length} bài)`}
            description="Mỗi bài đều nói rõ còn thiếu bao nhiêu điểm ở bài nào."
          />
          <CardBody>
            <ul className="space-y-2">
              {locked.slice(0, 5).map((lesson) => (
                <LessonRow key={lesson.code} lesson={lesson} />
              ))}
            </ul>

            {locked.length > 5 && (
              <p className="mt-3 text-sm text-secondary">
                Còn {locked.length - 5} bài nữa —{' '}
                <Link to="/learn/roadmap" className="text-brand-600 underline dark:text-brand-300">
                  xem toàn bộ lộ trình
                </Link>
                .
              </p>
            )}
          </CardBody>
        </Card>
      )}
    </div>
  )
}

function LessonRow({ lesson }: { lesson: LessonCard }) {
  const locked = lesson.state === 'Locked'
  const mastered = lesson.state === 'Mastered' && !lesson.unlockedByChallenge

  return (
    <li className="rounded-[var(--radius-card)] border border-[var(--border-subtle)] p-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 gap-3">
          <LessonIllustration
            name={lesson.illustration}
            size={76}
            variant="bare"
            className={`mt-0.5 hidden shrink-0 sm:block ${locked ? 'text-muted opacity-60' : 'text-brand-500'}`}
          />

          <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
            {mastered && <Check className="size-4 shrink-0 text-[var(--color-success)]" aria-label="Đã thạo" />}
            {locked && <Lock className="size-4 shrink-0 text-muted" aria-label="Đang khoá" />}
            <span>
              {lesson.code} · {lesson.titleVi}
            </span>
            {lesson.unlockedByChallenge && <Badge tone="warning">Đã đánh dấu biết</Badge>}
          </p>

          <p className="mt-1 text-xs text-muted">
            {lesson.level} · {lesson.estimatedMinutes} phút
          </p>

          {lesson.lockExplanationVi && (
            <p className="mt-1.5 text-sm text-secondary">{lesson.lockExplanationVi}</p>
          )}
          </div>
        </div>

        <Link to={`/learn/lesson/${lesson.code}`} className="shrink-0">
          <Button size="sm" variant={locked ? 'secondary' : 'primary'}>
            {locked ? 'Xem lý do' : 'Học'}
            {!locked && <ChevronRight className="size-4" aria-hidden />}
          </Button>
        </Link>
      </div>
    </li>
  )
}
