import type { ApiError } from '../types/api';
import { useAuthStore } from '../../features/auth';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7037/api';

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown;
  skipAuth?: boolean;
};

let authToken: string | null = null;

export function setAuthToken(token: string | null) {
  authToken = token;
}

export function getAuthToken() {
  return authToken ?? useAuthStore.getState().token;
}

function resolveAuthToken(skipAuth?: boolean) {
  if (skipAuth) return null;
  return useAuthStore.getState().token ?? authToken;
}

async function parseError(response: Response): Promise<ApiError> {
  try {
    const data = (await response.json()) as { title?: string; detail?: string; message?: string };
    return {
      message: data.detail ?? data.message ?? data.title ?? response.statusText,
      statusCode: response.status,
    };
  } catch {
    return {
      message: response.statusText || 'Request failed',
      statusCode: response.status,
    };
  }
}

export async function httpClient<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const { body, skipAuth, headers, ...rest } = options;

  const requestHeaders = new Headers(headers);

  if (body !== undefined && !(body instanceof FormData)) {
    requestHeaders.set('Content-Type', 'application/json');
  }

  const token = resolveAuthToken(skipAuth);
  if (token) {
    requestHeaders.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: requestHeaders,
    body:
      body === undefined
        ? undefined
        : body instanceof FormData
          ? body
          : JSON.stringify(body),
  });

  if (!response.ok) {
    if (response.status === 401 && !skipAuth) {
      useAuthStore.getState().logout();
    }
    throw await parseError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get('Content-Type') ?? '';
  if (contentType.includes('application/json')) {
    const text = await response.text();
    if (!text.trim() || text.trim() === 'null') {
      return null as T;
    }
    return JSON.parse(text) as T;
  }

  // Empty successful responses (e.g. Ok(null) serialized oddly) are treated as null.
  const text = await response.text();
  if (!text.trim() || text.trim() === 'null') {
    return null as T;
  }

  try {
    return JSON.parse(text) as T;
  } catch {
    return text as T;
  }
}
