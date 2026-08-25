// Public surface of the HTTP transport platform (R29): a small, predictable
// set of request primitives — JSON, multipart, Blob, and browser download —
// plus the one hook the rest of the app uses to wire session auth into it.
// Feature code talks to these, never to fetch or auth internals directly.
export { httpClient } from './json';
export type { HttpRequestOptions } from './json';
export { httpMultipart } from './multipart';
export type { HttpMultipartOptions } from './multipart';
export { httpBlob } from './blob';
export type { HttpBlobResult } from './blob';
export { httpDownload } from './download';
export { configureHttpAuth } from './config';
export type { HttpAuthConfig } from './config';
