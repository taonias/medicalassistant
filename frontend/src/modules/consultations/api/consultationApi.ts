import { httpClient, httpMultipart, httpBlob, httpDownload } from '../../../platform/http';
import type { ApiError } from '../../../shared/types/api';
import type {
  Consultation,
  ConsultationSummary,
  CreateConsultationRequest,
  DraftConsultationGroup,
} from '../types';
import type { DashboardAnalytics } from '../../../features/dashboard';

export const consultationApi = {
  getById: (id: number) => httpClient<Consultation>(`/consultation/${id}`),

  getByPatient: (patientId: number) =>
    httpClient<ConsultationSummary[]>(`/consultation/patient/${patientId}`),

  getDrafts: () => httpClient<DraftConsultationGroup[]>('/consultation/drafts'),

  getUnattachedDrafts: () =>
    httpClient<ConsultationSummary[]>('/consultation/drafts/unattached'),

  getAnalytics: () => httpClient<DashboardAnalytics>('/consultation/analytics'),

  create: (request: CreateConsultationRequest, idempotencyKey?: string) =>
    httpClient<Consultation>('/consultation', {
      method: 'POST',
      body: request,
      headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
    }),

  assignPatient: (consultationId: number, patientId: number) =>
    httpClient<Consultation>(`/consultation/${consultationId}/patient`, {
      method: 'PUT',
      body: { patientId },
    }),

  /** Manually retry a failed consultation (transcription or indexing). */
  retryProcessing: (consultationId: number) =>
    httpClient<Consultation>(`/consultation/${consultationId}/retry`, {
      method: 'POST',
    }),

  uploadAudio: (consultationId: number, audioFile: File, durationSeconds?: number) => {
    const formData = new FormData();
    formData.append('audioFile', audioFile);
    if (durationSeconds !== undefined) {
      formData.append('durationSeconds', String(durationSeconds));
    }

    return httpMultipart<Consultation>(`/consultation/${consultationId}/audio`, formData);
  },

  uploadDocument: (consultationId: number, documentFile: File) => {
    const formData = new FormData();
    formData.append('documentFile', documentFile);

    return httpMultipart<Consultation>(`/consultation/${consultationId}/document`, formData);
  },

  /** Returns an object URL for the consultation audio, or null when no audio exists. */
  getAudioObjectUrl: async (consultationId: number): Promise<string | null> => {
    try {
      const { blob, contentType } = await httpBlob(`/consultation/${consultationId}/audio`);
      const headerType = contentType?.split(';', 1)[0]?.trim();
      const typedBlob = new Blob([blob], {
        type: headerType && headerType !== 'application/octet-stream' ? headerType : 'audio/webm',
      });
      return URL.createObjectURL(typedBlob);
    } catch (error) {
      if ((error as ApiError).statusCode === 404) {
        return null;
      }
      throw error;
    }
  },

  downloadDocument: (consultationId: number, fileName?: string) =>
    httpDownload(`/consultation/${consultationId}/document`, fileName ?? 'consultation.pdf'),

  downloadAudio: (consultationId: number, fileName?: string) =>
    httpDownload(
      `/consultation/${consultationId}/audio`,
      fileName ??
        ((contentType) => {
          const headerType = contentType?.split(';', 1)[0]?.trim();
          const extension =
            headerType === 'audio/mpeg' || headerType === 'audio/mp3'
              ? 'mp3'
              : headerType === 'audio/wav'
                ? 'wav'
                : headerType === 'audio/ogg'
                  ? 'ogg'
                  : 'webm';
          return `consultation-${consultationId}.${extension}`;
        }),
    ),

  delete: (id: number) =>
    httpClient<void>(`/consultation/${id}`, {
      method: 'DELETE',
    }),
};
