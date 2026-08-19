import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Compass, Headphones, Mic, BookOpen, PenLine, Layers, Check } from 'lucide-react'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { SkeletonCard } from '@/components/ui/feedback'

interface TrackOption {
  value: string
  labelVi: string
  hintVi: string
  lessonCount: number
}

interface Preferences {
  primaryTrack: string
  studyMode: string
  onboardingCompleted: boolean
  tracks: TrackOption[]
}

/** Thứ tự khớp thứ tự ưu tiên kỹ năng của cả app: nghe, nói, đọc, viết. */
const MODES = [
  { value: 'Mixed', label: 'Đủ bốn kỹ năng', hint: 'Nghe, nói, đọc, viết. Nên chọn nếu chưa chắc.', icon: Layers },
  { value: 'ListeningOnly', label: 'Chỉ nghe', hint: 'Hợp lúc đi đường hoặc không tiện nói.', icon: Headphones },
  { value: 'SpeakingOnly', label: 'Chỉ nói', hint: 'Cần micro và chỗ nói được thành tiếng.', icon: Mic },
  { value: 'ReadingOnly', label: 'Chỉ đọc', hint: 'Không cần loa, không cần micro.', icon: BookOpen },
  { value: 'WritingOnly', label: 'Chỉ viết', hint: 'Gõ và được chấm ngay tại máy chủ.', icon: PenLine },
]

/**
 * Màn hình chọn lĩnh vực và chế độ học.
 *
 * Hiện sau lần đăng nhập đầu, và vào lại được từ Cài đặt. Đổi lựa chọn KHÔNG xoá tiến độ:
 * người thử một lĩnh vực rồi quay lại vẫn còn nguyên bài đã học.
 */
export function ChooseTrackPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // Đường dẫn phải ĐỦ tiền tố /api/v1: api-client không có base URL, nó fetch nguyên văn.
  // Thiếu tiền tố thì request rơi vào route SPA và trả về index.html, rồi component vỡ ở
  // chỗ .map() với một lỗi không liên quan gì tới nguyên nhân thật.
  const { data, isLoading } = useQuery({
    queryKey: ['preferences'],
    queryFn: () => api.get<Preferences>('/api/v1/learning/preferences'),
  })

  const [track, setTrack] = useState<string | null>(null)
  const [mode, setMode] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: (body: { primaryTrack: string; studyMode: string }) =>
      api.put('/api/v1/learning/preferences', body),
    onSuccess: async () => {
      // Lộ trình và bảng điều khiển đều phụ thuộc lựa chọn này nên phải nạp lại,
      // nếu không học viên đổi xong vẫn thấy màn hình cũ và tưởng không ăn.
      await queryClient.invalidateQueries()
      navigate('/learn', { replace: true })
    },
  })

  if (isLoading || !data) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    )
  }

  const chosenTrack = track ?? data.primaryTrack
  const chosenMode = mode ?? data.studyMode

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title={data.onboardingCompleted ? 'Đổi lĩnh vực học' : 'Bạn muốn học tiếng Anh để làm gì?'}
          description="Chọn một lĩnh vực và một chế độ. Đổi lại lúc nào cũng được, và không mất tiến độ đã có."
          icon={<Compass className="size-5 text-brand-600" aria-hidden />}
        />
      </Card>

      <Card>
        <CardHeader title="Lĩnh vực" description="Bốn kỹ năng sẽ xoay quanh tình huống của lĩnh vực bạn chọn." />
        <CardBody>
          <ul className="grid gap-2 sm:grid-cols-2">
            {data.tracks.map((option) => {
              const active = option.value === chosenTrack

              return (
                <li key={option.value}>
                  <button
                    type="button"
                    onClick={() => setTrack(option.value)}
                    aria-pressed={active}
                    className={cn(
                      'flex w-full items-start gap-3 rounded-[var(--radius-card)] border p-3 text-left transition-colors',
                      active
                        ? 'border-brand-500 bg-brand-50 dark:bg-brand-900/40'
                        : 'border-[var(--border-subtle)] hover:bg-[var(--surface-hover)]',
                    )}
                  >
                    <span
                      className={cn(
                        'mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full border',
                        active ? 'border-brand-600 bg-brand-600 text-white' : 'border-[var(--border-strong)]',
                      )}
                      aria-hidden
                    >
                      {active && <Check className="size-3.5" />}
                    </span>

                    <span className="min-w-0">
                      <span className="block font-medium">{option.labelVi}</span>
                      <span className="block text-sm text-secondary">{option.hintVi}</span>

                      {/* Số bài thật, để không ai chọn vào một nhánh gần như rỗng mà không biết. */}
                      <span className="mt-1 block text-xs text-muted">{option.lessonCount} bài</span>
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="Chế độ học" description="Chọn một kỹ năng thì bài chỉ hiện bước của kỹ năng đó." />
        <CardBody>
          <ul className="grid gap-2 sm:grid-cols-2">
            {MODES.map((option) => {
              const active = option.value === chosenMode

              return (
                <li key={option.value}>
                  <button
                    type="button"
                    onClick={() => setMode(option.value)}
                    aria-pressed={active}
                    className={cn(
                      'flex w-full items-start gap-3 rounded-[var(--radius-card)] border p-3 text-left transition-colors',
                      active
                        ? 'border-brand-500 bg-brand-50 dark:bg-brand-900/40'
                        : 'border-[var(--border-subtle)] hover:bg-[var(--surface-hover)]',
                    )}
                  >
                    <option.icon className="mt-0.5 size-5 shrink-0 text-brand-600" aria-hidden />

                    <span className="min-w-0">
                      <span className="block font-medium">{option.label}</span>
                      <span className="block text-sm text-secondary">{option.hint}</span>
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>

          <div className="mt-5 flex flex-wrap items-center gap-3">
            <Button
              onClick={() => save.mutate({ primaryTrack: chosenTrack, studyMode: chosenMode })}
              loading={save.isPending}
            >
              Bắt đầu học
            </Button>

            {data.onboardingCompleted && (
              <Button variant="ghost" onClick={() => navigate('/learn')}>
                Để sau
              </Button>
            )}
          </div>

          {save.isError && (
            <p className="mt-3 text-sm text-[var(--color-danger-text)]">
              Chưa lưu được. Thử lại giúp tôi.
            </p>
          )}
        </CardBody>
      </Card>
    </div>
  )
}
