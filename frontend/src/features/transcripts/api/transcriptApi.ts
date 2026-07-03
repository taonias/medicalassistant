import { httpClient } from '../../../shared/api/httpClient';
import type { Transcript } from '../../../shared/types/api';

export const transcriptApi = {
  getByConsultation: (consultationId: number) =>
    httpClient<Transcript>(`/transcript/${consultationId}`),
};
