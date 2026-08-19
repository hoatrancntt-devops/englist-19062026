import { lazy, Suspense } from 'react'
import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom'
import { useCurrentUser } from '@/features/auth/use-auth'
import { LearnerShell } from '@/components/layout/learner-shell'
import { LoginPage } from '@/features/auth/login-page'
import { RegisterPage } from '@/features/auth/register-page'
import { DashboardPage } from '@/features/dashboard/dashboard-page'
import { LandingPage } from '@/features/marketing/landing-page'
import { SkeletonCard } from '@/components/ui/feedback'
import { RouteErrorBoundary } from './route-error-boundary'
import {
  ListeningPage,
  SpeakingPage,
  ReadingPage,
  WritingPage,
  NotificationsPage,
  SettingsPage,
} from '@/features/learn/section-pages'
import { PlacementPage } from '@/features/placement/placement-page'
import { LessonPlayerPage } from '@/features/lesson/lesson-player-page'
import { ReviewPage } from '@/features/review/review-page'
import { ChallengePage } from '@/features/lesson/challenge-page'
import { RoadmapPage } from '@/features/learn/roadmap-page'
import { ChooseTrackPage } from '@/features/learn/choose-track-page'
import { RoleplayPage } from '@/features/roleplay/roleplay-page'

// Trang quản trị tách bundle riêng: học viên chiếm gần hết lưu lượng và
// không bao giờ cần tải mã của khu quản trị.
const AdminShell = lazy(() =>
  import('@/features/admin/admin-shell').then((m) => ({ default: m.AdminShell })),
)

/** Chặn khu vực cần đăng nhập. Đang tải thì hiện khung xương, không đá người dùng ra ngoài. */
function RequireAuth({ adminOnly = false }: { adminOnly?: boolean }) {
  const { data: user, isLoading } = useCurrentUser()

  if (isLoading) {
    return (
      <div className="mx-auto max-w-5xl space-y-4 p-6">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (adminOnly && !user.roles.some((role) => role === 'Admin' || role === 'SuperAdmin')) {
    return <Navigate to="/learn" replace />
  }

  return <Outlet />
}

/** Đã đăng nhập rồi thì không cho quay lại trang đăng nhập. */
function RedirectIfAuthenticated() {
  const { data: user, isLoading } = useCurrentUser()

  if (isLoading) {
    return null
  }

  return user ? <Navigate to="/learn" replace /> : <Outlet />
}

/**
 * Đường dẫn không tồn tại.
 *
 * Phải phân biệt theo trạng thái đăng nhập. Trước đây mọi đường dẫn lạ đều đá về "/",
 * mà "/" là trang tiếp thị luôn hiện nút "Đăng nhập / Tạo tài khoản" — người đang đăng nhập
 * gõ nhầm một URL sẽ tưởng mình vừa bị đăng xuất.
 */
function NotFoundRedirect() {
  const { data: user, isLoading } = useCurrentUser()

  if (isLoading) {
    return null
  }

  return <Navigate to={user ? '/learn' : '/'} replace />
}

export const router = createBrowserRouter([
  {
    errorElement: <RouteErrorBoundary />,
    children: [
      { path: '/', element: <LandingPage /> },

      {
        element: <RedirectIfAuthenticated />,
        children: [
          { path: '/login', element: <LoginPage /> },
          { path: '/register', element: <RegisterPage /> },
        ],
      },

      {
        element: <RequireAuth />,
        children: [
          {
            path: '/learn',
            element: <LearnerShell />,
            children: [
              { index: true, element: <DashboardPage /> },

              // Bốn kỹ năng, theo đúng thứ tự ưu tiên: nghe, nói, đọc, viết.
              { path: 'listening', element: <ListeningPage /> },
              { path: 'speaking', element: <SpeakingPage /> },
              { path: 'reading', element: <ReadingPage /> },
              { path: 'writing', element: <WritingPage /> },

              { path: 'roadmap', element: <RoadmapPage /> },
              { path: 'review', element: <ReviewPage /> },
              { path: 'roleplay', element: <RoleplayPage /> },
              { path: 'notifications', element: <NotificationsPage /> },
              { path: 'settings', element: <SettingsPage /> },

              // Chọn lĩnh vực và chế độ học. Vào được cả sau đăng nhập lần đầu lẫn từ Cài đặt.
              { path: 'chon-linh-vuc', element: <ChooseTrackPage /> },
              { path: 'lesson/:code', element: <LessonPlayerPage /> },
              { path: 'lesson/:code/challenge', element: <ChallengePage /> },

              // Đường dẫn lạ bên trong khu học viên thì về bảng điều khiển,
              // KHÔNG rơi ra trang tiếp thị.
              { path: '*', element: <Navigate to="/learn" replace /> },
            ],
          },

          // Bài xếp lớp chạy toàn màn hình, không có thanh bên: bỏ điều hướng đi
          // để người đang thi không bấm nhầm ra giữa chừng.
          {
            path: '/placement',
            element: (
              <div className="mx-auto max-w-3xl p-6">
                <PlacementPage />
              </div>
            ),
          },
        ],
      },

      {
        element: <RequireAuth adminOnly />,
        children: [
          {
            path: '/admin/*',
            element: (
              <Suspense fallback={<SkeletonCard />}>
                <AdminShell />
              </Suspense>
            ),
          },
        ],
      },

      { path: '*', element: <NotFoundRedirect /> },
    ],
  },
])
