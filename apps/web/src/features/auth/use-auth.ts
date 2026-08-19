import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/lib/api-client'

export interface CurrentUser {
  id: string
  email: string
  displayName: string
  roles: string[]
}

export const currentUserKey = ['auth', 'me'] as const

/**
 * Trạng thái đăng nhập. Trả về `null` khi chưa đăng nhập chứ không ném lỗi —
 * chưa đăng nhập là trạng thái bình thường của trang công khai, không phải sự cố.
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: async (): Promise<CurrentUser | null> => {
      try {
        return await api.get<CurrentUser>('/api/v1/auth/me')
      } catch (error) {
        if (error instanceof ApiError && error.isUnauthorized) {
          return null
        }
        throw error
      }
    },
    // Phiên sống 30 ngày nên không cần hỏi lại server mỗi lần chuyển tab.
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: { email: string; password: string }) =>
      api.post<{ userId: string }>('/api/v1/auth/login', input),
    onSuccess: () => {
      // Nạp lại danh tính thay vì tự ghép: server là nguồn sự thật cho vai trò.
      void queryClient.invalidateQueries({ queryKey: currentUserKey })
    },
  })
}

export function useRegister() {
  return useMutation({
    mutationFn: (input: { email: string; password: string; displayName: string }) =>
      api.post<{ message: string }>('/api/v1/auth/register', input),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => api.post<{ message: string }>('/api/v1/auth/logout'),
    onSuccess: () => {
      // Xoá sạch cache: dữ liệu học của người vừa đăng xuất không được để lại
      // cho người đăng nhập kế tiếp trên cùng máy.
      queryClient.clear()
    },
  })
}
