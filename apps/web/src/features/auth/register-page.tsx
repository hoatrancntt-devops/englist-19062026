import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, useNavigate } from 'react-router-dom'
import { GraduationCap, CheckCircle2 } from 'lucide-react'
import { useRegister } from './use-auth'
import { ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { TextField } from '@/components/ui/text-field'

// Ngưỡng 10 ký tự khớp với AuthOptions.MinPasswordLength ở backend.
// Lệch hai con số này nghĩa là người dùng bị báo lỗi sau khi bấm gửi, không phải trước.
const schema = z.object({
  displayName: z.string().min(2, 'Nhập tên hiển thị').max(120),
  email: z.string().min(1, 'Nhập email').email('Email không đúng định dạng'),
  password: z.string().min(10, 'Mật khẩu phải dài ít nhất 10 ký tự').max(256),
})

type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const navigate = useNavigate()
  const registerMutation = useRegister()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = handleSubmit(async (values) => {
    await registerMutation.mutateAsync(values)
  })

  const serverMessage =
    registerMutation.error instanceof ApiError ? registerMutation.error.message : null

  if (registerMutation.isSuccess) {
    return (
      <div className="flex min-h-dvh items-center justify-center px-4 py-12">
        <Card className="w-full max-w-sm p-6 text-center">
          <CheckCircle2 className="mx-auto size-10 text-[var(--color-success)]" aria-hidden />
          <h1 className="mt-3 font-semibold">Xong</h1>
          {/* Thông báo trung tính: máy chủ không nói email đã tồn tại hay chưa,
              nên client cũng không được suy ra. */}
          <p className="mt-1.5 text-sm text-secondary">{registerMutation.data.message}</p>
          <Button className="mt-5 w-full" onClick={() => navigate('/login')}>
            Tới trang đăng nhập
          </Button>
        </Card>
      </div>
    )
  }

  return (
    <div className="flex min-h-dvh items-center justify-center bg-[var(--surface-base)] px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex flex-col items-center text-center">
          <GraduationCap className="size-10 text-brand-600" aria-hidden />
          <h1 className="mt-3 text-xl font-semibold">Tạo tài khoản</h1>
          <p className="mt-1 text-sm text-secondary">Miễn phí, không cần thẻ</p>
        </div>

        <Card className="p-6">
          <form onSubmit={onSubmit} className="space-y-4" noValidate>
            <TextField
              label="Tên hiển thị"
              autoComplete="name"
              autoFocus
              error={errors.displayName?.message}
              {...register('displayName')}
            />

            <TextField
              label="Email"
              type="email"
              autoComplete="email"
              error={errors.email?.message}
              {...register('email')}
            />

            <TextField
              label="Mật khẩu"
              type="password"
              autoComplete="new-password"
              hint="Tối thiểu 10 ký tự. Câu dài dễ nhớ mà khó đoán hơn ký tự đặc biệt."
              error={errors.password?.message}
              {...register('password')}
            />

            {serverMessage && (
              <p
                role="alert"
                className="rounded-[var(--radius-control)] bg-[color-mix(in_oklch,var(--color-danger)_12%,transparent)] px-3 py-2 text-sm text-[var(--color-danger)]"
              >
                {serverMessage}
              </p>
            )}

            <Button type="submit" className="w-full" loading={registerMutation.isPending}>
              Đăng ký
            </Button>
          </form>
        </Card>

        <p className="mt-4 text-center text-sm text-secondary">
          Đã có tài khoản?{' '}
          <Link to="/login" className="font-medium text-brand-600 hover:underline">
            Đăng nhập
          </Link>
        </p>
      </div>
    </div>
  )
}
