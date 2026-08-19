import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  Flame,
  Clock,
  Target,
  RotateCcw,
  ArrowRight,
  Trophy,
  CalendarCheck,
  Snowflake,
} from 'lucide-react'
import { api } from '@/lib/api-client'
import type { DashboardData } from './dashboard-types'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge, EmptyState, ProgressBar, SkeletonCard } from '@/components/ui/feedback'
import { SKILL_ORDER, SKILL_META } from '@/components/skill-badge'
import { HeroIllustration } from '@/components/illustrations/track-illustrations'
import { LessonIllustration } from '@/components/illustrations/scene-illustrations'

const LAYER_LABEL: Record<DashboardData['currentLayer'], string> = {
  Life: 'Đời sống',
  Office: 'Văn phòng',
  Professional: 'Chuyên môn',
}

const MODE_LABEL: Record<DashboardData['studyMode'], string> = {
  Mixed: 'Đủ 4 kỹ năng',
  ListeningOnly: 'Chỉ Nghe',
  SpeakingOnly: 'Chỉ Nói',
  ReadingOnly: 'Chỉ Đọc',
  WritingOnly: 'Chỉ Viết',
}

export function DashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['learning', 'dashboard'],
    queryFn: () => api.get<DashboardData>('/api/v1/learning/dashboard'),
  })

  if (isLoading) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <Card>
        <EmptyState
          title="Chưa tải được tiến độ"
          description="Máy chủ không phản hồi. Thử tải lại trang; nếu vẫn lỗi, báo mã ở góc màn hình cho quản trị."
          action={
            <Button onClick={() => window.location.reload()}>
              <RotateCcw className="size-4" aria-hidden />
              Tải lại
            </Button>
          }
        />
      </Card>
    )
  }

  // Chưa xếp lớp thì mọi con số đều vô nghĩa. Đưa thẳng người học sang việc cần làm —
  // nhưng KHÔNG chặn cứng: người mất gốc bắt đầu ở bài đầu tiên dù có thi hay không,
  // nên bắt họ thi trước chỉ tạo thêm một rào cản không cần thiết.
  if (!data.placementCompleted) {
    return <PlacementPrompt name={data.displayName} nextLesson={data.nextLesson} />
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Chào {data.displayName}</h1>
          <p className="mt-0.5 text-sm text-secondary">
            Bậc {data.currentLevel} · tầng {LAYER_LABEL[data.currentLayer]} · {MODE_LABEL[data.studyMode]}
          </p>
        </div>
        <Link to="/learn/settings">
          <Button variant="secondary" size="sm">
            Đổi chế độ học
          </Button>
        </Link>
      </header>

      <NextLessonCard data={data} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          icon={<Flame className="size-5" />}
          label="Chuỗi ngày"
          value={`${data.streak.current}`}
          suffix="ngày"
          hint={
            data.streak.studiedToday
              ? 'Hôm nay đã học rồi'
              : data.streak.freezeTokens > 0
                ? `Còn ${data.streak.freezeTokens} lượt nghỉ`
                : 'Chưa học hôm nay'
          }
          tone={data.streak.studiedToday ? 'success' : 'warning'}
        />
        <StatTile
          icon={<Target className="size-5" />}
          label="Bài đã thạo"
          value={`${data.progress.lessonsMastered}`}
          suffix={`/ ${data.progress.lessonsTotal}`}
          hint={`${data.progress.lessonsInProgress} bài đang học`}
        />
        <StatTile
          icon={<Clock className="size-5" />}
          label="7 ngày qua"
          value={`${data.progress.minutesStudiedLast7Days}`}
          suffix="phút"
          hint={
            data.progress.estimatedDaysRemaining !== null
              ? `Còn khoảng ${data.progress.estimatedDaysRemaining} ngày`
              : 'Học thêm vài buổi để ước lượng'
          }
        />
        {/* Ô này bấm được: thấy con số mà không đi tới đâu thì học viên phải tự
            tìm mục Ôn tập trên thanh bên, và phần lớn sẽ không tìm. */}
        <Link to="/learn/review" className="rounded-[var(--radius-card)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-500">
          <StatTile
            icon={<RotateCcw className="size-5" />}
            label="Cần ôn lại"
            value={`${data.reviewDueCount}`}
            suffix="câu"
            hint={data.reviewDueCount > 0 ? 'Ôn trước khi học bài mới' : 'Không nợ ôn tập'}
            tone={data.reviewDueCount > 12 ? 'warning' : undefined}
          />
        </Link>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <SkillPanel scores={data.skillScores} mode={data.studyMode} />
        <MilestonePanel milestones={data.milestones} />
      </div>
    </div>
  )
}

function NextLessonCard({ data }: { data: DashboardData }) {
  if (!data.nextLesson) {
    return (
      <Card>
        <EmptyState
          illustration={<HeroIllustration size={220} className="text-brand-500" />}
          title="Hết bài trong bậc hiện tại"
          description="Bạn đã học xong mọi bài đang mở. Làm bài kiểm tra cuối bậc để lên bậc tiếp theo."
          action={
            <Link to="/learn/roadmap">
              <Button>Xem lộ trình</Button>
            </Link>
          }
        />
      </Card>
    )
  }

  const lesson = data.nextLesson

  return (
    <Card emphasis className="pulse-ring overflow-hidden">
      <div className="flex flex-col gap-5 p-5 sm:flex-row sm:items-center">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="brand">Học tiếp</Badge>
            <Badge>{lesson.code}</Badge>
            <Badge>
              <Clock className="size-3" aria-hidden />
              {lesson.estimatedMinutes} phút
            </Badge>
          </div>

          <h2 className="mt-2.5 text-lg font-semibold">{lesson.titleVi}</h2>

          {/* Câu giải thích do server sinh: nói rõ vì sao đúng bài này, không phải bài khác. */}
          <p className="mt-1.5 text-sm text-secondary">{lesson.reasonVi}</p>

          <div className="mt-3 flex flex-wrap gap-1.5">
            {SKILL_ORDER.filter((skill) => lesson.supportedSkills.includes(skill)).map((skill) => {
              const Icon = SKILL_META[skill].icon
              return (
                <span
                  key={skill}
                  className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
                  style={{
                    backgroundColor: `color-mix(in oklch, ${SKILL_META[skill].colorVar} 14%, transparent)`,
                    color: SKILL_META[skill].colorVar,
                  }}
                >
                  <Icon className="size-3" aria-hidden />
                  {SKILL_META[skill].labelVi}
                </span>
              )
            })}
          </div>
        </div>

        <div className="flex shrink-0 flex-col items-center gap-3">
          {/* Hình gợi bối cảnh của bài kế — nhìn là hình dung ra tình huống
              trước cả khi đọc tiêu đề. */}
          <LessonIllustration name={lesson.illustration} size={220} className="text-brand-500" />

          <Link to={`/learn/lesson/${lesson.code}`} className="w-full">
            <Button size="lg" className="w-full">
              Bắt đầu
              <ArrowRight className="size-4" aria-hidden />
            </Button>
          </Link>
        </div>
      </div>
    </Card>
  )
}

function StatTile({
  icon,
  label,
  value,
  suffix,
  hint,
  tone,
}: {
  icon: React.ReactNode
  label: string
  value: string
  suffix?: string
  hint?: string
  tone?: 'success' | 'warning'
}) {
  const toneColor =
    tone === 'success' ? 'var(--color-success)' : tone === 'warning' ? 'var(--color-warning)' : undefined

  return (
    <Card className="p-4">
      <div className="flex items-center gap-2 text-secondary" style={toneColor ? { color: toneColor } : undefined}>
        {icon}
        <span className="text-xs font-medium uppercase tracking-wide">{label}</span>
      </div>
      <div className="mt-2 flex items-baseline gap-1">
        <span className="text-2xl font-semibold tabular-nums">{value}</span>
        {suffix && <span className="text-sm text-muted">{suffix}</span>}
      </div>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </Card>
  )
}

function SkillPanel({
  scores,
  mode,
}: {
  scores: DashboardData['skillScores']
  mode: DashboardData['studyMode']
}) {
  const focusedSkill =
    mode === 'ListeningOnly'
      ? 'Listening'
      : mode === 'SpeakingOnly'
        ? 'Speaking'
        : mode === 'ReadingOnly'
          ? 'Reading'
          : mode === 'WritingOnly'
            ? 'Writing'
            : null

  return (
    <Card>
      <CardHeader
        title="Bốn kỹ năng"
        description="Ngưỡng xét riêng từng trục — điểm tổng cao không che được trục yếu."
      />
      <CardBody className="space-y-4">
        {SKILL_ORDER.map((skill) => {
          const Icon = SKILL_META[skill].icon
          const dimmed = focusedSkill !== null && focusedSkill !== skill

          return (
            <div key={skill} className={dimmed ? 'opacity-45' : undefined}>
              <div className="mb-1.5 flex items-center justify-between text-sm">
                <span className="flex items-center gap-1.5" style={{ color: SKILL_META[skill].colorVar }}>
                  <Icon className="size-4" aria-hidden />
                  <span className="font-medium">{SKILL_META[skill].labelVi}</span>
                </span>
                <span className="tabular-nums font-semibold">{Math.round(scores[skill])}</span>
              </div>
              <div className="h-2 w-full overflow-hidden rounded-full bg-[var(--surface-sunken)]">
                <div
                  className="h-full rounded-full transition-[width] duration-700 ease-out"
                  style={{
                    width: `${Math.min(100, Math.max(0, scores[skill]))}%`,
                    backgroundColor: SKILL_META[skill].colorVar,
                  }}
                />
              </div>
            </div>
          )
        })}

        {focusedSkill && (
          <p className="rounded-[var(--radius-control)] bg-[color-mix(in_oklch,var(--color-warning)_12%,transparent)] p-3 text-xs text-secondary">
            <strong className="font-semibold text-[var(--text-primary)]">
              Bạn đang học chế độ một kỹ năng.
            </strong>{' '}
            Ba trục còn lại sẽ đứng yên. Hai hệ quả: bài kiểm tra cuối bậc đòi cả bốn trục đạt ngưỡng
            nên chế độ này <em>không đủ để lên bậc</em>, và một ngày chỉ tính vào chuỗi khi chạm đủ
            bốn kỹ năng — nên <em>chuỗi ngày sẽ không tăng</em>.
          </p>
        )}
      </CardBody>
    </Card>
  )
}

function MilestonePanel({ milestones }: { milestones: DashboardData['milestones'] }) {
  return (
    <Card>
      <CardHeader
        title="Mốc nghề nghiệp"
        description="Thứ bạn nói được với sếp, không phải số bài đã học."
        icon={<Trophy className="size-5 text-brand-600" aria-hidden />}
      />
      <CardBody className="space-y-3">
        {milestones.map((milestone) => (
          <div key={milestone.key} className="flex items-start gap-3">
            <div
              className="mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full"
              style={{
                backgroundColor: milestone.achieved
                  ? 'color-mix(in oklch, var(--color-success) 20%, transparent)'
                  : 'var(--surface-sunken)',
                color: milestone.achieved ? 'var(--color-success)' : 'var(--text-muted)',
              }}
            >
              {milestone.achieved ? (
                <CalendarCheck className="size-3.5" aria-hidden />
              ) : (
                <Snowflake className="size-3" aria-hidden />
              )}
            </div>

            <div className="min-w-0 flex-1">
              <div className="flex items-baseline justify-between gap-2">
                <span className={milestone.achieved ? 'text-sm font-medium' : 'text-sm'}>
                  {milestone.labelVi}
                </span>
                <span className="shrink-0 text-xs tabular-nums text-muted">
                  {Math.round(milestone.progressPercent)}%
                </span>
              </div>
              <p className="mt-0.5 text-xs text-muted">{milestone.requirementVi}</p>
              <ProgressBar value={milestone.progressPercent} className="mt-1.5" />
            </div>
          </div>
        ))}
      </CardBody>
    </Card>
  )
}

function PlacementPrompt({
  name,
  nextLesson,
}: {
  name: string
  nextLesson: DashboardData['nextLesson']
}) {
  return (
    <div className="space-y-4">
      <Card>
        <EmptyState
          illustration={<HeroIllustration size={260} className="text-brand-500" />}
          title={`${name}, bắt đầu bằng bài xếp lớp`}
          description="26 câu, khoảng 18 phút. Đo cả bốn kỹ năng để đặt bạn vào đúng bậc — làm một lần, tiết kiệm hàng tuần học sai chỗ."
          action={
            <Link to="/placement">
              <Button size="lg">
                Làm bài xếp lớp
                <ArrowRight className="size-4" aria-hidden />
              </Button>
            </Link>
          }
        />
      </Card>

      {/* Lối đi thứ hai cho người mất gốc: kết quả xếp lớp của họ gần như chắc chắn
          là bài đầu tiên, nên thi trước chỉ tốn 18 phút mà không đổi lộ trình. */}
      {nextLesson && (
        <Card className="p-5">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0">
              <h2 className="font-semibold">Hoặc bắt đầu luôn từ số 0</h2>
              <p className="mt-1 text-sm text-secondary">
                Nếu bạn mất gốc hẳn thì không cần thi: lộ trình sẽ bắt đầu ở{' '}
                <span className="font-medium text-[var(--text-primary)]">{nextLesson.titleVi}</span>{' '}
                ({nextLesson.code}, {nextLesson.estimatedMinutes} phút). Thi xếp lớp lúc nào cũng được.
              </p>
            </div>

            <Link to={`/learn/lesson/${nextLesson.code}`} className="shrink-0">
              <Button variant="secondary" className="w-full sm:w-auto">
                Học bài đầu tiên
                <ArrowRight className="size-4" aria-hidden />
              </Button>
            </Link>
          </div>
        </Card>
      )}
    </div>
  )
}
