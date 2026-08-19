import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, useNavigate } from 'react-router-dom'
import { GraduationCap } from 'lucide-react'
import { useLogin } from './use-auth'
import { ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { TextField } from '@/components/ui/text-field'

const schema = z.object({
  email: z.string().min(1, 'Nhập email').email('Email không đúng định dạng'),
  password: z.string().min(1, 'Nhập mật khẩu'),
})

type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = handleSubmit(async (values) => {
    await login.mutateAsync(values)
    navigate('/learn', { replace: true })
  })

  // Máy chủ cố tình trả cùng một thông báo cho email lạ và sai mật khẩu.
  // Client hiển thị nguyên văn, không đoán thêm.
  const serverMessage =
    login.error instanceof ApiError
      ? login.error.message
      : login.error
        ? 'Không kết nối được máy chủ.'
        : null

  return (
    <div className="flex min-h-dvh items-center justify-center bg-[var(--surface-base)] px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex flex-col items-center text-center">
          <GraduationCap className="size-10 text-brand-600" aria-hidden />
          <h1 className="mt-3 text-xl font-semibold">Đăng nhập</h1>
          <p className="mt-1 text-sm text-secondary">Tiếng Anh cho kỹ sư IT, Cloud và AI</p>
        </div>

        <Card className="p-6">
          <form onSubmit={onSubmit} className="space-y-4" noValidate>
            <TextField
              label="Email"
              type="email"
              autoComplete="email"
              autoFocus
              error={errors.email?.message}
              {...register('email')}
            />

            <TextField
              label="Mật khẩu"
              type="password"
              autoComplete="current-password"
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

            <Button type="submit" className="w-full" loading={login.isPending}>
              Đăng nhập
            </Button>
          </form>
        </Card>

        <p className="mt-4 text-center text-sm text-secondary">
          Chưa có tài khoản?{' '}
          <Link to="/register" className="font-medium text-brand-600 hover:underline">
            Đăng ký
          </Link>
        </p>
      </div>
    </div>
  )
}
