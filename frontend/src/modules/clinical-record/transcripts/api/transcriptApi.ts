import { httpClient } from '../../../../platform/http';
import type { Transcript } from '../types';

export const transcriptApi = {
  getByConsultation: (consultationId: number) =>
    httpClient<Transcript | null>(`/transcript/${consultationId}`),

  update: (consultationId: number, transcript: string) =>
    httpClient<Transcript>(`/transcript/${consultationId}`, {
      method: 'PUT',
      body: { transcript },
    }),
};
