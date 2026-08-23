import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { queryKeys } from '../../../shared/constants/queryKeys';
import { createTestQueryClient, queryClientWrapper } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { useCreateConsultation } from './useConsultations';

describe('Consultation creation cache contract', () => {
  it('publishes the created Consultation and invalidates every affected list', async () => {
    const consultation = {
      id: 42,
      patientId: 7,
      doctorId: 'doctor-1',
      consultationDate: '2026-08-23T12:00:00Z',
      status: 'Draft',
    };
    server.use(
      http.post('https://backend.test/api/consultation', () => HttpResponse.json(consultation)),
    );
    const queryClient = createTestQueryClient();
    const affectedKeys = [
      queryKeys.consultationsByPatient(7),
      queryKeys.patientHistory(7),
      queryKeys.draftConsultations,
      queryKeys.unattachedDraftConsultations,
      queryKeys.dashboardAnalytics,
    ] as const;
    for (const key of affectedKeys) {
      queryClient.setQueryData(key, []);
    }
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useCreateConsultation(), { wrapper });

    result.current.mutate({ request: { patientId: 7 }, idempotencyKey: 'create-42' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryData(queryKeys.consultation(42))).toEqual(consultation);
    for (const key of affectedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    }
  });
});
