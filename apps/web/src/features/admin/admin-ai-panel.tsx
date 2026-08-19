import { useState } from 'react'

import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { Badge, ProgressBar } from '@/components/ui/feedback'
import { TextField } from '@/components/ui/text-field'
import { api, ApiError } from '@/lib/api-client'
import type { AiStatus } from './admin-types'

interface AiTestResult {
  text: string
  fromCache: boolean
  fromFallback: boolean
  provider: string | null
  model: string | null
}

const MODE_LABEL: Record<string, string> = {
  Normal: 'Bình thường',
  Degraded: 'Hạ cấp',
  CacheOnly: 'Chỉ còn cache',
}

const MODE_EXPLAIN: Record<string, string> = {
  Normal: 'Dưới 70% ngân sách. Mọi tác vụ chạy đúng tầng đã đặt.',
  Degraded: 'Từ 70% ngân sách. Tác vụ T2 hạ xuống T1, cache giữ lâu gấp đôi.',
  CacheOnly: 'Từ 90% ngân sách. Không gọi nhà cung cấp nữa, chỉ dùng cache và câu dự phòng.',
}

/**
 * Trạng thái AI.
 *
 * Điều màn này phải làm rõ với người vận hành: <b>app không phụ thuộc vào AI</b>. Không cấu
 * hình nhà cung cấp nào, hay chạm trần ngân sách, đều là trạng thái hợp lệ — phần học vẫn
 * chạy đủ bằng luật, chỉ là câu chữ kém trau chuốt hơn.
 */
export function AdminAiPanel({ status }: { status: AiStatus }) {
  const [prompt, setPrompt] = useState('Explain what a pull request is, in one sentence.')
  const [result, setResult] = useState<AiTestResult | null>(null)
  const [testing, setTesting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const usedRatio = status.monthlyCapUsd > 0 ? status.spentThisMonthUsd / status.monthlyCapUsd : 0

  const test = async () => {
    setTesting(true)
    setError(null)

    try {
      setResult(await api.post<AiTestResult>('/api/v1/admin/ai/test', { prompt }))
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Không gọi được AI.')
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Ngân sách tháng này"
          description={MODE_EXPLAIN[status.budgetMode] ?? ''}
          action={<Badge>{MODE_LABEL[status.budgetMode] ?? status.budgetMode}</Badge>}
        />
        <CardBody className="space-y-3">
          {status.monthlyCapUsd > 0 ? (
            <>
              <ProgressBar value={Math.min(100, Math.round(usedRatio * 100))} />
              <p className="text-sm text-secondary">
                Đã dùng <strong>{status.spentThisMonthUsd.toFixed(4)} USD</strong> trên trần{' '}
                <strong>{status.monthlyCapUsd} USD</strong>
              </p>
            </>
          ) : (
            <p className="text-sm text-secondary">
              Chưa đặt trần ngân sách, nên không có giới hạn. Đã dùng{' '}
              <strong>{status.spentThisMonthUsd.toFixed(4)} USD</strong> tháng này.
            </p>
          )}

          <p className="text-sm text-secondary">
            {status.callsThisMonth} lượt gọi, trong đó {status.cacheHitsThisMonth} lượt lấy từ
            cache. Hiện có {status.cacheEntries} mục trong cache.
          </p>
        </CardBody>
      </Card>

      <Card>
        <CardHeader
          title="Nhà cung cấp"
          description="Gọi lần lượt theo thứ tự này. Nhà nào lỗi thì thử nhà kế tiếp, hết cả thì dùng câu dự phòng."
        />
        <CardBody>
          {status.providers.length === 0 ? (
            <p className="text-sm text-secondary">
              Chưa cấu hình nhà cung cấp nào. Đây là trạng thái hợp lệ — toàn bộ phần học vẫn
              chạy bằng luật, chỉ là câu nhận xét kém trau chuốt hơn.
            </p>
          ) : (
            <ul className="space-y-2 text-sm">
              {status.providers.map((provider) => (
                <li
                  key={provider.provider}
                  className="flex flex-wrap items-center gap-2 rounded-[var(--radius-control)] border border-[var(--border-subtle)] p-2"
                >
                  <span className="font-medium">{provider.provider}</span>
                  <Badge>{provider.enabled ? 'đang bật' : 'đang tắt'}</Badge>
                  {/* Chỉ hiện CÓ hay KHÔNG có khoá. Khoá không bao giờ rời khỏi máy chủ. */}
                  <Badge>{provider.hasKey ? 'đã có khoá' : 'chưa có khoá'}</Badge>
                  {provider.baseUrl && <span className="text-muted">{provider.baseUrl}</span>}
                </li>
              ))}
            </ul>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="Thử một lời gọi" description="Gọi thật qua đúng đường mà phần học dùng, kể cả cache và ngân sách." />
        <CardBody className="space-y-3">
          <TextField
            label="Câu nhắc"
            value={prompt}
            onChange={(event) => setPrompt(event.target.value)}
          />

          <Button variant="secondary" onClick={test} loading={testing}>
            Gọi thử
          </Button>

          {error && <p className="text-sm text-[var(--color-danger)]">{error}</p>}

          {result && (
            <div className="space-y-2 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] p-3 text-sm">
              <div className="flex flex-wrap gap-2">
                {result.fromCache && <Badge>từ cache</Badge>}
                {result.fromFallback && <Badge>câu dự phòng</Badge>}
                {result.provider && <Badge>{result.provider}</Badge>}
                {result.model && <Badge>{result.model}</Badge>}
              </div>
              <p className="whitespace-pre-wrap">{result.text}</p>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  )
}
