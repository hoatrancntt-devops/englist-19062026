import { useMemo, useState } from 'react'

import { cn } from '@/lib/cn'
import type { ContentGraph, GraphNode } from './admin-types'

/** Mỗi bậc là một hàng ngang. Đủ chỗ cho mã bài và vài con số. */
const ROW_HEIGHT = 34
const NODE_WIDTH = 104
const COLUMN_GAP = 18

/**
 * Đồ thị tiên quyết, xếp theo bậc.
 *
 * Xếp DỌC chứ không ngang, vì đồ thị thật gần như một chuỗi thẳng 43 bậc: xếp ngang thì
 * người xem phải cuộn ngang một quãng dài trên màn hình vốn rộng hơn là cao, còn xếp dọc
 * thì cuộn theo đúng hướng người ta vẫn cuộn.
 *
 * Vẽ tay bằng SVG chứ không kéo thư viện đồ thị: 58 nút, một phép xếp bậc, không kéo thả,
 * không zoom. Thư viện nhỏ nhất cũng nặng hơn cả file này.
 */
export function ContentGraphView({ graph }: { graph: ContentGraph }) {
  const [selected, setSelected] = useState<string | null>(null)

  const layout = useMemo(() => {
    const byDepth = new Map<number, GraphNode[]>()

    for (const node of graph.nodes) {
      const row = byDepth.get(node.depth) ?? []
      row.push(node)
      byDepth.set(node.depth, row)
    }

    const position = new Map<string, { x: number; y: number }>()
    let widest = 1

    for (const [depth, row] of byDepth) {
      // Sắp trong hàng theo mã bài để lần dựng nào cũng cho ra cùng một hình.
      // Thứ tự đến từ API là thứ tự track, nên không ổn định khi nội dung đổi.
      row.sort((a, b) => a.code.localeCompare(b.code))
      widest = Math.max(widest, row.length)

      row.forEach((node, index) => {
        position.set(node.code, {
          x: index * (NODE_WIDTH + COLUMN_GAP) + NODE_WIDTH / 2,
          y: depth * ROW_HEIGHT + ROW_HEIGHT / 2,
        })
      })
    }

    return {
      position,
      width: widest * (NODE_WIDTH + COLUMN_GAP),
      height: (graph.maxDepth + 1) * ROW_HEIGHT,
    }
  }, [graph])

  // Chọn một bài thì chỉ tô những cạnh chạm tới nó. Vẽ hết 89 cạnh cùng độ đậm
  // thì phần giữa đồ thị thành một mảng đặc không đọc được gì.
  const touching = useMemo(() => {
    if (!selected) return null

    const codes = new Set<string>([selected])

    for (const edge of graph.edges) {
      if (edge.from === selected) codes.add(edge.to)
      if (edge.to === selected) codes.add(edge.from)
    }

    return codes
  }, [graph.edges, selected])

  return (
    <div className="space-y-2">
      <p className="text-sm text-secondary">
        Xếp theo bậc: bài ở hàng trên phải học xong trước bài hàng dưới. Bấm một bài để chỉ hiện
        các cạnh chạm tới nó.
        {selected && (
          <button
            type="button"
            onClick={() => setSelected(null)}
            className="ml-2 underline underline-offset-2"
          >
            Bỏ chọn
          </button>
        )}
      </p>

      <div className="overflow-auto rounded-[var(--radius-control)] border border-[var(--border-subtle)] p-3">
        <svg
          width={layout.width}
          height={layout.height}
          viewBox={`0 0 ${layout.width} ${layout.height}`}
          role="img"
          aria-label={`Đồ thị tiên quyết: ${graph.nodes.length} bài, ${graph.edges.length} cạnh, sâu nhất ${graph.maxDepth + 1} bậc`}
        >
          {graph.edges.map((edge, index) => {
            const from = layout.position.get(edge.from)
            const to = layout.position.get(edge.to)
            if (!from || !to) return null

            const lit = !touching || (touching.has(edge.from) && touching.has(edge.to))

            return (
              <line
                key={index}
                x1={from.x}
                y1={from.y + 9}
                x2={to.x}
                y2={to.y - 9}
                stroke="currentColor"
                strokeWidth={lit ? 1.4 : 0.6}
                // Cạnh mềm không khoá bài nên vẽ đứt nét, phân biệt được ngay với cạnh cứng.
                strokeDasharray={edge.kind === 'Soft' ? '3 3' : undefined}
                className={lit ? 'text-[var(--text-secondary)]' : 'text-[var(--border-subtle)]'}
              />
            )
          })}

          {graph.nodes.map((node) => {
            const at = layout.position.get(node.code)
            if (!at) return null

            const lit = !touching || touching.has(node.code)

            return (
              <g
                key={node.code}
                transform={`translate(${at.x - NODE_WIDTH / 2}, ${at.y - 9})`}
                onClick={() => setSelected(node.code === selected ? null : node.code)}
                className="cursor-pointer"
              >
                <title>
                  {`${node.code} — ${node.titleVi}\nBậc ${node.depth}, chặn ${node.gates} bài, ${node.items} câu`}
                </title>

                <rect
                  width={NODE_WIDTH}
                  height={18}
                  rx={4}
                  className={cn(
                    node.status !== 'Published'
                      ? 'fill-[var(--surface-sunken)] stroke-[var(--color-warning)]'
                      : node.isCheckpoint
                        ? 'fill-[var(--surface-raised)] stroke-[var(--color-brand-500)]'
                        : 'fill-[var(--surface-raised)] stroke-[var(--border-subtle)]',
                  )}
                  strokeWidth={1}
                  opacity={lit ? 1 : 0.35}
                />

                <text
                  x={NODE_WIDTH / 2}
                  y={13}
                  textAnchor="middle"
                  className="fill-[var(--text-primary)] text-[11px]"
                  opacity={lit ? 1 : 0.35}
                >
                  {node.code}
                </text>
              </g>
            )
          })}
        </svg>
      </div>
    </div>
  )
}
