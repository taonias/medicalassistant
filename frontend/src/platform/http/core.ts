import { API_BASE_URL, notifyUnauthorized, resolveAuthToken } from './config';
import { parseError } from './errors';

/**
 * The one place every request passes through: base-URL prefix, the
 * Authorization header, and unified error handling (including the
 * unauthorized callback on 401) — shared by JSON, multipart, Blob, and
 * download requests alike, so none of them can drift from the others (R29).
 */
export async function sendRequest(
  path: string,
  init: RequestInit,
  skipAuth?: boolean,
): Promise<Response> {
  const headers = new Headers(init.headers);
  const token = resolveAuthToken(skipAuth);
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });

  if (!response.ok) {
    if (response.status === 401 && !skipAuth) {
      notifyUnauthorized();
    }
    throw await parseError(response);
  }

  return response;
}
