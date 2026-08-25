import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { consultationKeys } from '../queryKeys';
import { patientKeys } from '../../../features/patients';
import { transcriptKeys, structuredDataKeys } from '../../clinical-record';
import { createTestQueryClient, queryClientWrapper } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { useUploadConsultationDocument } from './useConsultations';

describe('Consultation document-upload cache contract', () => {
  it('publishes the updated Consultation and invalidates every affected list, including the clinical record, when a patient is attached', async () => {
    const consultation = {
      id: 42,
      patientId: 7,
      doctorId: 'doctor-1',
      consultationDate: '2026-08-23T12:00:00Z',
      status: 'DocumentUploaded',
    };
    server.use(
      http.post('https://backend.test/api/consultation/42/document', () =>
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
      transcriptKeys.transcript(42),
      structuredDataKeys.structuredData(42),
    ] as const;
    for (const key of affectedKeys) {
      queryClient.setQueryData(key, []);
    }
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useUploadConsultationDocument(), { wrapper });

    result.current.mutate({
      consultationId: 42,
      documentFile: new File(['document'], 'referral.pdf'),
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(queryClient.getQueryData(consultationKeys.consultation(42))).toEqual(consultation);
    for (const key of affectedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    }
  });

  it('does not invalidate the clinical record when no patient is attached yet', async () => {
    const consultation = {
      id: 43,
      patientId: null,
      doctorId: 'doctor-1',
      consultationDate: '2026-08-23T12:00:00Z',
      status: 'DocumentUploaded',
    };
    server.use(
      http.post('https://backend.test/api/consultation/43/document', () =>
        HttpResponse.json(consultation),
      ),
    );
    const queryClient = createTestQueryClient();
    const untouchedKeys = [transcriptKeys.transcript(43), structuredDataKeys.structuredData(43)] as const;
    for (const key of untouchedKeys) {
      queryClient.setQueryData(key, []);
    }
    const wrapper = queryClientWrapper(queryClient);
    const { result } = renderHook(() => useUploadConsultationDocument(), { wrapper });

    result.current.mutate({
      consultationId: 43,
      documentFile: new File(['document'], 'referral.pdf'),
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    for (const key of untouchedKeys) {
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(false);
    }
  });
});
