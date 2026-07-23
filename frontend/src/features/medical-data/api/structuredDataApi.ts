import { httpClient } from '../../../shared/api/httpClient';
import type { MedicalStructuredDataDto } from '../../../shared/types/api';

export const structuredDataApi = {
  getByConsultation: async (consultationId: number) => {
    const data = await httpClient<MedicalStructuredDataDto | null>(
      `/consultation/${consultationId}/structured-data`,
    );
    // ASP.NET returns 204 for null → httpClient yields undefined; React Query requires a defined value.
    return data ?? null;
  },

  approve: (consultationId: number) =>
    httpClient<void>(`/consultation/${consultationId}/structured-data/approve`, {
      method: 'POST',
    }),
};
