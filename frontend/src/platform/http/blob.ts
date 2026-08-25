import { sendRequest } from './core';

export interface HttpBlobResult {
  blob: Blob;
  contentType: string | null;
}

/**
 * A binary GET. Callers handle any domain-specific status codes themselves
 * (e.g. treating 404 as "not found yet" rather than an error) by catching
 * the ApiError this throws on a non-ok response (R29).
 */
export async function httpBlob(path: string): Promise<HttpBlobResult> {
  const response = await sendRequest(path, {}, undefined);
  return {
    blob: await response.blob(),
    contentType: response.headers.get('Content-Type'),
  };
}
