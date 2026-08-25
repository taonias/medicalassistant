import { sendRequest } from './core';

export type HttpRequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown;
  skipAuth?: boolean;
};

export async function httpClient<T>(
  path: string,
  options: HttpRequestOptions = {},
): Promise<T> {
  const { body, skipAuth, headers, ...rest } = options;

  const requestHeaders = new Headers(headers);
  if (body !== undefined) {
    requestHeaders.set('Content-Type', 'application/json');
  }

  const response = await sendRequest(
    path,
    {
      ...rest,
      headers: requestHeaders,
      body: body === undefined ? undefined : JSON.stringify(body),
    },
    skipAuth,
  );

  return parseJsonBody<T>(response);
}

/** Shared by json and multipart requests — both get a JSON-or-empty response back. */
export async function parseJsonBody<T>(response: Response): Promise<T> {
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
