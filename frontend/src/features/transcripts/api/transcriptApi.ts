import { httpClient } from '../../../shared/api/httpClient';
import type { Transcript } from '../../../shared/types/api';

export const transcriptApi = {
  getByConsultation: (consultationId: number) =>
    httpClient<Transcript | null>(`/transcript/${consultationId}`),

  update: (consultationId: number, transcript: string) =>
    httpClient<Transcript>(`/transcript/${consultationId}`, {
      method: 'PUT',
      body: { transcript },
    }),
};
