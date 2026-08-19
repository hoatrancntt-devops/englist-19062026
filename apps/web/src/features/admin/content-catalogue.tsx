import { useMemo, useState } from 'react'
import { ArrowDown, ArrowUp } from 'lucide-react'

import { cn } from '@/lib/cn'
import type { GraphNode } from './admin-types'

type SortKey = 'code' | 'depth' | 'gates' | 'items'

const COLUMNS: { key: SortKey; label: string; hint: string; numeric: boolean }[] = [
  { key: 'code', label: 'Mã bài', hint: 'Mã ổn định dùng ở mọi nơi ngoài DB', numeric: false },
  { key: 'depth', label: 'Bậc', hint: 'Số bài phải học xong trước, theo đường dài nhất', numeric: true },
  { key: 'gates', label: 'Chặn', hint: 'Số bài bị bài này chặn, tính cả gián tiếp', numeric: true },
  { key: 'items', label: 'Câu', hint: 'Số câu hỏi trong bài', numeric: true },
]

/**
 * Danh mục bài, tra cứu và sắp xếp.
 *
 * Hai cột đáng tiền là Bậc và Chặn: cả hai chỉ tính được khi nhìn cả đồ thị, nên mở từng
 * file YAML ra đọc thì không bao giờ thấy. Bậc cho biết học viên phải cày bao lâu mới tới
 * được bài này; Chặn cho biết sửa bài này thì ảnh hưởng tới bao nhiêu bài phía sau.
 */
export function ContentCatalogue({ nodes }: { nodes: GraphNode[] }) {
  const [query, setQuery] = useState('')
  const [track, setTrack] = useState('')
  const [sort, setSort] = useState<SortKey>('depth')
  const [descending, setDescending] = useState(false)

  const tracks = useMemo(
    () => [...new Set(nodes.map((n) => n.track))].sort((a, b) => a.localeCompare(b)),
    [nodes],
  )

  const rows = useMemo(() => {
    const needle = query.trim().toLowerCase()

    const filtered = nodes.filter(
      (n) =>
        (!track || n.track === track) &&
        (!needle ||
          n.code.toLowerCase().includes(needle) ||
          n.titleVi.toLowerCase().includes(needle)),
    )

    return filtered.sort((a, b) => {
      const order =
        sort === 'code' ? a.code.localeCompare(b.code) : (a[sort] as number) - (b[sort] as number)

      return descending ? -order : order
    })
  }, [nodes, query, track, sort, descending])

  const toggle = (key: SortKey) => {
    if (key === sort) {
      setDescending((previous) => !previous)
    } else {
      setSort(key)
      setDescending(key !== 'code')
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        <input
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Tìm theo mã hoặc tên bài"
          aria-label="Tìm bài"
          className="min-w-48 flex-1 rounded-[var(--radius-control)] border border-[var(--border-subtle)] bg-[var(--surface-raised)] px-3 py-2 text-sm"
        />

        <select
          value={track}
          onChange={(event) => setTrack(event.target.value)}
          aria-label="Lọc theo track"
          className="rounded-[var(--radius-control)] border border-[var(--border-subtle)] bg-[var(--surface-raised)] px-3 py-2 text-sm"
        >
          <option value="">Mọi track</option>
          {tracks.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
      </div>

      <p className="text-sm text-secondary">
        {rows.length}/{nodes.length} bài
      </p>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-[var(--border-subtle)] text-left">
              {COLUMNS.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  className={cn('py-2 font-medium', column.numeric && 'text-right')}
                >
                  <button
                    type="button"
                    onClick={() => toggle(column.key)}
                    title={column.hint}
                    className={cn(
                      'inline-flex items-center gap-1',
                      column.numeric && 'flex-row-reverse',
                    )}
                  >
                    {column.label}
                    {sort === column.key &&
                      (descending ? (
                        <ArrowDown className="size-3" aria-hidden />
                      ) : (
                        <ArrowUp className="size-3" aria-hidden />
                      ))}
                  </button>
                </th>
              ))}
              <th scope="col" className="py-2 font-medium">
                Tên bài
              </th>
            </tr>
          </thead>

          <tbody>
            {rows.map((node) => (
              <tr key={node.code} className="border-b border-[var(--border-subtle)] last:border-0">
                <td className="py-2 font-mono text-xs">
                  {node.code}
                  {node.status !== 'Published' && (
                    <span className="ml-2 rounded bg-[var(--surface-sunken)] px-1.5 py-0.5 text-[10px] text-[var(--color-warning)]">
                      {node.status}
                    </span>
                  )}
                </td>
                <td className="py-2 text-right tabular-nums">{node.depth}</td>
                <td className="py-2 text-right tabular-nums">{node.gates}</td>
                {/* Bài không có câu nào là hỏng âm thầm — học viên mở ra thấy màn trống. */}
                <td
                  className={cn(
                    'py-2 text-right tabular-nums',
                    node.items === 0 && 'text-[var(--color-danger)]',
                  )}
                >
                  {node.items}
                </td>
                <td className="py-2">{node.titleVi}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {rows.length === 0 && (
          <p className="py-4 text-sm text-secondary">Không có bài nào khớp bộ lọc.</p>
        )}
      </div>
    </div>
  )
}
