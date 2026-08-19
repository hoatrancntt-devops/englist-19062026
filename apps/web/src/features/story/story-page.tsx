import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, BookMarked, Check, Lock, Users } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, SkeletonCard } from '@/components/ui/feedback'
import type { StoryChapterDetail, StoryChapterSummary } from './story-types'

/**
 * Mạch truyện.
 *
 * Chương chưa mở vẫn hiện tiêu đề và câu mở — đó là toàn bộ cơ chế giữ chân: học viên
 * thấy trước thứ mình sắp được đọc và biết chính xác phải học bài nào để mở nó.
 *
 * Thân chương chỉ tải khi bấm vào chương đã mở. Máy chủ không gửi thân của chương khoá,
 * nên không có cách nào đọc trước bằng công cụ nhà phát triển.
 */
export function StoryPage() {
  const queryClient = useQueryClient()
  const [reading, setReading] = useState<string | null>(null)

  const { data: chapters, isLoading } = useQuery({
    queryKey: ['story', 'list'],
    queryFn: () => api.get<StoryChapterSummary[]>('/api/v1/story'),
  })

  // Lần đọc đầu được máy chủ ghi mốc. Lấy lại danh sách lúc quay ra để nhãn "Đã đọc"
  // khớp trạng thái thật, thay vì đợi tới lần vào trang sau.
  const closeReader = () => {
    setReading(null)
    void queryClient.invalidateQueries({ queryKey: ['story', 'list'] })
  }

  if (isLoading) {
    return <SkeletonCard />
  }

  if (reading) {
    return <ChapterReader code={reading} onBack={closeReader} />
  }

  if (!chapters || chapters.length === 0) {
    return (
      <Card>
        <CardBody className="py-10 text-center text-secondary">Chưa có chương truyện nào.</CardBody>
      </Card>
    )
  }

  const unlocked = chapters.filter((c) => c.unlocked).length

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold">Mạch truyện</h1>
        <p className="max-w-2xl text-secondary">
          Sáu tháng đầu đi làm ở HT Group. Mỗi chặng học xong mở một chương — đọc để biết thứ
          mình vừa học dùng vào lúc nào.
        </p>
        <p className="text-sm text-secondary">
          Đã mở {unlocked}/{chapters.length} chương.
        </p>
      </header>

      <ol className="space-y-3">
        {chapters.map((chapter) => (
          <li key={chapter.code}>
            <ChapterCard chapter={chapter} onRead={() => setReading(chapter.code)} />
          </li>
        ))}
      </ol>
    </div>
  )
}

function ChapterCard({ chapter, onRead }: { chapter: StoryChapterSummary; onRead: () => void }) {
  const locked = !chapter.unlocked

  return (
    <Card className={cn(locked && 'opacity-70')}>
      <CardBody className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-medium text-secondary">Chương {chapter.number}</span>

            {chapter.readAt ? (
              <Badge tone="success">
                <Check className="mr-1 inline size-3" aria-hidden />
                Đã đọc
              </Badge>
            ) : null}

            {locked ? (
              <Badge>
                <Lock className="mr-1 inline size-3" aria-hidden />
                Chưa mở
              </Badge>
            ) : null}
          </div>

          <h2 className={cn('text-lg font-semibold', locked && 'text-secondary')}>
            {chapter.titleVi}
          </h2>

          <p className="text-secondary">{chapter.hookVi}</p>

          {locked ? (
            <p className="pt-1 text-sm text-secondary">
              Mở khi bạn thông thạo{' '}
              <Link
                to="/learn/roadmap"
                className="font-medium text-brand-600 underline dark:text-brand-300"
              >
                {chapter.unlockAfterLessonCode}
                {chapter.unlockAfterLessonTitle ? ` — ${chapter.unlockAfterLessonTitle}` : ''}
              </Link>
            </p>
          ) : null}
        </div>

        {locked ? null : (
          <Button onClick={onRead} className="shrink-0">
            <BookMarked className="mr-2 size-4" aria-hidden />
            {chapter.readAt ? 'Đọc lại' : 'Đọc'}
          </Button>
        )}
      </CardBody>
    </Card>
  )
}

function ChapterReader({ code, onBack }: { code: string; onBack: () => void }) {
  const navigate = useNavigate()

  const { data: chapter, isLoading } = useQuery({
    queryKey: ['story', 'chapter', code],
    queryFn: () => api.get<StoryChapterDetail>(`/api/v1/story/${code}`),
  })

  if (isLoading) {
    return <SkeletonCard />
  }

  if (!chapter) {
    return (
      <Card>
        <CardBody className="space-y-4 py-10 text-center">
          <p className="text-secondary">Chương này chưa mở.</p>
          <Button onClick={onBack}>Quay lại danh sách</Button>
        </CardBody>
      </Card>
    )
  }

  return (
    <article className="space-y-6">
      <Button variant="ghost" onClick={onBack} className="-ml-2">
        <ArrowLeft className="mr-2 size-4" aria-hidden />
        Tất cả chương
      </Button>

      <header className="space-y-2">
        <p className="text-sm font-medium text-secondary">Chương {chapter.number}</p>
        <h1 className="text-2xl font-semibold">{chapter.titleVi}</h1>
      </header>

      {chapter.newCharacters.length > 0 ? (
        <Card>
          <CardHeader title="Nhân vật mới" icon={<Users className="size-4" aria-hidden />} />
          <CardBody className="space-y-1 pt-0">
            {chapter.newCharacters.map((person) => (
              <p key={person} className="text-secondary">
                {person}
              </p>
            ))}
          </CardBody>
        </Card>
      ) : null}

      {/* Xuống dòng giữ nguyên từ nội dung gốc: chương viết theo đoạn, gộp lại thành
          một khối chữ liền là mất nhịp đọc. */}
      <div className="whitespace-pre-line text-lg leading-relaxed">{chapter.bodyVi}</div>

      <Card>
        <CardBody className="text-secondary italic">{chapter.endsVi}</CardBody>
      </Card>

      <div className="flex justify-between gap-3">
        <Button variant="ghost" onClick={onBack}>
          Tất cả chương
        </Button>
        <Button onClick={() => navigate('/learn/roadmap')}>Học tiếp</Button>
      </div>
    </article>
  )
}
