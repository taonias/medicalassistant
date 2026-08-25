import { sendRequest } from './core';
import { parseJsonBody } from './json';

export interface HttpMultipartOptions {
  method?: string;
  skipAuth?: boolean;
}

/**
 * Uploads a FormData body, JSON response. No Content-Type header is set —
 * the browser fills in `multipart/form-data; boundary=...` itself, exactly
 * as it did when multipart bodies went through the plain JSON method (R29).
 */
export async function httpMultipart<T>(
  path: string,
  formData: FormData,
  options: HttpMultipartOptions = {},
): Promise<T> {
  const response = await sendRequest(
    path,
    { method: options.method ?? 'POST', body: formData },
    options.skipAuth,
  );

  return parseJsonBody<T>(response);
}
