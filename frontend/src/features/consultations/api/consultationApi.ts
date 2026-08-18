import { httpClient, getAuthToken } from '../../../shared/api/httpClient';
import type {
  Consultation,
  ConsultationSummary,
  CreateConsultationRequest,
  DashboardAnalytics,
  DraftConsultationGroup,
} from '../../../shared/types/api';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7037/api';

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

    return httpClient<Consultation>(`/consultation/${consultationId}/audio`, {
      method: 'POST',
      body: formData,
    });
  },

  uploadDocument: (consultationId: number, documentFile: File) => {
    const formData = new FormData();
    formData.append('documentFile', documentFile);

    return httpClient<Consultation>(`/consultation/${consultationId}/document`, {
      method: 'POST',
      body: formData,
    });
  },

  /** Returns an object URL for the consultation audio, or null when no audio exists. */
  getAudioObjectUrl: async (consultationId: number): Promise<string | null> => {
    const headers = new Headers();
    const token = getAuthToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(`${API_BASE_URL}/consultation/${consultationId}/audio`, {
      headers,
    });

    if (response.status === 404) {
      return null;
    }

    if (!response.ok) {
      throw new Error(response.statusText || 'Unable to load consultation audio.');
    }

    const headerType = response.headers.get('Content-Type')?.split(';', 1)[0]?.trim();
    const buffer = await response.arrayBuffer();
    const blob = new Blob([buffer], {
      type: headerType && headerType !== 'application/octet-stream' ? headerType : 'audio/webm',
    });
    return URL.createObjectURL(blob);
  },

  downloadDocument: async (consultationId: number, fileName?: string) => {
    const headers = new Headers();
    const token = getAuthToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(
      `${API_BASE_URL}/consultation/${consultationId}/document`,
      { headers },
    );

    if (!response.ok) {
      throw new Error(response.statusText || 'Unable to download document.');
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName ?? 'consultation.pdf';
    anchor.click();
    URL.revokeObjectURL(url);
  },

  downloadAudio: async (consultationId: number, fileName?: string) => {
    const headers = new Headers();
    const token = getAuthToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(`${API_BASE_URL}/consultation/${consultationId}/audio`, {
      headers,
    });

    if (!response.ok) {
      throw new Error(response.statusText || 'Unable to download recording.');
    }

    const headerType = response.headers.get('Content-Type')?.split(';', 1)[0]?.trim();
    const extension =
      headerType === 'audio/mpeg' || headerType === 'audio/mp3'
        ? 'mp3'
        : headerType === 'audio/wav'
          ? 'wav'
          : headerType === 'audio/ogg'
            ? 'ogg'
            : 'webm';
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName ?? `consultation-${consultationId}.${extension}`;
    anchor.click();
    URL.revokeObjectURL(url);
  },

  delete: (id: number) =>
    httpClient<void>(`/consultation/${id}`, {
      method: 'DELETE',
    }),
};
