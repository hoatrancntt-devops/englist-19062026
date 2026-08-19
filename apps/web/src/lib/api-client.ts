/**
 * Lớp gọi API duy nhất của ứng dụng.
 *
 * Ba việc nó lo, để không component nào phải nhớ:
 *  1. Gửi cookie phiên (credentials: 'include').
 *  2. Đọc cookie CSRF và đặt vào header cho mọi request làm thay đổi dữ liệu.
 *  3. Chuẩn hoá lỗi thành ApiError để tầng UI chỉ xử lý một hình dạng.
 */

export class ApiError extends Error {
  // Khai báo trường tường minh thay vì dùng parameter property: tsconfig bật
  // erasableSyntaxOnly, nên cú pháp chỉ-TypeScript như `constructor(readonly x)` bị cấm.
  readonly status: number
  readonly code: string
  readonly correlationId?: string

  constructor(status: number, code: string, message: string, correlationId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.correlationId = correlationId
  }

  /** Chưa đăng nhập hoặc phiên hết hạn — tầng router dùng để đẩy về trang đăng nhập. */
  get isUnauthorized(): boolean {
    return this.status === 401
  }

  /** Bị giới hạn tần suất. UI nên bảo người dùng chờ chứ không nên tự thử lại. */
  get isRateLimited(): boolean {
    return this.status === 429
  }
}

const CSRF_COOKIE = 'efit_csrf'
const CSRF_HEADER = 'X-CSRF-Token'
const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

function readCookie(name: string): string | null {
  // Cookie CSRF cố ý không HttpOnly để đọc được ở đây. Cookie phiên thì không đọc được,
  // và đó chính là điều làm cho double-submit có tác dụng.
  const match = document.cookie.match(new RegExp(`(^|;\\s*)${name}=([^;]*)`))
  return match ? decodeURIComponent(match[2]) : null
}

interface RequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase()

  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  // FormData phải để trình duyệt tự đặt Content-Type: header multipart cần kèm chuỗi
  // boundary mà chỉ trình duyệt biết. Đặt tay là hỏng request.
  const isFormData = options.body instanceof FormData

  if (options.body !== undefined && !isFormData) {
    headers.set('Content-Type', 'application/json')
  }

  if (MUTATING_METHODS.has(method)) {
    const csrf = readCookie(CSRF_COOKIE)
    if (csrf) {
      headers.set(CSRF_HEADER, csrf)
    }
  }

  const response = await fetch(path, {
    ...options,
    method,
    headers,
    // Bắt buộc: không có dòng này thì cookie phiên không được gửi kèm.
    credentials: 'include',
    body:
      options.body === undefined
        ? undefined
        : isFormData
          ? (options.body as FormData)
          : JSON.stringify(options.body),
  })

  const correlationId = response.headers.get('X-Correlation-Id') ?? undefined

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('Content-Type') ?? ''
  const isJson = contentType.includes('application/json')
  const payload = isJson ? await response.json() : await response.text()

  if (!response.ok) {
    const code = isJson && payload?.error ? String(payload.error) : `http_${response.status}`
    const message =
      isJson && payload?.message
        ? String(payload.message)
        : 'Có lỗi xảy ra. Thử lại sau ít phút.'

    throw new ApiError(response.status, code, message, correlationId)
  }

  return payload as T
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) => request<T>(path, { ...options, method: 'GET' }),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'POST', body }),
  /** Gửi file kèm dữ liệu. Dùng cho ghi âm — JSON không chở được nhị phân. */
  postForm: <T>(path: string, form: FormData, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'POST', body: form }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'PUT', body }),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'PATCH', body }),
  delete: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'DELETE' }),
}
