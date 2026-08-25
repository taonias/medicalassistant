import type { ApiError } from '../../shared/types/api';

export async function parseError(response: Response): Promise<ApiError> {
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
