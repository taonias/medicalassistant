import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { server } from '../../test/server';
import { configureHttpAuth, httpClient, httpDownload } from './index';

const apiBaseUrl = 'https://backend.test/api';

describe('HTTP authentication contract', () => {
  let token: string | null;
  let unauthorizedCount: number;

  beforeEach(() => {
    token = 'doctor-token';
    unauthorizedCount = 0;
    configureHttpAuth({
      getToken: () => token,
      onUnauthorized: () => {
        unauthorizedCount += 1;
        token = null;
      },
    });
  });

  it('calls the unauthorized callback when an authenticated request returns 401', async () => {
    server.use(
      http.get(`${apiBaseUrl}/protected`, () =>
        HttpResponse.json({ detail: 'Session expired' }, { status: 401 }),
      ),
    );

    await expect(httpClient('/protected')).rejects.toEqual({
      message: 'Session expired',
      statusCode: 401,
    });
    expect(unauthorizedCount).toBe(1);
    expect(token).toBeNull();
  });

  it('does not call the unauthorized callback for a skip-auth request that returns 401', async () => {
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
    expect(unauthorizedCount).toBe(0);
    expect(token).toBe('doctor-token');
  });

  it('calls the unauthorized callback when a download returns 401 (R29 — was previously a silent gap)', async () => {
    server.use(
      http.get(`${apiBaseUrl}/downloads/report.pdf`, () =>
        HttpResponse.json({ detail: 'Session expired' }, { status: 401 }),
      ),
    );

    await expect(httpDownload('/downloads/report.pdf', 'report.pdf')).rejects.toEqual({
      message: 'Session expired',
      statusCode: 401,
    });
    expect(unauthorizedCount).toBe(1);
    expect(token).toBeNull();
  });
});
