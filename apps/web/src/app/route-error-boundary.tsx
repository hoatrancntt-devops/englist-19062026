import { useRouteError, isRouteErrorResponse, Link } from 'react-router-dom'
import { AlertTriangle } from 'lucide-react'
import { ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'

/**
 * Lưới an toàn cuối cùng. Hiển thị mã tương quan khi có, để người dùng đọc cho quản trị
 * và quản trị tìm được đúng dòng log — thay vì "app bị lỗi" rồi không ai tra được gì.
 */
export function RouteErrorBoundary() {
  const error = useRouteError()

  let title = 'Có lỗi xảy ra'
  let description = 'Thử tải lại trang. Nếu vẫn lỗi, báo cho quản trị kèm mã bên dưới.'
  let correlationId: string | undefined

  if (isRouteErrorResponse(error)) {
    title = error.status === 404 ? 'Không tìm thấy trang' : `Lỗi ${error.status}`
    description = error.status === 404 ? 'Đường dẫn này không tồn tại hoặc đã đổi.' : description
  } else if (error instanceof ApiError) {
    title = error.isRateLimited ? 'Bạn thao tác hơi nhanh' : title
    description = error.message
    correlationId = error.correlationId
  }

  return (
    <div className="flex min-h-dvh items-center justify-center px-4 py-12">
      <Card className="w-full max-w-md p-6 text-center">
        <AlertTriangle className="mx-auto size-10 text-[var(--color-warning)]" aria-hidden />
        <h1 className="mt-3 font-semibold">{title}</h1>
        <p className="mt-1.5 text-sm text-secondary">{description}</p>

        {correlationId && (
          <p className="mt-3 rounded-[var(--radius-control)] bg-[var(--surface-sunken)] px-3 py-2 font-mono text-xs text-muted">
            {correlationId}
          </p>
        )}

        <div className="mt-5 flex justify-center gap-2">
          <Button variant="secondary" onClick={() => window.location.reload()}>
            Tải lại
          </Button>
          <Link to="/learn">
            <Button>Về bảng điều khiển</Button>
          </Link>
        </div>
      </Card>
    </div>
  )
}
