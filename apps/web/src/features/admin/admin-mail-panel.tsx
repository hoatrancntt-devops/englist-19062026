import { useState } from 'react'

import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { TextField } from '@/components/ui/text-field'
import { api, ApiError } from '@/lib/api-client'
import type { MailSettings } from './admin-types'

/**
 * Cấu hình gửi thư.
 *
 * Quy tắc về mật khẩu, giải thích ngay trên màn cho người vận hành thấy: <b>ô mật khẩu để
 * trống nghĩa là giữ nguyên cái đang có, không phải xoá nó.</b> Máy chủ không trả mật khẩu
 * về nên màn này không thể hiển thị lại; nếu để trống mà xoá thì mỗi lần sửa cổng SMTP
 * người vận hành lại mất mật khẩu mà không biết.
 */
export function AdminMailPanel({
  settings,
  onSaved,
}: {
  settings: MailSettings
  onSaved: () => void
}) {
  const [form, setForm] = useState({
    enabled: settings.enabled,
    fromAddress: settings.fromAddress,
    fromDisplayName: settings.fromDisplayName,
    smtpHost: settings.smtpHost ?? '',
    smtpPort: settings.smtpPort?.toString() ?? '587',
    smtpUseStartTls: settings.smtpUseStartTls,
    smtpUsername: settings.smtpUsername ?? '',
    smtpPassword: '',
  })

  const [saving, setSaving] = useState(false)
  const [testAddress, setTestAddress] = useState('')
  const [message, setMessage] = useState<{ kind: 'ok' | 'loi'; text: string } | null>(null)

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  const save = async () => {
    setSaving(true)
    setMessage(null)

    try {
      await api.put('/api/v1/admin/mail', {
        enabled: form.enabled,
        fromAddress: form.fromAddress,
        fromDisplayName: form.fromDisplayName,
        smtpHost: form.smtpHost || null,
        smtpPort: form.smtpPort ? Number(form.smtpPort) : null,
        smtpUseStartTls: form.smtpUseStartTls,
        smtpUsername: form.smtpUsername || null,
        smtpPassword: form.smtpPassword || null,
      })

      // Xoá ô mật khẩu sau khi lưu: giữ lại trên màn thì lần lưu sau vô tình ghi đè.
      set('smtpPassword', '')
      setMessage({ kind: 'ok', text: 'Đã lưu cấu hình gửi thư.' })
      onSaved()
    } catch (caught) {
      setMessage({
        kind: 'loi',
        text: caught instanceof ApiError ? caught.message : 'Không lưu được cấu hình.',
      })
    } finally {
      setSaving(false)
    }
  }

  const sendTest = async () => {
    setMessage(null)

    try {
      const result = await api.post<{ message: string }>('/api/v1/admin/mail/test', {
        toAddress: testAddress,
      })

      setMessage({ kind: 'ok', text: result.message })
    } catch (caught) {
      setMessage({
        kind: 'loi',
        text: caught instanceof ApiError ? caught.message : 'Không xếp được thư thử.',
      })
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Máy chủ gửi thư"
          description="Dùng cho thư xác minh tài khoản và đặt lại mật khẩu."
        />
        <CardBody className="space-y-4">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.enabled}
              onChange={(event) => set('enabled', event.target.checked)}
            />
            Bật gửi thư
          </label>

          <div className="grid gap-3 sm:grid-cols-2">
            <TextField
              label="Địa chỉ gửi"
              value={form.fromAddress}
              onChange={(event) => set('fromAddress', event.target.value)}
              placeholder="khong-tra-loi@congty.vn"
            />
            <TextField
              label="Tên hiển thị"
              value={form.fromDisplayName}
              onChange={(event) => set('fromDisplayName', event.target.value)}
              placeholder="English for IT"
            />
            <TextField
              label="Máy chủ SMTP"
              value={form.smtpHost}
              onChange={(event) => set('smtpHost', event.target.value)}
              placeholder="smtp.congty.vn"
            />
            <TextField
              label="Cổng"
              value={form.smtpPort}
              onChange={(event) => set('smtpPort', event.target.value)}
              inputMode="numeric"
            />
            <TextField
              label="Tên đăng nhập"
              value={form.smtpUsername}
              onChange={(event) => set('smtpUsername', event.target.value)}
            />
            <TextField
              label="Mật khẩu"
              type="password"
              value={form.smtpPassword}
              onChange={(event) => set('smtpPassword', event.target.value)}
              hint={
                settings.hasPassword
                  ? 'Đã có mật khẩu. Để trống thì giữ nguyên, chỉ nhập khi muốn đổi.'
                  : 'Chưa đặt mật khẩu.'
              }
            />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.smtpUseStartTls}
              onChange={(event) => set('smtpUseStartTls', event.target.checked)}
            />
            Dùng STARTTLS
          </label>

          <Button onClick={save} loading={saving}>
            Lưu cấu hình
          </Button>
        </CardBody>
      </Card>

      <Card>
        <CardHeader
          title="Gửi thư thử"
          description="Thư được xếp vào hộp thư đi, worker gửi trong vòng một phút. Xem kết quả ở tab Tổng quan."
        />
        <CardBody className="flex flex-wrap items-end gap-3">
          {/*
            Tắt gửi thư mà vẫn xếp được thư thử là cái bẫy: thư nằm mãi ở trạng thái chờ, và
            lý do chỉ hiện trong cột lỗi của hộp thư đi. Nói trước ở đây để không ai phải đi đào.
          */}
          {!settings.enabled && (
            <p className="w-full rounded-[var(--radius-control)] bg-[color-mix(in_oklch,var(--color-warning)_12%,transparent)] p-3 text-sm text-secondary">
              Gửi thư đang tắt, nên thư thử sẽ nằm chờ chứ không được gửi đi. Tích{' '}
              <strong className="font-semibold text-[var(--text-primary)]">Bật gửi thư</strong> ở
              trên rồi lưu lại trước.
            </p>
          )}

          <TextField
            label="Gửi tới"
            value={testAddress}
            onChange={(event) => setTestAddress(event.target.value)}
            placeholder="ban@congty.vn"
            className="min-w-[16rem] flex-1"
          />
          <Button variant="secondary" onClick={sendTest} disabled={!testAddress}>
            Gửi thư thử
          </Button>
        </CardBody>
      </Card>

      {message && (
        <p
          className={
            message.kind === 'ok'
              ? 'text-sm text-[var(--color-success)]'
              : 'text-sm text-[var(--color-danger)]'
          }
        >
          {message.text}
        </p>
      )}
    </div>
  )
}
