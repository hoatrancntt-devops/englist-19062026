import { Headphones, Mic, BookOpen, PenLine } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/cn'

export type Skill = 'Listening' | 'Speaking' | 'Reading' | 'Writing'

interface SkillMeta {
  icon: LucideIcon
  labelVi: string
  colorVar: string
}

/**
 * Một nguồn sự thật cho biểu tượng, nhãn tiếng Việt và màu của bốn kỹ năng.
 * Thứ tự khai báo chính là thứ tự ưu tiên toàn hệ thống: nghe, nói, đọc, viết.
 */
export const SKILL_META: Record<Skill, SkillMeta> = {
  Listening: { icon: Headphones, labelVi: 'Nghe', colorVar: 'var(--color-skill-listening)' },
  Speaking: { icon: Mic, labelVi: 'Nói', colorVar: 'var(--color-skill-speaking)' },
  Reading: { icon: BookOpen, labelVi: 'Đọc', colorVar: 'var(--color-skill-reading)' },
  Writing: { icon: PenLine, labelVi: 'Viết', colorVar: 'var(--color-skill-writing)' },
}

export const SKILL_ORDER: Skill[] = ['Listening', 'Speaking', 'Reading', 'Writing']

interface SkillBadgeProps {
  skill: Skill
  /** Hiện điểm bên cạnh nhãn. Bỏ trống khi chỉ cần nhãn. */
  score?: number
  size?: 'sm' | 'md'
  className?: string
}

export function SkillBadge({ skill, score, size = 'md', className }: SkillBadgeProps) {
  const meta = SKILL_META[skill]
  const Icon = meta.icon

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full font-medium',
        size === 'sm' ? 'px-2 py-0.5 text-xs' : 'px-2.5 py-1 text-sm',
        className,
      )}
      style={{
        backgroundColor: `color-mix(in oklch, ${meta.colorVar} 14%, transparent)`,
        color: meta.colorVar,
      }}
    >
      <Icon className={size === 'sm' ? 'size-3' : 'size-3.5'} aria-hidden />
      {meta.labelVi}
      {score !== undefined && <span className="tabular-nums font-semibold">{Math.round(score)}</span>}
    </span>
  )
}
