import { API_BASE_URL, notifyUnauthorized, resolveAuthToken } from './config';
import type { ApiError } from '../../shared/types/api';

export interface HttpMultipartProgressOptions {
  method?: string;
  skipAuth?: boolean;
  /** Fires with 0..1 as the request body is sent. Fetch has no cross-browser way to
   *  observe upload progress, so this goes through XMLHttpRequest instead. */
  onProgress?: (fraction: number) => void;
}

/** Same contract as httpMultipart (JSON-or-empty response, same error shape), with progress. */
export function httpMultipartWithProgress<T>(
  path: string,
  formData: FormData,
  options: HttpMultipartProgressOptions = {},
): Promise<T> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(options.method ?? 'POST', `${API_BASE_URL}${path}`);

    const token = resolveAuthToken(options.skipAuth);
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable) return;
      options.onProgress?.(event.loaded / event.total);
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(parseXhrJsonBody<T>(xhr));
        return;
      }
      if (xhr.status === 401 && !options.skipAuth) {
        notifyUnauthorized();
      }
      reject(parseXhrError(xhr));
    };

    xhr.onerror = () => {
      reject({ message: 'Network error', statusCode: 0 } satisfies ApiError);
    };

    xhr.send(formData);
  });
}

function parseXhrJsonBody<T>(xhr: XMLHttpRequest): T {
  if (xhr.status === 204 || !xhr.responseText.trim()) {
    return undefined as T;
  }
  try {
    return JSON.parse(xhr.responseText) as T;
  } catch {
    return xhr.responseText as T;
  }
}

function parseXhrError(xhr: XMLHttpRequest): ApiError {
  try {
    const data = JSON.parse(xhr.responseText) as { title?: string; detail?: string; message?: string };
    return {
      message: data.detail ?? data.message ?? data.title ?? xhr.statusText,
      statusCode: xhr.status,
    };
  } catch {
    return {
      message: xhr.statusText || 'Request failed',
      statusCode: xhr.status,
    };
  }
}
