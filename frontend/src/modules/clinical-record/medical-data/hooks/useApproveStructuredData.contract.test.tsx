import { act, renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { structuredDataKeys } from '../queryKeys';
import { consultationKeys } from '../../../consultations';
import { createTestQueryClient, queryClientWrapper } from '../../../../test/queryClient';
import { server } from '../../../../test/server';
import { useApproveStructuredData } from './useApproveStructuredData';

const apiBaseUrl = 'https://backend.test/api';

function deferred() {
  let resolve!: () => void;
  const promise = new Promise<void>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
}

describe('Structured Medical Data approval cache contract', () => {
  it('invalidates structured data and Consultation caches only after approval succeeds', async () => {
    const requestStarted = deferred();
    const releaseResponse = deferred();
    server.use(
      http.post(`${apiBaseUrl}/consultation/42/structured-data/approve`, async () => {
        requestStarted.resolve();
        await releaseResponse.promise;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const queryClient = createTestQueryClient();
    queryClient.setQueryData(structuredDataKeys.structuredData(42), { approved: false });
    queryClient.setQueryData(consultationKeys.consultation(42), { id: 42 });
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useApproveStructuredData(), { wrapper });

    act(() => result.current.mutate(42));
    await requestStarted.promise;

    expect(queryClient.getQueryState(structuredDataKeys.structuredData(42))?.isInvalidated).toBe(false);
    expect(queryClient.getQueryState(consultationKeys.consultation(42))?.isInvalidated).toBe(false);

    releaseResponse.resolve();
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryState(structuredDataKeys.structuredData(42))?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(consultationKeys.consultation(42))?.isInvalidated).toBe(true);
  });
});
