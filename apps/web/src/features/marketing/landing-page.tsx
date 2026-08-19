import { Link } from 'react-router-dom'
import { SiteFooter } from '@/components/layout/site-footer'
import {
  GraduationCap,
  Sun,
  Moon,
  ArrowRight,
  ShieldCheck,
  Server,
  Cloud,
  Cpu,
  Headset,
  Coffee,
  Building2,
  Wrench,
} from 'lucide-react'
import { useTheme } from '@/providers/theme-provider'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  HeroIllustration,
  InfraIllustration,
  CloudIllustration,
  SecurityIllustration,
  HelpdeskIllustration,
  AiIllustration,
  LifeIllustration,
  OfficeIllustration,
} from '@/components/illustrations/track-illustrations'

/**
 * Ba tầng ngữ cảnh. Đây là thứ phải hiện TRƯỚC năm nhánh nghề:
 * người mất gốc nhìn thẳng vào danh sách nhánh nghề sẽ tưởng phải biết sẵn tiếng Anh mới vào học được.
 */
const LAYERS = [
  {
    icon: Coffee,
    step: 'Tầng 1',
    title: 'Đời sống',
    body: 'Bảng chữ cái, số, giờ, tiền. Gọi món, đặt xe, hỏi đường, hẹn gặp. Và ba câu cứu hộ để không bao giờ phải đứng im.',
    examples: ['Sorry?', 'Could you say that again?', 'Could you speak more slowly?'],
    illustration: LifeIllustration,
  },
  {
    icon: Building2,
    step: 'Tầng 2',
    title: 'Văn phòng',
    body: 'Chào buổi sáng ở pantry, giới thiệu bản thân, xin nghỉ phép, hỏi lại yêu cầu chưa rõ, báo tiến độ, đặt và dời lịch họp, nhắn Teams/Slack.',
    examples: ['Could I take Friday off?', 'Just to make sure I got it right…', 'Can we move it to 3?'],
    illustration: OfficeIllustration,
  },
  {
    icon: Wrench,
    step: 'Tầng 3',
    title: 'Chuyên môn',
    body: 'Vận hành, báo cáo, họp team, xử lý sự cố, viết report — theo năm nhánh nghề bên dưới.',
    examples: ['The portal has been down since 2 AM.', "I'll escalate this to L3."],
    illustration: InfraIllustration,
  },
]

const TRACKS = [
  {
    icon: Server,
    title: 'Hạ tầng & Vận hành',
    body: 'Standup, báo outage, bàn giao ca trực, change review. Thuật ngữ bám ITIL 4.',
    illustration: InfraIllustration,
  },
  {
    icon: Headset,
    title: 'Helpdesk',
    body: 'Nhận ticket, hỏi để chẩn đoán, hướng dẫn từng bước, leo thang đúng cách.',
    illustration: HelpdeskIllustration,
  },
  {
    icon: ShieldCheck,
    title: 'Bảo mật',
    body: 'Báo sự cố bảo mật theo sáu giai đoạn của NIST SP 800-61, viết advisory nội bộ.',
    illustration: SecurityIllustration,
  },
  {
    icon: Cloud,
    title: 'Cloud',
    body: 'Trình bày đánh đổi kiến trúc theo sáu trụ cột Well-Architected, viết migration plan.',
    illustration: CloudIllustration,
  },
  {
    icon: Cpu,
    title: 'AI',
    body: 'Trình bày use case cho người không chuyên, viết proposal và risk assessment.',
    illustration: AiIllustration,
  },
]

export function LandingPage() {
  const { resolved, toggle } = useTheme()

  return (
    <div className="min-h-dvh bg-[var(--surface-base)]">
      <header className="border-b border-[var(--border-subtle)]">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-3 px-4">
          <span className="flex items-center gap-2 font-semibold">
            <GraduationCap className="size-6 text-brand-600" aria-hidden />
            English for IT
          </span>

          <div className="ml-auto flex items-center gap-2">
            <button
              type="button"
              onClick={toggle}
              className="rounded-md p-2 hover:bg-[var(--surface-hover)]"
              aria-label={resolved === 'dark' ? 'Chuyển sang nền sáng' : 'Chuyển sang nền tối'}
            >
              {resolved === 'dark' ? <Sun className="size-5" /> : <Moon className="size-5" />}
            </button>
            <Link to="/login">
              <Button variant="ghost" size="sm">
                Đăng nhập
              </Button>
            </Link>
            <Link to="/register">
              <Button size="sm">Bắt đầu</Button>
            </Link>
          </div>
        </div>
      </header>

      <main>
        <section className="mx-auto grid max-w-6xl items-center gap-10 px-4 py-16 lg:grid-cols-2">
          <div>
            <p className="text-sm font-medium text-brand-600">Dành cho kỹ sư IT, Cloud và AI</p>
            <h1 className="mt-3 text-3xl font-semibold leading-tight sm:text-4xl">
              Học tiếng Anh bằng đúng việc bạn làm mỗi ngày
            </h1>
            <p className="mt-4 text-secondary">
              Không phải "chào bạn, bạn khoẻ không". Là báo sự cố lúc 2 giờ sáng, gọi vendor,
              trình bày kiến trúc cloud, và viết postmortem — bằng tiếng Anh, giải thích bằng tiếng Việt.
            </p>

            <ul className="mt-6 space-y-2 text-sm">
              {[
                'Bắt đầu từ số 0: bảng chữ cái, số, giờ — rồi mới tới chuyên môn',
                'Chọn học một kỹ năng hoặc đủ bốn: nghe, nói, đọc, viết',
                'Chấm phát âm ngay tại máy chủ, giọng của bạn không đi đâu cả',
                'Mỗi buổi 15 phút, mục tiêu 45 phút mỗi ngày, có lộ trình chặn nhảy cóc',
              ].map((line) => (
                <li key={line} className="flex gap-2">
                  <span className="mt-2 size-1.5 shrink-0 rounded-full bg-brand-500" aria-hidden />
                  <span className="text-secondary">{line}</span>
                </li>
              ))}
            </ul>

            <div className="mt-8 flex flex-wrap gap-3">
              <Link to="/register">
                <Button size="lg">
                  Tạo tài khoản miễn phí
                  <ArrowRight className="size-4" aria-hidden />
                </Button>
              </Link>
              <Link to="/login">
                <Button size="lg" variant="secondary">
                  Tôi đã có tài khoản
                </Button>
              </Link>
            </div>
          </div>

          <div className="flex justify-center lg:justify-end">
            <HeroIllustration size={400} className="text-brand-500" />
          </div>
        </section>

        <section className="border-t border-[var(--border-subtle)] py-16">
          <div className="mx-auto max-w-6xl px-4">
            <h2 className="text-center text-2xl font-semibold">Ba tầng, đi từ dễ tới khó</h2>
            <p className="mx-auto mt-2 max-w-2xl text-center text-sm text-secondary">
              Mất gốc thì bắt đầu ở tầng 1. Không phải vì tầng 1 dễ hơn, mà vì báo sự cố
              mà nói sai giờ thì cả câu thành vô nghĩa.
            </p>

            <ol className="mt-10 grid gap-4 lg:grid-cols-3">
              {LAYERS.map((layer) => (
                <li key={layer.title}>
                  <Card className="flex h-full flex-col p-5">
                    <div className="flex items-center justify-center py-2">
                      <layer.illustration size={140} className="text-brand-500" />
                    </div>

                    <span className="mt-3 text-xs font-semibold uppercase tracking-wide text-brand-600">
                      {layer.step}
                    </span>
                    <h3 className="mt-1 flex items-center gap-2 text-lg font-semibold">
                      <layer.icon className="size-5 text-brand-600" aria-hidden />
                      {layer.title}
                    </h3>
                    <p className="mt-1.5 text-sm text-secondary">{layer.body}</p>

                    <ul className="mt-3 space-y-1">
                      {layer.examples.map((example) => (
                        <li
                          key={example}
                          className="rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-2.5 py-1 font-mono text-xs text-secondary"
                        >
                          {example}
                        </li>
                      ))}
                    </ul>
                  </Card>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section className="border-t border-[var(--border-subtle)] bg-[var(--surface-sunken)] py-16">
          <div className="mx-auto max-w-6xl px-4">
            <h2 className="text-center text-2xl font-semibold">Tầng 3 — năm nhánh nghề</h2>
            <p className="mx-auto mt-2 max-w-2xl text-center text-sm text-secondary">
              Mỗi nhánh bám một chuẩn ngành có thật, không phải tình huống tự nghĩ ra.
            </p>

            <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {TRACKS.map((track) => (
                <Card key={track.title} className="flex flex-col p-5">
                  <div className="flex items-center justify-center py-2">
                    <track.illustration size={130} className="text-brand-500" />
                  </div>
                  <h3 className="mt-3 flex items-center gap-2 font-semibold">
                    <track.icon className="size-4.5 text-brand-600" aria-hidden />
                    {track.title}
                  </h3>
                  <p className="mt-1.5 text-sm text-secondary">{track.body}</p>
                </Card>
              ))}
            </div>
          </div>
        </section>
      </main>

      <SiteFooter />
    </div>
  )
}
