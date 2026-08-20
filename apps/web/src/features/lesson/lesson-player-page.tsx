import { useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ArrowRight, Check, Clock, Lock, Lightbulb, Rocket, TriangleAlert } from 'lucide-react'
import { api, ApiError } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, SkeletonCard } from '@/components/ui/feedback'
import { SKILL_META } from '@/components/skill-badge'
import { ACTIVITY_LABEL } from './lesson-types'
import type { ActivityGrade, LessonSubmissionResult, PlayerLesson } from './lesson-types'
import { LessonIllustration } from '@/components/illustrations/scene-illustrations'
import { LessonCountdown, useLessonCountdown } from './lesson-timer'
import { QuizStep } from './steps/quiz-step'
import { ListeningIntro, ReadingIntro, SpeakingStep, VocabStep, WritingStep } from './steps/content-steps'

export function LessonPlayerPage() {
  const { code = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [stepIndex, setStepIndex] = useState(0)
  const [grades, setGrades] = useState<Record<string, ActivityGrade>>({})
  const [result, setResult] = useState<LessonSubmissionResult | null>(null)

  // Đo thời gian thật của từng bước, không ước lượng: dự báo ngày hoàn thành
  // trên bảng điều khiển tính từ chính con số này.
  const stepStartedAt = useRef(Date.now())

  const { data: lesson, isLoading } = useQuery({
    queryKey: ['lesson', code],
    queryFn: () => api.get<PlayerLesson>(`/api/v1/learning/lessons/${code}`),
  })

  /**
   * Hết giờ: bỏ mọi thứ đã làm ở phía client và tải lại bài từ máy chủ.
   *
   * Máy chủ đã xoá bản ghi các bước của lượt này rồi, nên giữ lại điểm trên màn hình chỉ làm
   * học viên tưởng mình vẫn còn tiến độ.
   */
  const [expired, setExpired] = useState(false)

  /** Câu giải thích của máy chủ khi bước sau bị khoá vì bước trước chưa đạt. */
  const [stepBlocked, setStepBlocked] = useState<string | null>(null)

  const restartAfterExpiry = () => {
    setExpired(true)
    setStepBlocked(null)
    setStepIndex(0)
    setGrades({})
    setResult(null)
    stepStartedAt.current = Date.now()

    void queryClient.invalidateQueries({ queryKey: ['lesson', code] })
  }

  const submitActivity = useMutation({
    mutationFn: (body: {
      activityId: string
      responses: { itemCode: string; chosenIndex: number }[]
      textAnswers?: string[]
    }) =>
      api.post<ActivityGrade>(`/api/v1/learning/lessons/${code}/activities`, {
        ...body,
        durationSeconds: Math.round((Date.now() - stepStartedAt.current) / 1000),
      }),
    onSuccess: (grade, variables) => {
      // Đồng hồ phía máy chủ chỉ bắt đầu chạy khi lượt được mở, tức là ở bước nộp đầu tiên.
      // Trước đó màn hình đếm từ trọn 30 phút, nên đọc bài mười phút rồi mới làm sẽ khiến
      // hai đồng hồ lệch nhau đúng mười phút. Lấy lại con số thật ngay sau bước đầu.
      const firstStep = Object.keys(grades).length === 0

      setStepBlocked(null)
      setGrades((prev) => ({ ...prev, [variables.activityId]: grade }))

      if (firstStep) {
        void queryClient.invalidateQueries({ queryKey: ['lesson', code] })
      }
    },

    // Hai loại 409 khác hẳn nhau và PHẢI phân biệt bằng mã lỗi, không phải bằng mã trạng thái:
    // hết giờ thì xoá sạch bài làm, còn bước bị khoá thì tuyệt đối không được xoá gì.
    onError: (error) => {
      if (!(error instanceof ApiError) || error.status !== 409) {
        return
      }

      if (error.code === 'lesson_expired') {
        restartAfterExpiry()
        return
      }

      if (error.code === 'step_locked') {
        setStepBlocked(error.message)
      }
    },
  })

  const submitLesson = useMutation({
    mutationFn: () => api.post<LessonSubmissionResult>(`/api/v1/learning/lessons/${code}/submit`),
    onSuccess: (data) => {
      setResult(data)
      // Bảng điều khiển và lộ trình đổi theo kết quả bài này nên phải nạp lại.
      void queryClient.invalidateQueries({ queryKey: ['learning'] })
    },
  })

  const secondsLeft = useLessonCountdown(lesson?.secondsRemaining, restartAfterExpiry)

  const activity = lesson?.activities[stepIndex]
  const grade = activity ? (grades[activity.id] ?? null) : null

  const allStepsDone = useMemo(
    () => lesson?.activities.every((a) => grades[a.id] !== undefined) ?? false,
    [lesson, grades],
  )

  if (isLoading) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    )
  }

  if (!lesson) {
    return (
      <Card className="p-6 text-center">
        <p className="text-secondary">Không tìm thấy bài học này.</p>
        <Link to="/learn" className="mt-4 inline-block">
          <Button variant="secondary">Về bảng điều khiển</Button>
        </Link>
      </Card>
    )
  }

  if (lesson.state === 'Locked') {
    return <LockedLesson lesson={lesson} />
  }

  if (result) {
    return <LessonResult lesson={lesson} result={result} onBack={() => navigate('/learn')} />
  }

  const goTo = (index: number) => {
    stepStartedAt.current = Date.now()
    setStepIndex(index)
  }

  return (
    <div className="space-y-4">
      <LessonHeader lesson={lesson} secondsLeft={secondsLeft} />

      {/* Chỉ hiện sau khi vừa bị đặt lại. Không có dòng này thì học viên quay lại thấy màn
          hình trắng trơn ở bước một và tưởng hệ thống nuốt mất bài làm của mình. */}
      {expired ? (
        <div className="flex items-start gap-2 rounded-[var(--radius-control)] bg-[color-mix(in_oklch,var(--color-warning)_18%,transparent)] p-3 text-sm text-[var(--color-warning-text)]">
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          <p>
            Hết {lesson.timeLimitMinutes} phút cho một lượt làm bài. Các bước đã làm được đặt lại,
            bạn bắt đầu lại từ bước một với đồng hồ mới.
          </p>
        </div>
      ) : null}

      {/* Bước bị khoá KHÔNG xoá gì cả — chỉ nói cần quay lại bước nào. */}
      {stepBlocked ? (
        <div className="flex items-start gap-2 rounded-[var(--radius-control)] bg-[color-mix(in_oklch,var(--color-warning)_18%,transparent)] p-3 text-sm text-[var(--color-warning-text)]">
          <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
          <p>{stepBlocked}</p>
        </div>
      ) : null}

      <StepRail
        lesson={lesson}
        current={stepIndex}
        grades={grades}
        onSelect={goTo}
      />

      {activity && (
        <Card>
          <CardHeader
            title={ACTIVITY_LABEL[activity.kind]}
            description={`Đạt từ ${activity.passScore} điểm`}
            icon={<StepIcon skill={activity.skill} />}
            action={<Badge>{`Bước ${stepIndex + 1}/${lesson.activities.length}`}</Badge>}
          />

          <CardBody>
            {activity.kind === 'Listen' && <ListeningIntro payload={activity.payload} />}
            {activity.kind === 'Read' && <ReadingIntro payload={activity.payload} />}

            {activity.kind === 'Vocab' ? (
              <VocabStep
                activity={activity}
                submitting={submitActivity.isPending}
                onDone={() => submitActivity.mutate({ activityId: activity.id, responses: [] })}
              />
            ) : activity.kind === 'Shadow' || activity.kind === 'Speak' ? (
              <SpeakingStep
                activity={activity}
                grade={grade}
                submitting={submitActivity.isPending}
                onDone={() => submitActivity.mutate({ activityId: activity.id, responses: [] })}
              />
            ) : activity.kind === 'Write' ? (
              <WritingStep
                activity={activity}
                grade={grade}
                submitting={submitActivity.isPending}
                onSubmit={(textAnswers) =>
                  submitActivity.mutate({ activityId: activity.id, responses: [], textAnswers })
                }
              />
            ) : (
              <QuizStep
                activity={activity}
                grade={grade}
                submitting={submitActivity.isPending}
                onSubmit={(responses) => submitActivity.mutate({ activityId: activity.id, responses })}
              />
            )}

            {grade && <GradeFeedback grade={grade} />}
          </CardBody>
        </Card>
      )}

      <div className="flex items-center justify-between gap-3">
        <Button variant="ghost" onClick={() => goTo(stepIndex - 1)} disabled={stepIndex === 0}>
          <ArrowLeft className="size-4" aria-hidden />
          Bước trước
        </Button>

        {stepIndex < lesson.activities.length - 1 ? (
          <Button onClick={() => goTo(stepIndex + 1)} disabled={!grade}>
            Bước tiếp
            <ArrowRight className="size-4" aria-hidden />
          </Button>
        ) : (
          <Button onClick={() => submitLesson.mutate()} disabled={!allStepsDone} loading={submitLesson.isPending}>
            Chốt bài
            <Check className="size-4" aria-hidden />
          </Button>
        )}
      </div>

      <ExplanationPanel lesson={lesson} />
    </div>
  )
}

function LessonHeader({
  lesson,
  secondsLeft,
}: {
  lesson: PlayerLesson
  secondsLeft: number
}) {
  return (
    <header>
      <Link to="/learn" className="mb-2 inline-flex items-center gap-1.5 text-sm text-secondary hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        Bảng điều khiển
      </Link>

      {/* Hình đặt cạnh tiêu đề chứ không đè lên: nó là gợi ý bối cảnh,
          không phải ảnh bìa. Trên màn nhỏ thì hình xuống dưới. */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-xl font-semibold">{lesson.titleVi}</h1>

          <div className="mt-1.5 flex flex-wrap items-center gap-2 text-sm">
            <Badge>{lesson.code}</Badge>
            <Badge>bậc {lesson.level}</Badge>
            <Badge>
              <Clock className="size-3" aria-hidden />
              {lesson.estimatedMinutes} phút
            </Badge>

            <LessonCountdown secondsLeft={secondsLeft} limitMinutes={lesson.timeLimitMinutes} />
          </div>

          <p className="mt-2 text-sm text-secondary">{lesson.objectiveVi}</p>
        </div>

        <div className="shrink-0 self-center">
          <LessonIllustration name={lesson.illustration} size={260} className="text-brand-500" />
        </div>
      </div>
    </header>
  )
}

/** Thanh các bước. Cho phép quay lại bước đã xong, không cho nhảy tới bước chưa tới. */
function StepRail({
  lesson,
  current,
  grades,
  onSelect,
}: {
  lesson: PlayerLesson
  current: number
  grades: Record<string, ActivityGrade>
  onSelect: (index: number) => void
}) {
  return (
    <ol className="flex flex-wrap gap-1.5">
      {lesson.activities.map((a, index) => {
        const done = grades[a.id] !== undefined
        const reachable = done || index <= current

        return (
          <li key={a.id}>
            <button
              type="button"
              onClick={() => reachable && onSelect(index)}
              disabled={!reachable}
              aria-current={index === current ? 'step' : undefined}
              className={cn(
                'flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition-colors',
                index === current && 'bg-brand-600 text-white',
                index !== current && done && 'bg-[color-mix(in_oklch,var(--color-success)_15%,transparent)] text-[var(--color-success)]',
                index !== current && !done && 'bg-[var(--surface-sunken)] text-[var(--text-secondary)]',
                !reachable && 'cursor-not-allowed opacity-50',
              )}
            >
              {done && index !== current && <Check className="size-3" aria-hidden />}
              {ACTIVITY_LABEL[a.kind]}
            </button>
          </li>
        )
      })}
    </ol>
  )
}

function StepIcon({ skill }: { skill: keyof typeof SKILL_META }) {
  const Icon = SKILL_META[skill].icon
  return <Icon className="size-5" style={{ color: SKILL_META[skill].colorVar }} aria-hidden />
}

function GradeFeedback({ grade }: { grade: ActivityGrade }) {
  const tone = !grade.graded
    ? 'var(--color-warning)'
    : grade.passed
      ? 'var(--color-success)'
      : 'var(--color-danger)'

  return (
    <p
      role="status"
      className="mt-4 rounded-[var(--radius-control)] px-3 py-2 text-sm"
      style={{
        backgroundColor: `color-mix(in oklch, ${tone} 12%, transparent)`,
        color: 'var(--text-primary)',
      }}
    >
      {grade.graded && <strong className="font-semibold">{Math.round(grade.score)} điểm. </strong>}
      {grade.feedbackVi}
    </p>
  )
}

/** Giải thích tiếng Việt và lỗi thường gặp. Luôn hiện, không ẩn sau nút. */
function ExplanationPanel({ lesson }: { lesson: PlayerLesson }) {
  return (
    <Card>
      <CardHeader
        title="Vì sao chỗ này khó"
        icon={<Lightbulb className="size-5 text-brand-600" aria-hidden />}
      />
      <CardBody className="space-y-4 text-sm">
        <p className="text-secondary">{lesson.explanation.WhyVi}</p>
        <p className="text-secondary">{lesson.explanation.HowVi}</p>

        {lesson.explanation.ContrastVi && (
          <p className="rounded-[var(--radius-control)] bg-[var(--surface-sunken)] p-3 text-secondary">
            {lesson.explanation.ContrastVi}
          </p>
        )}

        {lesson.commonMistakes.length > 0 && (
          <div>
            <h3 className="mb-2 flex items-center gap-2 font-semibold">
              <TriangleAlert className="size-4 text-[var(--color-warning)]" aria-hidden />
              Lỗi thường gặp
            </h3>
            <ul className="space-y-3">
              {lesson.commonMistakes.map((mistake) => (
                <li key={mistake.Mistake} className="border-l-2 border-[var(--color-warning)] pl-3">
                  <p className="font-medium">{mistake.Mistake}</p>
                  <p className="mt-0.5 text-secondary">{mistake.WhyVi}</p>
                  <p className="mt-0.5 text-secondary">
                    <span className="font-medium text-[var(--text-primary)]">Cách sửa: </span>
                    {mistake.FixVi}
                  </p>
                </li>
              ))}
            </ul>
          </div>
        )}
      </CardBody>
    </Card>
  )
}

function LockedLesson({ lesson }: { lesson: PlayerLesson }) {
  return (
    <Card className="p-6">
      <div className="flex flex-col items-center text-center">
        <Lock className="size-10 text-[var(--text-muted)]" aria-hidden />
        <h1 className="mt-3 font-semibold">{lesson.titleVi} chưa mở</h1>

        {/* Con số cụ thể, không phải câu "chưa đủ điều kiện". */}
        <p className="mt-2 max-w-md text-sm text-secondary">{lesson.lockExplanationVi}</p>

        {/* Lối thoát cho người đã biết sẵn nội dung. Đặt ngay ở màn khoá vì đây đúng là
            chỗ họ đứng khi bực mình vì bị chặn — bắt họ đi tìm nút này ở nơi khác là vô lý. */}
        <p className="mt-4 max-w-md text-sm text-secondary">
          Đã biết phần này rồi? Thi vượt để mở bài mà không phải học lại từ đầu.
        </p>

        <div className="mt-4 flex flex-wrap justify-center gap-3">
          <Link to={`/learn/lesson/${lesson.code}/challenge`}>
            <Button>
              <Rocket className="size-4" aria-hidden />
              Thi vượt bài này
            </Button>
          </Link>

          <Link to="/learn">
            <Button variant="secondary">Về bảng điều khiển</Button>
          </Link>
        </div>
      </div>
    </Card>
  )
}

function LessonResult({
  lesson,
  result,
  onBack,
}: {
  lesson: PlayerLesson
  result: LessonSubmissionResult
  onBack: () => void
}) {
  const mastered = result.state === 'Mastered'

  return (
    <Card className="p-6">
      <div className="text-center">
        <div
          className="mx-auto flex size-14 items-center justify-center rounded-full"
          style={{
            backgroundColor: `color-mix(in oklch, ${mastered ? 'var(--color-success)' : 'var(--color-warning)'} 18%, transparent)`,
            color: mastered ? 'var(--color-success)' : 'var(--color-warning)',
          }}
        >
          {mastered ? <Check className="size-7" aria-hidden /> : <ArrowRight className="size-7" aria-hidden />}
        </div>

        <h1 className="mt-3 text-xl font-semibold">
          {mastered ? 'Đã thạo bài này' : 'Chưa chốt được bài'}
        </h1>
        <p className="mt-1.5 text-3xl font-semibold tabular-nums">{Math.round(result.score)}</p>
        <p className="mx-auto mt-2 max-w-lg text-sm text-secondary">{result.messageVi}</p>
      </div>

      <dl className="mt-6 grid gap-3 sm:grid-cols-2">
        {Object.entries(result.skillScores).map(([skill, score]) => (
          <div key={skill} className="rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2">
            <dt className="text-xs text-muted">{SKILL_META[skill as keyof typeof SKILL_META]?.labelVi ?? skill}</dt>
            <dd className="text-lg font-semibold tabular-nums">{Math.round(score)}</dd>
          </div>
        ))}
      </dl>

      {result.reviewItemsScheduled > 0 && (
        <p className="mt-4 text-center text-sm text-secondary">
          Đã xếp lịch ôn lại {result.reviewItemsScheduled} câu.
        </p>
      )}

      <div className="mt-6 flex justify-center gap-2">
        <Button onClick={onBack}>Về bảng điều khiển</Button>
        {!mastered && (
          <Button variant="secondary" onClick={() => window.location.reload()}>
            Làm lại bài {lesson.code}
          </Button>
        )}
      </div>
    </Card>
  )
}
