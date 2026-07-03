import { httpClient } from '../../../shared/api/httpClient';
import type {
  Consultation,
  ConsultationSummary,
  CreateConsultationRequest,
  DraftConsultationGroup,
} from '../../../shared/types/api';

export const consultationApi = {
  getById: (id: number) => httpClient<Consultation>(`/consultation/${id}`),

  getByPatient: (patientId: number) =>
    httpClient<ConsultationSummary[]>(`/consultation/patient/${patientId}`),

  getDrafts: () => httpClient<DraftConsultationGroup[]>('/consultation/drafts'),

  create: (request: CreateConsultationRequest, idempotencyKey?: string) =>
    httpClient<Consultation>('/consultation', {
      method: 'POST',
      body: request,
      headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
    }),

  uploadAudio: (consultationId: number, audioFile: File, durationSeconds?: number) => {
    const formData = new FormData();
    formData.append('audioFile', audioFile);
    if (durationSeconds !== undefined) {
      formData.append('durationSeconds', String(durationSeconds));
    }

    return httpClient<Consultation>(`/consultation/${consultationId}/audio`, {
      method: 'POST',
      body: formData,
    });
  },
};
