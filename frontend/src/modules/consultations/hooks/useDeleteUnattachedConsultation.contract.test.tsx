import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { consultationKeys } from '../queryKeys';
import { createTestQueryClient, queryClientWrapper } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { useDeleteUnattachedConsultation } from './useConsultations';

describe('Unattached consultation deletion cache contract', () => {
  it('removes the deleted Consultation from cache and invalidates the unattached lists', async () => {
    server.use(
      http.delete('https://backend.test/api/consultation/42', () => new HttpResponse(null, { status: 204 })),
    );
    const queryClient = createTestQueryClient();
    const consultation = { id: 42, patientId: null, doctorId: 'doctor-1', status: 'AudioUploaded' };
    queryClient.setQueryData(consultationKeys.consultation(42), consultation);
    const affectedKeys = [
      consultationKeys.draftConsultations,
      consultationKeys.unattachedDraftConsultations,
      consultationKeys.dashboardAnalytics,
    ] as const;
    for (const key of affectedKeys) {
      queryClient.setQueryData(key, []);
    }
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useDeleteUnattachedConsultation(), { wrapper });

    result.current.mutate({ consultationId: 42 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryState(consultationKeys.consultation(42))).toBeUndefined();
    for (const key of affectedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    }
  });
});
