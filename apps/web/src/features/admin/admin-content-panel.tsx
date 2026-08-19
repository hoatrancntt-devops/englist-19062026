import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, Info, XCircle } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardBody, CardHeader } from '@/components/ui/card'
import { SkeletonCard } from '@/components/ui/feedback'
import { api, ApiError } from '@/lib/api-client'
import type { ContentGraph, GraphProblem, ReseedResult, SeedOutcome } from './admin-types'
import { ContentCatalogue } from './content-catalogue'
import { ContentGraphView } from './content-graph-view'

/**
 * Khu nội dung: nạp lại từ YAML, tra cứu danh mục, soi đồ thị tiên quyết.
 *
 * Không có chỗ nào sửa bài, cố ý. Nguồn sự thật là file YAML và seeder upsert theo mã bài;
 * sửa thẳng vào DB thì lần nạp lại kế tiếp ghi đè mất mà không báo gì.
 *
 * Đồ thị tự gọi API khi panel này mở, không gọi cùng lúc với ba nguồn kia trong shell:
 * nó nặng hơn hẳn mấy con số trạng thái, mà phần lớn lượt vào quản trị không mở tab này.
 */
export function AdminContentPanel() {
  const [result, setResult] = useState<ReseedResult | null>(null)
  const [running, setRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [graph, setGraph] = useState<ContentGraph | null>(null)
  const [graphError, setGraphError] = useState<string | null>(null)

  const loadGraph = useCallback(async () => {
    setGraphError(null)

    try {
      setGraph(await api.get<ContentGraph>('/api/v1/admin/content/graph'))
    } catch (caught) {
      setGraphError(caught instanceof ApiError ? caught.message : 'Không tải được đồ thị nội dung.')
    }
  }, [])

  useEffect(() => {
    void loadGraph()
  }, [loadGraph])

  const reseed = async () => {
    setRunning(true)
    setError(null)

    try {
      setResult(await api.post<ReseedResult>('/api/v1/admin/content/reseed'))

      // Nạp lại xong thì đồ thị cũ đã lỗi thời. Không tải lại thì màn bên dưới
      // vẫn hiện hình cũ và người vận hành tưởng thay đổi chưa vào.
      await loadGraph()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Không nạp lại được nội dung.')
    } finally {
      setRunning(false)
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader
          title="Nạp lại nội dung"
          description="Đọc lại toàn bộ file YAML trong thư mục nội dung. Bài không đổi thì bỏ qua, nên chạy lại nhiều lần vô hại."
        />
        <CardBody className="space-y-3">
          <Button onClick={reseed} loading={running}>
            Nạp lại từ YAML
          </Button>

          {error && <p className="text-sm text-[var(--color-danger)]">{error}</p>}

          {result && (
            <div className="space-y-3">
              <SeedReport title="Bài học" outcome={result.lessons} />
              <SeedReport title="Đề xếp lớp" outcome={result.placement} />
              <SeedReport title="Kịch bản đóng vai" outcome={result.roleplay} />
            </div>
          )}
        </CardBody>
      </Card>

      {graphError && (
        <Card>
          <CardBody>
            <p className="text-sm text-[var(--color-danger)]">{graphError}</p>
          </CardBody>
        </Card>
      )}

      {!graph && !graphError && <SkeletonCard />}

      {graph && (
        <>
          {graph.problems.length > 0 && (
            <Card>
              <CardHeader
                title="Hình dạng lộ trình"
                description="Những thứ chỉ thấy khi ghép cả kho bài lại. Cổng validate lúc nạp chỉ xét từng file một."
              />
              <CardBody>
                <ul className="space-y-2">
                  {graph.problems.map((problem, index) => (
                    <ProblemRow key={index} problem={problem} />
                  ))}
                </ul>
              </CardBody>
            </Card>
          )}

          <Card>
            <CardHeader
              title="Danh mục bài"
              description={`${graph.nodes.length} bài, ${graph.edges.length} cạnh tiên quyết, chuỗi dài nhất ${graph.maxDepth + 1} bậc.`}
            />
            <CardBody>
              <ContentCatalogue nodes={graph.nodes} />
            </CardBody>
          </Card>

          <Card>
            <CardHeader title="Đồ thị tiên quyết" />
            <CardBody>
              <ContentGraphView graph={graph} />
            </CardBody>
          </Card>
        </>
      )}
    </div>
  )
}

function ProblemRow({ problem }: { problem: GraphProblem }) {
  const Icon = problem.severity === 'error' ? XCircle : problem.severity === 'warning' ? AlertTriangle : Info

  const tone =
    problem.severity === 'error'
      ? 'text-[var(--color-danger)]'
      : problem.severity === 'warning'
        ? 'text-[var(--color-warning)]'
        : 'text-secondary'

  return (
    <li className="flex gap-2 text-sm">
      <Icon className={`mt-0.5 size-4 shrink-0 ${tone}`} aria-hidden />
      <span>
        <span className="font-mono text-xs text-muted">{problem.code}</span>{' '}
        {problem.lessonCode && <strong className="font-mono text-xs">{problem.lessonCode}</strong>}{' '}
        {problem.message}
      </span>
    </li>
  )
}

function SeedReport({ title, outcome }: { title: string; outcome: SeedOutcome }) {
  return (
    <div className="rounded-[var(--radius-control)] border border-[var(--border-subtle)] p-3">
      <p className="font-medium">{title}</p>

      <p className="mt-1 text-sm text-secondary">
        Thêm mới <strong>{outcome.inserted}</strong>, cập nhật <strong>{outcome.updated}</strong>,
        không đổi <strong>{outcome.unchanged}</strong>
      </p>

      {/* File hỏng phải hiện nguyên văn lý do. "Có lỗi xảy ra" không giúp ai sửa được file nào. */}
      {outcome.problems.length > 0 && (
        <ul className="mt-2 space-y-1 text-sm text-[var(--color-danger)]">
          {outcome.problems.map((problem, index) => (
            <li key={index} className="flex gap-2">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>{problem}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
