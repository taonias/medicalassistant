import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { recordApiRequests } from '../../../test/recordApiRequests';
import { server } from '../../../test/server';
import { authApi } from './authApi';

describe('authentication API contract', () => {
  it('keeps every authentication method and URL unchanged', async () => {
    const { origins, requests } = recordApiRequests();

    await authApi.login({ userName: 'doctor', password: 'secret' });
    await authApi.getSession();
    await authApi.updateProfile({
      firstName: 'Test',
      lastName: 'Doctor',
      email: 'doctor@example.test',
    });
    await authApi.changePassword({ currentPassword: 'old', newPassword: 'new' });

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
      { method: 'POST', path: '/api/auth/login' },
      { method: 'GET', path: '/api/auth/session' },
      { method: 'PUT', path: '/api/auth/profile' },
      { method: 'PUT', path: '/api/auth/password' },
    ]);
  });

  it('sends login JSON without an Authorization header', async () => {
    let requestContract: { authorization: string | null; body: unknown } | undefined;
    server.use(
      http.post('https://backend.test/api/auth/login', async ({ request }) => {
        requestContract = {
          authorization: request.headers.get('Authorization'),
          body: await request.json(),
        };
        return HttpResponse.json({});
      }),
    );

    await authApi.login({ userName: 'doctor', password: 'secret' });

    expect(requestContract).toEqual({
      authorization: null,
      body: { userName: 'doctor', password: 'secret' },
    });
  });
});
