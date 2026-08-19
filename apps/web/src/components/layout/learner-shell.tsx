import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  LayoutDashboard,
  Map,
  Headphones,
  MessagesSquare,
  PenLine,
  BookOpen,
  BookMarked,
  RotateCcw,
  Bell,
  Settings,
  LogOut,
  Menu,
  X,
  Sun,
  Moon,
  GraduationCap,
  Compass,
} from 'lucide-react'
import { cn } from '@/lib/cn'
import { SiteFooter } from './site-footer'
import { useTheme } from '@/providers/theme-provider'
import { useCurrentUser, useLogout } from '@/features/auth/use-auth'
import { Button } from '@/components/ui/button'

interface NavItem {
  to: string
  label: string
  icon: typeof LayoutDashboard
  /** Gợi nhớ ngắn hiển thị trên bản rộng. Giúp người mới biết mục đó để làm gì. */
  hint?: string
}

/** Thứ tự trên thanh bên khớp thứ tự ưu tiên kỹ năng: nghe, nói, đọc, viết. */
const NAV_ITEMS: NavItem[] = [
  { to: '/learn', label: 'Bảng điều khiển', icon: LayoutDashboard, hint: 'Hôm nay học gì' },

  // Đứng ngay dưới bảng điều khiển: đây là thứ quyết định mọi bài phía sau nói về chuyện gì,
  // nên phải thấy được từ đầu chứ không nằm lẫn trong Cài đặt.
  { to: '/learn/chon-linh-vuc', label: 'Lĩnh vực học', icon: Compass, hint: 'Đổi chủ đề và kỹ năng' },

  { to: '/learn/roadmap', label: 'Lộ trình', icon: Map, hint: 'Toàn bộ chặng đường' },
  // Ôn tập đứng trên bốn kỹ năng vì nó là việc nên làm TRƯỚC khi học bài mới:
  // câu đã tới hạn mà để trôi thì phần học trước đó mất dần.
  { to: '/learn/review', label: 'Ôn tập', icon: RotateCcw, hint: 'Câu sắp quên' },
  { to: '/learn/roleplay', label: 'Đóng vai', icon: MessagesSquare, hint: 'Tình huống nghề' },

  // Mạch truyện đứng cạnh Đóng vai chứ không lẫn vào bốn kỹ năng: nó không dạy kỹ năng nào,
  // nó là lý do để học tiếp.
  { to: '/learn/truyen', label: 'Mạch truyện', icon: BookMarked, hint: 'Sáu tháng đầu đi làm' },
  { to: '/learn/listening', label: 'Nghe', icon: Headphones },
  { to: '/learn/speaking', label: 'Nói', icon: MessagesSquare },
  { to: '/learn/reading', label: 'Đọc', icon: BookOpen },
  { to: '/learn/writing', label: 'Viết', icon: PenLine },
]

export function LearnerShell() {
  const [mobileOpen, setMobileOpen] = useState(false)
  const { resolved, toggle } = useTheme()
  const { data: user } = useCurrentUser()
  const logout = useLogout()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await logout.mutateAsync()
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-dvh bg-[var(--surface-base)]">
      {/* Bỏ qua điều hướng — bắt buộc cho người dùng bàn phím, ẩn tới khi được focus. */}
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-brand-600 focus:px-4 focus:py-2 focus:text-white"
      >
        Bỏ qua tới nội dung chính
      </a>

      <header className="sticky top-0 z-30 border-b border-[var(--border-subtle)] bg-[var(--surface-raised)]/85 backdrop-blur">
        <div className="flex h-14 items-center gap-3 px-4">
          <button
            type="button"
            onClick={() => setMobileOpen((open) => !open)}
            className="rounded-md p-2 hover:bg-[var(--surface-hover)] lg:hidden"
            aria-label={mobileOpen ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={mobileOpen}
          >
            {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>

          <NavLink to="/learn" className="flex items-center gap-2 font-semibold">
            <GraduationCap className="size-6 text-brand-600" aria-hidden />
            <span className="hidden sm:inline">English for IT</span>
          </NavLink>

          <div className="ml-auto flex items-center gap-1">
            <NavLink
              to="/learn/notifications"
              className="rounded-md p-2 hover:bg-[var(--surface-hover)]"
              aria-label="Thông báo"
            >
              <Bell className="size-5" />
            </NavLink>

            <button
              type="button"
              onClick={toggle}
              className="rounded-md p-2 hover:bg-[var(--surface-hover)]"
              aria-label={resolved === 'dark' ? 'Chuyển sang nền sáng' : 'Chuyển sang nền tối'}
            >
              {resolved === 'dark' ? <Sun className="size-5" /> : <Moon className="size-5" />}
            </button>

            <div className="mx-2 hidden text-sm sm:block">
              <span className="text-secondary">Xin chào, </span>
              <span className="font-medium">{user?.displayName ?? '...'}</span>
            </div>

            <Button variant="ghost" size="sm" onClick={handleLogout} loading={logout.isPending}>
              <LogOut className="size-4" aria-hidden />
              <span className="sr-only sm:not-sr-only">Đăng xuất</span>
            </Button>
          </div>
        </div>
      </header>

      <div className="flex">
        <Sidebar mobileOpen={mobileOpen} onNavigate={() => setMobileOpen(false)} />

        {/* Footer nằm TRONG cột nội dung, không phải dưới cả trang: đặt ngoài thì trên màn
            hình rộng nó chạy ngang qua dưới thanh bên và lệch khỏi mạch đọc. */}
        <div className="min-w-0 flex-1">
          <main id="main" className="px-4 py-6 lg:px-8">
            <div className="mx-auto max-w-5xl">
              <Outlet />
            </div>
          </main>

          <SiteFooter />
        </div>
      </div>
    </div>
  )
}

function Sidebar({ mobileOpen, onNavigate }: { mobileOpen: boolean; onNavigate: () => void }) {
  return (
    <nav
      aria-label="Điều hướng chính"
      className={cn(
        'w-60 shrink-0 border-r border-[var(--border-subtle)] bg-[var(--surface-raised)] p-3',
        // Trên màn hình nhỏ, thanh bên trượt ra như một lớp phủ.
        'fixed inset-y-14 left-0 z-20 overflow-y-auto transition-transform lg:static lg:inset-auto lg:translate-x-0',
        mobileOpen ? 'translate-x-0' : '-translate-x-full',
      )}
    >
      <ul className="space-y-0.5">
        {NAV_ITEMS.map((item) => (
          <li key={item.to}>
            <NavLink
              to={item.to}
              end={item.to === '/learn'}
              onClick={onNavigate}
              className={({ isActive }) =>
                cn(
                  'flex items-start gap-3 rounded-[var(--radius-control)] px-3 py-2 text-sm transition-colors',
                  isActive
                    ? 'bg-brand-50 font-medium text-brand-700 dark:bg-brand-900/40 dark:text-brand-200'
                    : 'text-[var(--text-secondary)] hover:bg-[var(--surface-hover)] hover:text-[var(--text-primary)]',
                )
              }
            >
              <item.icon className="mt-0.5 size-4.5 shrink-0" aria-hidden />
              <span className="min-w-0">
                <span className="block">{item.label}</span>
                {item.hint && <span className="block text-xs text-muted">{item.hint}</span>}
              </span>
            </NavLink>
          </li>
        ))}
      </ul>

      <hr className="my-3 border-[var(--border-subtle)]" />

      <NavLink
        to="/learn/settings"
        onClick={onNavigate}
        className="flex items-center gap-3 rounded-[var(--radius-control)] px-3 py-2 text-sm text-[var(--text-secondary)] hover:bg-[var(--surface-hover)]"
      >
        <Settings className="size-4.5" aria-hidden />
        Cài đặt
      </NavLink>
    </nav>
  )
}
