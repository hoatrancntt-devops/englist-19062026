import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Bot, FileStack, LayoutDashboard, Mail, ScrollText } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { SkeletonCard } from '@/components/ui/feedback'
import { api, ApiError } from '@/lib/api-client'
import { cn } from '@/lib/cn'
import { AdminAiPanel } from './admin-ai-panel'
import { AdminContentPanel } from './admin-content-panel'
import { AdminMailPanel } from './admin-mail-panel'
import { AdminOverviewPanel } from './admin-overview-panel'
import type { AdminOverview, AiStatus, AuditEntry, MailSettings } from './admin-types'

type Tab = 'tong-quan' | 'noi-dung' | 'gui-thu' | 'ai' | 'nhat-ky'

const TABS: { id: Tab; label: string; icon: typeof LayoutDashboard }[] = [
  { id: 'tong-quan', label: 'Tổng quan', icon: LayoutDashboard },
  { id: 'noi-dung', label: 'Nội dung', icon: FileStack },
  { id: 'gui-thu', label: 'Gửi thư', icon: Mail },
  { id: 'ai', label: 'AI', icon: Bot },
  { id: 'nhat-ky', label: 'Nhật ký', icon: ScrollText },
]

/**
 * Khu quản trị.
 *
 * Tải cả bốn nguồn dữ liệu một lần rồi chia sang các tab, thay vì mỗi tab tự gọi khi được mở.
 * Người vận hành thường nhảy qua lại giữa các tab khi dò một sự cố; gọi lại mỗi lần chuyển
 * tab làm màn giật và che mất con số họ vừa nhìn.
 */
export function AdminShell() {
  const [tab, setTab] = useState<Tab>('tong-quan')

  const [overview, setOverview] = useState<AdminOverview | null>(null)
  const [mail, setMail] = useState<MailSettings | null>(null)
  const [ai, setAi] = useState<AiStatus | null>(null)
  const [audit, setAudit] = useState<AuditEntry[]>([])

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setError(null)

    try {
      // Tải song song. Một nguồn chậm không nên giữ ba nguồn còn lại.
      const [nextOverview, nextMail, nextAi, nextAudit] = await Promise.all([
        api.get<AdminOverview>('/api/v1/admin/overview'),
        api.get<MailSettings>('/api/v1/admin/mail'),
        api.get<AiStatus>('/api/v1/admin/ai/status'),
        api.get<AuditEntry[]>('/api/v1/admin/audit'),
      ])

      setOverview(nextOverview)
      setMail(nextMail)
      setAi(nextAi)
      setAudit(nextAudit)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Không tải được dữ liệu quản trị.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div className="mx-auto max-w-5xl space-y-4 p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Khu quản trị</h1>

        <div className="flex gap-2">
          <Button variant="ghost" size="sm" onClick={() => void load()}>
            Tải lại
          </Button>
          <Link to="/learn">
            <Button variant="secondary" size="sm">
              Về khu học viên
            </Button>
          </Link>
        </div>
      </div>

      <nav className="flex flex-wrap gap-1" aria-label="Mục quản trị">
        {TABS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            type="button"
            onClick={() => setTab(id)}
            aria-current={tab === id ? 'page' : undefined}
            className={cn(
              'flex items-center gap-2 rounded-[var(--radius-control)] px-3 py-2 text-sm',
              tab === id
                ? 'bg-[var(--surface-sunken)] font-medium'
                : 'text-secondary hover:bg-[var(--surface-sunken)]',
            )}
          >
            <Icon className="size-4" aria-hidden />
            {label}
          </button>
        ))}
      </nav>

      {error && <p className="text-sm text-[var(--color-danger)]">{error}</p>}

      {loading ? (
        <SkeletonCard />
      ) : (
        <>
          {tab === 'tong-quan' && overview && <AdminOverviewPanel data={overview} />}
          {tab === 'noi-dung' && <AdminContentPanel />}
          {tab === 'gui-thu' && mail && <AdminMailPanel settings={mail} onSaved={load} />}
          {tab === 'ai' && ai && <AdminAiPanel status={ai} />}
          {tab === 'nhat-ky' && <AuditPanel entries={audit} />}
        </>
      )}
    </div>
  )
}

/**
 * Nhật ký kiểm toán.
 *
 * Chỉ ghi TÊN hành động và đối tượng, không ghi giá trị — nhật ký mà chứa mật khẩu cũ thì
 * chính nó thành chỗ rò rỉ.
 */
function AuditPanel({ entries }: { entries: AuditEntry[] }) {
  return (
    <Card>
      <CardHeader
        title="Nhật ký kiểm toán"
        description="100 việc gần nhất. Chỉ ghi ai làm gì với cái gì, không ghi giá trị đã đặt."
      />
      <CardBody>
        {entries.length === 0 ? (
          <p className="text-sm text-secondary">Chưa có việc nào được ghi.</p>
        ) : (
          <ul className="space-y-2 text-sm">
            {entries.map((entry, index) => (
              <li
                key={index}
                className="flex flex-wrap items-baseline gap-x-3 gap-y-1 border-b border-[var(--border-subtle)] pb-2 last:border-0"
              >
                <span className="font-mono text-xs text-muted">
                  {new Date(entry.createdAt).toLocaleString('vi-VN')}
                </span>
                <span className="font-medium">{entry.action}</span>
                {entry.targetType && <span className="text-secondary">{entry.targetType}</span>}
                {entry.metadataJson && (
                  <span className="w-full font-mono text-xs text-muted">{entry.metadataJson}</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  )
}
