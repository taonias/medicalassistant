import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { consultationKeys } from '../queryKeys';
import { patientKeys } from '../../../features/patients';
import { createTestQueryClient, queryClientWrapper } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { useDeleteConsultation } from './useConsultations';

describe('Consultation deletion cache contract', () => {
  it('removes the deleted Consultation from cache and invalidates every affected list', async () => {
    server.use(
      http.delete('https://backend.test/api/consultation/42', () => new HttpResponse(null, { status: 204 })),
    );
    const queryClient = createTestQueryClient();
    const consultation = { id: 42, patientId: 7, doctorId: 'doctor-1', status: 'Completed' };
    queryClient.setQueryData(consultationKeys.consultation(42), consultation);
    const affectedKeys = [
      consultationKeys.consultationsByPatient(7),
      patientKeys.patientHistory(7),
      consultationKeys.draftConsultations,
      consultationKeys.unattachedDraftConsultations,
      consultationKeys.dashboardAnalytics,
    ] as const;
    for (const key of affectedKeys) {
      queryClient.setQueryData(key, []);
    }
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useDeleteConsultation(), { wrapper });

    result.current.mutate({ consultationId: 42, patientId: 7 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryState(consultationKeys.consultation(42))).toBeUndefined();
    for (const key of affectedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    }
  });
});
