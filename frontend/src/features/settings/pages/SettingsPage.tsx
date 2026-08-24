import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { ThemeToggle } from '../../theme';
import { useAuthStore } from '../../auth';
import { useChangePassword, useUpdateProfile } from '../hooks/useSettings';

const profileSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().email('Enter a valid email address'),
});

const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Current password is required'),
    newPassword: z.string().min(3, 'Password must be at least 3 characters'),
    confirmPassword: z.string().min(1, 'Please confirm your new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });

type ProfileForm = z.infer<typeof profileSchema>;
type PasswordForm = z.infer<typeof passwordSchema>;

export function SettingsPage() {
  const user = useAuthStore((state) => state.user);
  const updateProfile = useUpdateProfile();
  const changePassword = useChangePassword();
  const [profileSaved, setProfileSaved] = useState(false);
  const [passwordSaved, setPasswordSaved] = useState(false);

  const profileForm = useForm<ProfileForm>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      firstName: user?.firstName ?? '',
      lastName: user?.lastName ?? '',
      email: user?.email ?? '',
    },
  });

  const passwordForm = useForm<PasswordForm>({
    resolver: zodResolver(passwordSchema),
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  });

  useEffect(() => {
    if (!user) return;
    profileForm.reset({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
    });
  }, [user, profileForm]);

  return (
    <div className="page settings-page">
      <section className="panel settings-section">
        <h2>Appearance</h2>
        <p className="muted">Choose how Medical Assistant looks on this device.</p>
        <div className="settings-row">
          <div>
            <strong>Theme</strong>
            <p className="muted settings-row__hint">Light or dark mode</p>
          </div>
          <ThemeToggle />
        </div>
      </section>

      <section className="panel settings-section">
        <h2>Personal information</h2>
        <p className="muted">Update the name and email shown in your profile.</p>

        <form
          className="settings-form"
          onSubmit={profileForm.handleSubmit(async (values) => {
            setProfileSaved(false);
            await updateProfile.mutateAsync(values);
            setProfileSaved(true);
          })}
        >
          <label className="field">
            <span>Username</span>
            <input value={user?.userName ?? ''} disabled aria-readonly="true" />
          </label>

          <label className="field">
            <span>First name</span>
            <input {...profileForm.register('firstName')} />
            {profileForm.formState.errors.firstName ? (
              <span className="field__error">{profileForm.formState.errors.firstName.message}</span>
            ) : null}
          </label>

          <label className="field">
            <span>Last name</span>
            <input {...profileForm.register('lastName')} />
            {profileForm.formState.errors.lastName ? (
              <span className="field__error">{profileForm.formState.errors.lastName.message}</span>
            ) : null}
          </label>

          <label className="field">
            <span>Email</span>
            <input type="email" autoComplete="email" {...profileForm.register('email')} />
            {profileForm.formState.errors.email ? (
              <span className="field__error">{profileForm.formState.errors.email.message}</span>
            ) : null}
          </label>

          <div className="settings-form__actions">
            <button
              type="submit"
              className="button button--primary"
              disabled={updateProfile.isPending}
            >
              {updateProfile.isPending ? 'Saving...' : 'Save changes'}
            </button>
            {profileSaved ? <span className="settings-status">Profile updated.</span> : null}
          </div>

          {updateProfile.error ? (
            <ErrorMessage message={(updateProfile.error as Error).message} />
          ) : null}
        </form>
      </section>

      <section className="panel settings-section">
        <h2>Password</h2>
        <p className="muted">Change the password used to sign in.</p>

        <form
          className="settings-form"
          onSubmit={passwordForm.handleSubmit(async (values) => {
            setPasswordSaved(false);
            await changePassword.mutateAsync({
              currentPassword: values.currentPassword,
              newPassword: values.newPassword,
            });
            passwordForm.reset();
            setPasswordSaved(true);
          })}
        >
          <label className="field">
            <span>Current password</span>
            <input
              type="password"
              autoComplete="current-password"
              {...passwordForm.register('currentPassword')}
            />
            {passwordForm.formState.errors.currentPassword ? (
              <span className="field__error">
                {passwordForm.formState.errors.currentPassword.message}
              </span>
            ) : null}
          </label>

          <label className="field">
            <span>New password</span>
            <input
              type="password"
              autoComplete="new-password"
              {...passwordForm.register('newPassword')}
            />
            {passwordForm.formState.errors.newPassword ? (
              <span className="field__error">{passwordForm.formState.errors.newPassword.message}</span>
            ) : null}
          </label>

          <label className="field">
            <span>Confirm new password</span>
            <input
              type="password"
              autoComplete="new-password"
              {...passwordForm.register('confirmPassword')}
            />
            {passwordForm.formState.errors.confirmPassword ? (
              <span className="field__error">
                {passwordForm.formState.errors.confirmPassword.message}
              </span>
            ) : null}
          </label>

          <div className="settings-form__actions">
            <button
              type="submit"
              className="button button--primary"
              disabled={changePassword.isPending}
            >
              {changePassword.isPending ? 'Updating...' : 'Update password'}
            </button>
            {passwordSaved ? <span className="settings-status">Password updated.</span> : null}
          </div>

          {changePassword.error ? (
            <ErrorMessage message={(changePassword.error as Error).message} />
          ) : null}
        </form>
      </section>
    </div>
  );
}
