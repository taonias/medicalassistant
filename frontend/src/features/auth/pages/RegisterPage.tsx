import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, Navigate } from 'react-router-dom';
import { AppBrand } from '../../../shared/components/AppBrand';
import { passwordPolicySchema } from '../../../shared/validation/password';
import { useRegister } from '../hooks/useAuth';
import { useAuthStore } from '../store/authStore';

const schema = z
  .object({
    firstName: z.string().min(1, 'First name is required'),
    lastName: z.string().min(1, 'Last name is required'),
    email: z.string().email('Enter a valid email address'),
    userName: z.string().min(3, 'Username must be at least 3 characters'),
    password: passwordPolicySchema,
    confirmPassword: z.string().min(1, 'Please confirm your password'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });

type FormValues = z.infer<typeof schema>;

export function RegisterPage() {
  const token = useAuthStore((state) => state.token);
  const registerMutation = useRegister();
  const [submitted, setSubmitted] = useState(false);

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

  return (
    <div className="auth-page">
      <div className="auth-card">
        <AppBrand variant="vertical" className="auth-card__brand" />
        <p className="auth-card__subtitle">Create a doctor account</p>

        {submitted ? (
          <>
            <p role="status">
              Your account was created and is waiting for an administrator to enable access. You can
              sign in after it is approved.
            </p>
            <p className="auth-card__footer">
              <Link to="/login">Sign in</Link>
            </p>
          </>
        ) : (
          <>
            <form
              onSubmit={handleSubmit(async (values) => {
                await registerMutation.mutateAsync({
                  firstName: values.firstName,
                  lastName: values.lastName,
                  email: values.email,
                  userName: values.userName,
                  password: values.password,
                });
                setSubmitted(true);
              })}
            >
              <label className="field">
                <span>First name</span>
                <input autoComplete="given-name" {...register('firstName')} />
                {errors.firstName ? <span className="field__error">{errors.firstName.message}</span> : null}
              </label>

              <label className="field">
                <span>Last name</span>
                <input autoComplete="family-name" {...register('lastName')} />
                {errors.lastName ? <span className="field__error">{errors.lastName.message}</span> : null}
              </label>

              <label className="field">
                <span>Email</span>
                <input type="email" autoComplete="email" {...register('email')} />
                {errors.email ? <span className="field__error">{errors.email.message}</span> : null}
              </label>

              <label className="field">
                <span>Username</span>
                <input autoComplete="username" {...register('userName')} />
                {errors.userName ? <span className="field__error">{errors.userName.message}</span> : null}
              </label>

              <label className="field">
                <span>Password</span>
                <input type="password" autoComplete="new-password" {...register('password')} />
                {errors.password ? <span className="field__error">{errors.password.message}</span> : null}
              </label>

              <label className="field">
                <span>Confirm password</span>
                <input type="password" autoComplete="new-password" {...register('confirmPassword')} />
                {errors.confirmPassword ? (
                  <span className="field__error">{errors.confirmPassword.message}</span>
                ) : null}
              </label>

              {registerMutation.error ? (
                <p className="field__error" role="alert">
                  {'message' in registerMutation.error
                    ? String(registerMutation.error.message)
                    : 'Registration failed'}
                </p>
              ) : null}

              <button type="submit" className="button button--primary" disabled={registerMutation.isPending}>
                {registerMutation.isPending ? 'Creating account…' : 'Create account'}
              </button>
            </form>

            <p className="auth-card__footer">
              Already have an account? <Link to="/login">Sign in</Link>
            </p>
          </>
        )}
      </div>
    </div>
  );
}
