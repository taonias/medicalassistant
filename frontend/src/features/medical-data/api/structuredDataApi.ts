import { httpClient } from '../../../shared/api/httpClient';
import type { MedicalStructuredDataDto } from '../../../shared/types/api';

export const structuredDataApi = {
  getByConsultation: (consultationId: number) =>
    httpClient<MedicalStructuredDataDto | null>(
      `/consultation/${consultationId}/structured-data`,
    ),

  approve: (consultationId: number) =>
    httpClient<void>(`/consultation/${consultationId}/structured-data/approve`, {
      method: 'POST',
    }),
};
