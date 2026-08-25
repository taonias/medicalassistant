import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { consultationKeys } from '../queryKeys';
import { patientKeys } from '../../../features/patients';
import { transcriptKeys, structuredDataKeys } from '../../clinical-record';
import { createTestQueryClient, queryClientWrapper } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { useRetryConsultationProcessing } from './useConsultations';

describe('Consultation retry-processing cache contract', () => {
  it('publishes the updated Consultation and invalidates its clinical-record and patient views, but not the draft lists', async () => {
    const consultation = {
      id: 42,
      patientId: 7,
      doctorId: 'doctor-1',
      consultationDate: '2026-08-23T12:00:00Z',
      status: 'Transcribing',
    };
    server.use(
      http.post('https://backend.test/api/consultation/42/retry', () =>
        HttpResponse.json(consultation),
      ),
    );
    const queryClient = createTestQueryClient();
    const invalidatedKeys = [
      transcriptKeys.transcript(42),
      structuredDataKeys.structuredData(42),
      consultationKeys.dashboardAnalytics,
      consultationKeys.consultationsByPatient(7),
      patientKeys.patientHistory(7),
    ] as const;
    const untouchedKeys = [
      consultationKeys.draftConsultations,
      consultationKeys.unattachedDraftConsultations,
    ] as const;
    for (const key of [...invalidatedKeys, ...untouchedKeys]) {
      queryClient.setQueryData(key, []);
    }
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useRetryConsultationProcessing(), { wrapper });

    result.current.mutate({ consultationId: 42 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryData(consultationKeys.consultation(42))).toEqual(consultation);
    for (const key of invalidatedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    }
    for (const key of untouchedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(false);
    }
  });
});
