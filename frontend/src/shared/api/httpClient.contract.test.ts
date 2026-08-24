import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { useAuthStore } from '../../features/auth';
import { server } from '../../test/server';
import { httpClient, setAuthToken } from './httpClient';

const apiBaseUrl = 'https://backend.test/api';
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

beforeEach(() => {
  setAuthToken(null);
  useAuthStore.setState({ token: null, user: null, roles: [] });
});

describe('HTTP authentication contract', () => {
  it('logs the Doctor out when an authenticated request returns 401', async () => {
    useAuthStore.getState().setAuth(authenticatedDoctor);
    server.use(
      http.get(`${apiBaseUrl}/protected`, () =>
        HttpResponse.json({ detail: 'Session expired' }, { status: 401 }),
      ),
    );

    await expect(httpClient('/protected')).rejects.toEqual({
      message: 'Session expired',
      statusCode: 401,
    });
    expect(useAuthStore.getState()).toMatchObject({ token: null, user: null, roles: [] });
  });

  it('does not clear the current session when a skip-auth request returns 401', async () => {
    useAuthStore.getState().setAuth(authenticatedDoctor);
    let authorization: string | null | undefined;
    server.use(
      http.get(`${apiBaseUrl}/public`, ({ request }) => {
        authorization = request.headers.get('Authorization');
        return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 });
      }),
    );

    await expect(httpClient('/public', { skipAuth: true })).rejects.toMatchObject({
      statusCode: 401,
    });
    expect(authorization).toBeNull();
    expect(useAuthStore.getState().token).toBe('doctor-token');
  });

  it('persists authentication under the existing browser storage key', () => {
    useAuthStore.getState().setAuth(authenticatedDoctor);

    expect(JSON.parse(localStorage.getItem('medical-assistant-auth') ?? '{}')).toMatchObject({
      state: {
        token: 'doctor-token',
        roles: ['Doctor'],
      },
    });
  });
});
