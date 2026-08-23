import { http, HttpResponse } from 'msw';

import { server } from './server';

export type RequestContract = { method: string; path: string };

export function recordApiRequests() {
  const origins = new Set<string>();
  const requests: RequestContract[] = [];
  server.use(
    http.all('*', ({ request }) => {
      const url = new URL(request.url);
      origins.add(url.origin);
      requests.push({ method: request.method, path: `${url.pathname}${url.search}` });
      return HttpResponse.json({});
    }),
  );
  return { origins, requests };
}
