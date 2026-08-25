import { sendRequest } from './core';

/**
 * A binary GET that triggers a browser save — the anchor/click/revoke
 * sequence every download used to hand-roll separately (R29). `fileName` can
 * be a plain string, or a function of the response's content-type for
 * callers whose default filename depends on what came back.
 */
export async function httpDownload(
  path: string,
  fileName: string | ((contentType: string | null) => string),
): Promise<void> {
  const response = await sendRequest(path, {}, undefined);
  const contentType = response.headers.get('Content-Type');
  const blob = await response.blob();
  const resolvedFileName = typeof fileName === 'function' ? fileName(contentType) : fileName;

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = resolvedFileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
