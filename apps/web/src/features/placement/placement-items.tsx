import { Play, Square, Volume2, Mic } from 'lucide-react'
import { cn } from '@/lib/cn'
import { useSpeech } from '@/lib/use-speech'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/feedback'
import { promptLabelId } from './placement-types'
import type { PlacementCard, PlacementResponse } from './placement-types'

interface ItemProps {
  card: PlacementCard
  /** Câu trả lời hiện tại, giữ ở component cha để quay lại câu trước vẫn còn. */
  value: PlacementResponse | null
  onChange: (response: PlacementResponse) => void
}

/**
 * Vẽ một câu theo đúng dạng của nó.
 *
 * Không có nhánh nào hiển thị điểm hay đáp án: cả hai chỉ tồn tại phía máy chủ
 * cho tới lúc nộp toàn bài. Component này thậm chí không có đường nào nhận được chúng.
 */
export function PlacementItem({ card, value, onChange }: ItemProps) {
  switch (card.kind) {
    case 'Mcq':
    case 'McqRead':
    case 'Likert':
      return <ChoiceItem card={card} value={value} onChange={onChange} />

    case 'FillBlank':
    case 'ErrorCorrection':
    case 'ShortAnswer':
      return <ShortTextItem card={card} value={value} onChange={onChange} />

    case 'GuidedEmail':
      return <EmailItem card={card} value={value} onChange={onChange} />

    case 'ReadAloud':
    case 'Repeat':
      return <SpeakingItem card={card} />

    default:
      return null
  }
}

/** Chọn một trong nhiều lựa chọn. Dùng cho cả trắc nghiệm nghe, đọc và câu Likert. */
function ChoiceItem({ card, value, onChange }: ItemProps) {
  const chosen = value && 'choiceIndex' in value ? value.choiceIndex : null

  return (
    <div className="space-y-4">
      {card.prompt.audioText && <AudioPlayer text={card.prompt.audioText} speed={card.prompt.speed} />}

      {card.prompt.passageEn && (
        <blockquote className="rounded-[var(--radius-card)] border-l-4 border-brand-400 bg-[var(--surface-sunken)] p-4 text-sm leading-relaxed">
          {card.prompt.passageEn}
        </blockquote>
      )}

      {card.prompt.questionEn && <p className="font-medium">{card.prompt.questionEn}</p>}

      {/* Trỏ tới đề bài đã hiện sẵn phía trên thay vì lặp lại nó trong legend ẩn:
          lặp lại làm người dùng screen reader nghe cùng một câu hai lần. */}
      <fieldset className="space-y-2" aria-labelledby={promptLabelId(card.itemCode)}>
        {(card.prompt.choices ?? []).map((choice, index) => (
          <label
            key={index}
            className={cn(
              'flex cursor-pointer items-start gap-3 rounded-[var(--radius-control)] border p-3 text-sm transition-colors',
              chosen === index
                ? 'border-brand-500 bg-brand-50 dark:bg-brand-900/30'
                : 'border-[var(--border-subtle)] hover:bg-[var(--surface-hover)]',
            )}
          >
            <input
              type="radio"
              name={card.itemCode}
              checked={chosen === index}
              onChange={() => onChange({ choiceIndex: index })}
              className="mt-0.5 size-4 accent-[var(--brand-600)]"
            />
            <span>{choice}</span>
          </label>
        ))}
      </fieldset>
    </div>
  )
}

/** Điền chỗ trống, sửa lỗi, trả lời ngắn — cùng một ô nhập, khác phần dẫn. */
function ShortTextItem({ card, value, onChange }: ItemProps) {
  const text = value && 'text' in value ? value.text : ''

  return (
    <div className="space-y-4">
      {card.prompt.sentenceEn && (
        <p className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4 font-mono text-sm leading-relaxed">
          {card.prompt.sentenceEn}
        </p>
      )}

      <div>
        <input
          aria-labelledby={promptLabelId(card.itemCode)}
          type="text"
          value={text}
          onChange={(event) => onChange({ text: event.target.value })}
          autoComplete="off"
          spellCheck={false}
          placeholder={card.kind === 'FillBlank' ? 'Một từ' : 'Viết cả câu'}
          className="w-full rounded-[var(--radius-control)] border border-[var(--border-subtle)] bg-[var(--surface-raised)] px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:focus:ring-brand-900"
        />
      </div>
    </div>
  )
}

/** Email có hướng dẫn: các ý bắt buộc hiện ngay cạnh ô viết, không giấu trong đề bài. */
function EmailItem({ card, value, onChange }: ItemProps) {
  const text = value && 'text' in value ? value.text : ''
  const words = text.trim().split(/\s+/).filter(Boolean).length

  return (
    <div className="space-y-4">
      {card.prompt.scenarioVi && (
        <p className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4 text-sm leading-relaxed">
          {card.prompt.scenarioVi}
        </p>
      )}

      {card.prompt.requiredPointsVi && (
        <ul className="space-y-1 text-sm">
          {card.prompt.requiredPointsVi.map((point) => (
            <li key={point} className="flex items-start gap-2">
              <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-brand-500" aria-hidden />
              {point}
            </li>
          ))}
        </ul>
      )}

      <div>
        <textarea
          aria-labelledby={promptLabelId(card.itemCode)}
          value={text}
          onChange={(event) => onChange({ text: event.target.value })}
          rows={7}
          spellCheck={false}
          placeholder="Hi team, ..."
          className="w-full rounded-[var(--radius-control)] border border-[var(--border-subtle)] bg-[var(--surface-raised)] px-3 py-2 text-sm leading-relaxed outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:focus:ring-brand-900"
        />
        <p className="mt-1 text-xs text-muted">{words} từ</p>
      </div>
    </div>
  )
}

/**
 * Câu nói.
 *
 * Chỉ xuất hiện khi bật cờ LearningPolicy:PlacementSpeakingEnabled. Bật cờ mà chưa có
 * dịch vụ chấm phát âm thì màn này nói thẳng là chưa chấm được, thay vì hiện một ô
 * trống không bấm được — hỏng im lặng là kiểu hỏng khó tìm nhất.
 */
function SpeakingItem({ card }: { card: PlacementCard }) {
  return (
    <div className="space-y-4">
      {card.prompt.audioText && <AudioPlayer text={card.prompt.audioText} speed={card.prompt.speed} />}

      {card.prompt.targetEn && (
        <p className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4 text-lg leading-relaxed">
          {card.prompt.targetEn}
        </p>
      )}

      <div className="flex items-start gap-3 rounded-[var(--radius-card)] border border-dashed border-[var(--border-subtle)] p-4 text-sm text-secondary">
        <Mic className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>
          Phần chấm phát âm chưa hoạt động nên câu này không tính điểm. Bấm sang câu tiếp theo.
        </span>
      </div>
    </div>
  )
}

/**
 * Nút nghe.
 *
 * Dùng giọng của trình duyệt, giống bước Nghe trong màn học. Đây là giải pháp tạm
 * chờ audio sinh sẵn bằng Piper — xem ghi chú trong use-speech.ts.
 */
function AudioPlayer({ text, speed }: { text: string; speed?: number }) {
  const speech = useSpeech()

  return (
    <div className="rounded-[var(--radius-card)] bg-[var(--surface-sunken)] p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span className="flex items-center gap-2 text-sm font-medium">
          <Volume2 className="size-4 text-brand-600" aria-hidden />
          Đoạn nghe
          {speed && <Badge>tốc độ {speed}×</Badge>}
        </span>

        <Button
          size="sm"
          onClick={() => (speech.speaking ? speech.stop() : speech.speak(text, speed ?? 1))}
          disabled={!speech.ready}
        >
          {speech.speaking ? (
            <>
              <Square className="size-4" aria-hidden />
              Dừng
            </>
          ) : (
            <>
              <Play className="size-4" aria-hidden />
              Nghe
            </>
          )}
        </Button>
      </div>

      {!speech.ready && (
        // Không báo thì học viên tưởng tai mình có vấn đề.
        <p className="mt-2 text-xs text-muted">
          Trình duyệt này chưa có giọng đọc tiếng Anh. Thử Chrome hoặc Edge để làm phần nghe.
        </p>
      )}
    </div>
  )
}
