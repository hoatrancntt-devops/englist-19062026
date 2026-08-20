import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Check, Clock, KeyRound, Target } from 'lucide-react'
import { api, ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { SkeletonCard } from '@/components/ui/feedback'

interface TimeZoneOption {
  id: string
  labelVi: string
}

interface LearningSchedule {
  dailyMinutesTarget: number
  reminderHourLocal: number
  timeZone: string
  timeZones: TimeZoneOption[]
}

/**
 * Cài đặt.
 *
 * Hai phần tách hẳn nhau vì hậu quả khác nhau hoàn toàn: đổi lịch học chỉ lưu một dòng,
 * còn đổi mật khẩu đăng xuất mọi thiết bị kể cả thiết bị đang dùng.
 */
export function SettingsPage() {
  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Cài đặt</h1>
        <p className="text-secondary">Lịch học và mật khẩu.</p>
      </header>

      <ScheduleCard />
      <PasswordCard />
    </div>
  )
}

function ScheduleCard() {
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'schedule'],
    queryFn: () => api.get<LearningSchedule>('/api/v1/learning/schedule'),
  })

  const [minutes, setMinutes] = useState(45)
  const [hour, setHour] = useState(20)
  const [zone, setZone] = useState('Asia/Ho_Chi_Minh')
  const [saved, setSaved] = useState(false)

  // Đổ giá trị máy chủ vào form một lần khi tải xong. Không dùng defaultValue vì lúc render
  // đầu tiên dữ liệu chưa về, ô sẽ giữ mãi giá trị mặc định.
  useEffect(() => {
    if (!data) {
      return
    }

    setMinutes(data.dailyMinutesTarget)
    setHour(data.reminderHourLocal)
    setZone(data.timeZone)
  }, [data])

  const save = useMutation({
    mutationFn: () =>
      api.put<{ message: string }>('/api/v1/learning/schedule', {
        dailyMinutesTarget: minutes,
        reminderHourLocal: hour,
        timeZone: zone,
      }),
    onSuccess: () => {
      setSaved(true)
      void queryClient.invalidateQueries({ queryKey: ['settings', 'schedule'] })

      // Chuỗi ngày đo theo mục tiêu phút, nên bảng điều khiển đang hiện một con số cũ.
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })

  if (isLoading || !data) {
    return <SkeletonCard />
  }

  const error = save.error instanceof ApiError ? save.error.message : null

  return (
    <Card>
      <CardHeader
        title="Lịch học"
        description="Mỗi ngày bạn định học bao lâu, và muốn được nhắc lúc mấy giờ."
        icon={<Target className="size-5 text-brand-600" aria-hidden />}
      />

      <CardBody className="space-y-5 pt-0">
        <div className="space-y-2">
          <label htmlFor="daily-minutes" className="block text-sm font-medium">
            Mục tiêu mỗi ngày: <strong>{minutes} phút</strong>
          </label>

          <input
            id="daily-minutes"
            type="range"
            min={10}
            max={120}
            step={5}
            value={minutes}
            onChange={(e) => {
              setMinutes(Number(e.target.value))
              setSaved(false)
            }}
            className="w-full accent-brand-600"
          />

          <p className="text-xs text-muted">
            Một bài dài 10–12 phút. Ngày nào đủ mục tiêu này và chạm đủ bốn kỹ năng thì được
            tính vào chuỗi ngày.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor="reminder-hour" className="block text-sm font-medium">
              Giờ nhắc học
            </label>

            <select
              id="reminder-hour"
              value={hour}
              onChange={(e) => {
                setHour(Number(e.target.value))
                setSaved(false)
              }}
              className="w-full rounded-[var(--radius-control)] border border-[var(--border-strong)] bg-[var(--surface-raised)] px-3 py-2 text-sm focus:border-brand-500"
            >
              {Array.from({ length: 24 }, (_, h) => (
                <option key={h} value={h}>
                  {String(h).padStart(2, '0')}:00
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label htmlFor="time-zone" className="block text-sm font-medium">
              Múi giờ
            </label>

            <select
              id="time-zone"
              value={zone}
              onChange={(e) => {
                setZone(e.target.value)
                setSaved(false)
              }}
              className="w-full rounded-[var(--radius-control)] border border-[var(--border-strong)] bg-[var(--surface-raised)] px-3 py-2 text-sm focus:border-brand-500"
            >
              {data.timeZones.map((z) => (
                <option key={z.id} value={z.id}>
                  {z.labelVi}
                </option>
              ))}
            </select>
          </div>
        </div>

        <p className="text-xs text-muted">
          Giờ nhắc tính theo múi giờ này. Chọn sai múi giờ thì lời nhắc tới lệch cả buổi, và
          ngày học cũng bị cắt sai chỗ.
        </p>

        {error ? <p className="text-sm text-[var(--color-danger-text)]">{error}</p> : null}

        <div className="flex items-center gap-3">
          <Button onClick={() => save.mutate()} loading={save.isPending}>
            Lưu lịch học
          </Button>

          {saved && !save.isPending ? (
            <span className="flex items-center gap-1 text-sm text-[var(--color-success-text)]">
              <Check className="size-4" aria-hidden />
              Đã lưu
            </span>
          ) : null}
        </div>
      </CardBody>
    </Card>
  )
}

function PasswordCard() {
  const navigate = useNavigate()

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [mismatch, setMismatch] = useState(false)

  const change = useMutation({
    mutationFn: () =>
      api.post<{ message: string }>('/api/v1/auth/change-password', {
        currentPassword: current,
        newPassword: next,
      }),

    // Máy chủ đã xoá cookie phiên: mọi request sau đây đều 401. Đưa thẳng về trang đăng nhập
    // thay vì để học viên bấm loanh quanh rồi tưởng ứng dụng hỏng.
    onSuccess: () => navigate('/login', { replace: true }),
  })

  const error = change.error instanceof ApiError ? change.error.message : null
  const tooShort = next.length > 0 && next.length < 12

  const submit = () => {
    if (next !== confirm) {
      setMismatch(true)
      return
    }

    setMismatch(false)
    change.mutate()
  }

  return (
    <Card>
      <CardHeader
        title="Đổi mật khẩu"
        description="Đổi xong, mọi thiết bị đang đăng nhập đều bị đăng xuất — kể cả thiết bị này."
        icon={<KeyRound className="size-5 text-brand-600" aria-hidden />}
      />

      <CardBody className="space-y-4 pt-0">
        <div className="grid gap-4 sm:grid-cols-3">
          <Field
            id="current-password"
            label="Mật khẩu hiện tại"
            value={current}
            onChange={(v) => setCurrent(v)}
          />
          <Field
            id="new-password"
            label="Mật khẩu mới"
            value={next}
            onChange={(v) => {
              setNext(v)
              setMismatch(false)
            }}
          />
          <Field
            id="confirm-password"
            label="Nhập lại mật khẩu mới"
            value={confirm}
            onChange={(v) => {
              setConfirm(v)
              setMismatch(false)
            }}
          />
        </div>

        {tooShort ? (
          <p className="text-sm text-secondary">Mật khẩu mới phải dài ít nhất 12 ký tự.</p>
        ) : null}

        {mismatch ? (
          <p className="text-sm text-[var(--color-danger-text)]">
            Hai ô mật khẩu mới không khớp nhau.
          </p>
        ) : null}

        {error ? <p className="text-sm text-[var(--color-danger-text)]">{error}</p> : null}

        <div className="flex items-start gap-3">
          <Button
            onClick={submit}
            loading={change.isPending}
            disabled={!current || next.length < 12 || !confirm}
          >
            Đổi mật khẩu
          </Button>

          <p className="flex items-center gap-1 pt-2 text-xs text-muted">
            <Clock className="size-3.5 shrink-0" aria-hidden />
            Bạn sẽ phải đăng nhập lại ngay sau khi đổi.
          </p>
        </div>
      </CardBody>
    </Card>
  )
}

function Field({
  id,
  label,
  value,
  onChange,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
}) {
  return (
    <div className="space-y-2">
      <label htmlFor={id} className="block text-sm font-medium">
        {label}
      </label>
      <input
        id={id}
        type="password"
        autoComplete={id === 'current-password' ? 'current-password' : 'new-password'}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-[var(--radius-control)] border border-[var(--border-strong)] bg-[var(--surface-raised)] px-3 py-2 text-sm focus:border-brand-500"
      />
    </div>
  )
}
