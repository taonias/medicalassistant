import { httpClient } from '../../../../platform/http';
import type { MedicalStructuredDataDto } from '../types';

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
