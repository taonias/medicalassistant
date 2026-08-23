import { render, screen } from '@testing-library/react';
import {
  createMemoryRouter,
  RouterProvider,
  type RouteObject,
} from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';

import { useAuthStore } from '../features/auth/store/authStore';
import { ProtectedRoute } from '../shared/components/ProtectedRoute';
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

  it('redirects an unauthenticated Doctor to login before rendering protected content', async () => {
    useAuthStore.setState({ token: null, user: null, roles: [] });
    const protectedRouter = createMemoryRouter(
      [
        {
          element: <ProtectedRoute />,
          children: [{ path: '/patients', element: <h1>Patients</h1> }],
        },
        { path: '/login', element: <h1>Sign in</h1> },
      ],
      { initialEntries: ['/patients'] },
    );

    render(<RouterProvider router={protectedRouter} />);

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Patients' })).not.toBeInTheDocument();
  });
});
