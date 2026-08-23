import { render, screen } from '@testing-library/react';
import { RouterProvider, type RouteObject } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';

import { useAuthStore } from '../features/auth/store/authStore';
import { AppProviders } from './providers';
import { router } from './router';

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
  useAuthStore.setState({ token: null, user: null, roles: [] });
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
    useAuthStore.setState({ token: null, user: null, roles: [] });
    await router.navigate('/not-a-supported-route');

    render(
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>,
    );

    expect(await screen.findByText('Sign in to continue')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });
});
