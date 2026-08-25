import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { consultationKeys } from '../queryKeys';
import { patientKeys } from '../../../features/patients';
import { createTestQueryClient, queryClientWrapper } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { useAssignConsultationPatient } from './useConsultations';

describe('Consultation patient-assignment cache contract', () => {
  it('publishes the updated Consultation and invalidates every affected list', async () => {
    const consultation = {
      id: 42,
      patientId: 7,
      doctorId: 'doctor-1',
      consultationDate: '2026-08-23T12:00:00Z',
      status: 'Draft',
    };
    server.use(
      http.put('https://backend.test/api/consultation/42/patient', () =>
        HttpResponse.json(consultation),
      ),
    );
    const queryClient = createTestQueryClient();
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
    const { result } = renderHook(() => useAssignConsultationPatient(), { wrapper });

    result.current.mutate({ consultationId: 42, patientId: 7 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryData(consultationKeys.consultation(42))).toEqual(consultation);
    for (const key of affectedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    }
  });
});
