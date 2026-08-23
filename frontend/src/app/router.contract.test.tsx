import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { RouterProvider, type RouteObject } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';

import { useAuthStore } from '../features/auth/store/authStore';
import { server } from '../test/server';
import { AppProviders } from './providers';
import { router } from './router';

const authenticatedDoctor = {
  id: 'doctor-1',
  userName: 'doctor',
  email: 'doctor@example.test',
  emailConfirmed: true,
  firstName: 'Test',
  lastName: 'Doctor',
  token: 'doctor-token',
  roles: ['Doctor'],
};

function routePaths(routes: readonly RouteObject[], parentPath = ''): string[] {
  return routes.flatMap((route) => {
    const path = route.index
      ? parentPath || '/'
      : route.path?.startsWith('/')
        ? route.path
        : route.path
          ? `${parentPath}/${route.path}`.replace('//', '/')
          : parentPath;
    const ownPath = route.index || route.path ? [path] : [];
    const childPaths = route.children ? routePaths(route.children, path) : [];
    return [...ownPath, ...childPaths];
  });
}

afterEach(() => {
  useAuthStore.getState().logout();
});

describe('browser route contract', () => {
  it('keeps the supported route hierarchy discoverable in one manifest', () => {
    expect(routePaths(router.routes)).toEqual([
      '/login',
      '/',
      '/record',
      '/chat',
      '/settings',
      '/patients',
      '/patients/:patientId',
      '/patients/:patientId',
      '/patients/:patientId/history',
      '/patients/:patientId/structured-data',
      '/patients/:patientId/consultations/new',
      '/patients/:patientId/consultations/:consultationId',
      '/consultations/:consultationId',
      '/patients/:patientId/chat',
      '/*',
    ]);
  });

  it('renders the public login route with its existing Doctor-facing DOM', async () => {
    await router.navigate('/login');

    render(
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>,
    );

    expect(await screen.findByText('Sign in to continue')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Username' })).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('redirects an unauthenticated Doctor before rendering a protected route', async () => {
    useAuthStore.setState({ token: null, user: null, roles: [] });
    await router.navigate('/patients');

    render(
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>,
    );

    expect(await screen.findByText('Sign in to continue')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it('routes an unknown URL through the existing fallback', async () => {
    useAuthStore.getState().setAuth(authenticatedDoctor);
    server.use(
      http.get('https://backend.test/api/auth/session', () =>
        HttpResponse.json({
          id: authenticatedDoctor.id,
          userName: authenticatedDoctor.userName,
          email: authenticatedDoctor.email,
          emailConfirmed: authenticatedDoctor.emailConfirmed,
          firstName: authenticatedDoctor.firstName,
          lastName: authenticatedDoctor.lastName,
        }),
      ),
      http.get('https://backend.test/api/consultation/analytics', () =>
        HttpResponse.json(null),
      ),
      http.get('https://backend.test/api/consultation/drafts/unattached', () =>
        HttpResponse.json([]),
      ),
    );
    await router.navigate('/not-a-supported-route');

    render(
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>,
    );

    expect(await screen.findByText('No dashboard data yet')).toBeInTheDocument();
    expect(screen.getByLabelText('Application sidebar')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });
});
