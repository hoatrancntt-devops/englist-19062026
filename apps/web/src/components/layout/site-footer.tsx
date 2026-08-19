// Không có icon LinkedIn: lucide đã bỏ toàn bộ icon thương hiệu từ bản 1.x.
// Dùng Briefcase cho hồ sơ nghề nghiệp thay vì thêm một bộ icon nữa chỉ vì một hình.
import { Mail, Phone, Briefcase, MessageCircle, ExternalLink } from 'lucide-react'

/**
 * Footer dùng chung cho trang giới thiệu và các trang học.
 *
 * Thông tin lấy nguyên từ footer hoatranlab.io.vn để hai nơi không nói khác nhau. Đặt thành
 * component chứ không chép vào từng trang: số điện thoại và email mà nằm rải rác thì lần đổi
 * sau sẽ sót một chỗ, và chỗ sót đó là chỗ học viên bấm vào.
 */
export function SiteFooter() {
  return (
    <footer className="mt-10 border-t border-[var(--border-subtle)]">
      <div className="mx-auto max-w-5xl px-4 py-8 lg:px-8">
        <div className="grid gap-6 sm:grid-cols-2">
          <div>
            <p className="flex items-center gap-2 font-semibold">
              <span
                className="inline-flex size-7 items-center justify-center rounded-md bg-brand-600 text-xs font-bold text-white"
                aria-hidden
              >
                HT
              </span>
              <span>
                HoaTran<span className="text-[var(--color-warning-text)]">Lab</span>
              </span>
            </p>

            <p className="mt-2 max-w-sm text-sm text-secondary">
              Tự học và chia sẻ kiến thức CNTT, hướng tới mục tiêu trở thành chuyên gia hạ tầng
              và bảo mật.
            </p>

            <a
              href="https://hoatranlab.io.vn"
              target="_blank"
              rel="noopener noreferrer"
              className="mt-3 inline-flex items-center gap-1.5 text-sm text-brand-600 hover:underline dark:text-brand-300"
            >
              hoatranlab.io.vn
              <ExternalLink className="size-3.5" aria-hidden />
            </a>
          </div>

          <div>
            <h2 className="text-xs font-semibold uppercase tracking-wide text-muted">Kết nối</h2>

            <p className="mt-2 font-medium">Trần Văn Hòa</p>
            <p className="text-sm text-secondary">MCT · MS365 · System Expert</p>

            <ul className="mt-3 space-y-2 text-sm">
              <li>
                <a
                  href="tel:0917516878"
                  className="inline-flex items-center gap-2 hover:text-brand-600 dark:hover:text-brand-300"
                >
                  <Phone className="size-4 shrink-0 text-muted" aria-hidden />
                  0917 516 878
                </a>
              </li>

              <li>
                {/* rel="noopener" bắt buộc với target="_blank": thiếu nó thì trang được mở
                    đọc được window.opener và có thể điều hướng trang này đi nơi khác. */}
                <a
                  href="https://zalo.me/0917516878"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-2 hover:text-brand-600 dark:hover:text-brand-300"
                >
                  <MessageCircle className="size-4 shrink-0 text-muted" aria-hidden />
                  Zalo: 0917 516 878
                </a>
              </li>

              <li>
                <a
                  href="mailto:tech@hoatranlab.io.vn"
                  className="inline-flex items-center gap-2 break-all hover:text-brand-600 dark:hover:text-brand-300"
                >
                  <Mail className="size-4 shrink-0 text-muted" aria-hidden />
                  tech@hoatranlab.io.vn
                </a>
              </li>

              <li>
                <a
                  href="https://www.linkedin.com/in/hoatrancntt/"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-2 hover:text-brand-600 dark:hover:text-brand-300"
                >
                  <Briefcase className="size-4 shrink-0 text-muted" aria-hidden />
                  LinkedIn Profile
                </a>
              </li>
            </ul>
          </div>
        </div>

        <div className="mt-8 border-t border-[var(--border-subtle)] pt-4 text-xs text-muted">
          <p>© 2026 HoaTranLab.io.vn — Author: Trần Văn Hòa (MCT)</p>

          {/* Giữ lại câu này từ footer cũ của trang giới thiệu: nó nói nguồn gốc giáo trình,
              là thứ học viên cần biết và không có ở đâu khác trong app. */}
          <p className="mt-1">
            Nội dung tự biên soạn. Thang level bám CEFR Companion Volume 2020.
          </p>
        </div>
      </div>
    </footer>
  )
}
