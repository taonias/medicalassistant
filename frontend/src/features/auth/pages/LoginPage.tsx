import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { AppBrand } from '../../../shared/components/AppBrand';
import { useLogin } from '../hooks/useAuth';
import { useAuthStore } from '../store/authStore';

const schema = z.object({
  userName: z.string().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
});

type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const token = useAuthStore((state) => state.token);
  const navigate = useNavigate();
  const location = useLocation();
  const login = useLogin();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  if (token) {
    return <Navigate to="/" replace />;
  }

  const from = (location.state as { from?: string } | null)?.from ?? '/';

  return (
    <div className="auth-page">
      <div className="auth-card">
        <AppBrand variant="vertical" className="auth-card__brand" />
        <p className="auth-card__subtitle">Sign in to continue</p>

        <form
          onSubmit={handleSubmit(async (values) => {
            await login.mutateAsync(values);
            navigate(from, { replace: true });
          })}
        >
          <label className="field">
            <span>Username</span>
            <input autoComplete="username" {...register('userName')} />
            {errors.userName ? <span className="field__error">{errors.userName.message}</span> : null}
          </label>

          <label className="field">
            <span>Password</span>
            <input type="password" autoComplete="current-password" {...register('password')} />
            {errors.password ? <span className="field__error">{errors.password.message}</span> : null}
          </label>

          {login.error ? (
            <p className="field__error" role="alert">
              {'message' in login.error ? String(login.error.message) : 'Login failed'}
            </p>
          ) : null}

          <button type="submit" className="button button--primary" disabled={login.isPending}>
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
}
