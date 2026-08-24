import { useQuery } from '@tanstack/react-query';
import { structuredDataKeys } from '../queryKeys';
import type { MedicalStructuredDataDto } from '../types';
import { structuredDataApi } from '../api/structuredDataApi';

const STRUCTURED_DATA_STATUSES = new Set([
  'StructuredDataPending',
  'Completed',
]);

export function useStructuredData(consultationId: number, status?: string) {
  const normalized = status?.replace(/\s+/g, '') ?? '';
  const enabled =
    consultationId > 0 &&
    status != null &&
    STRUCTURED_DATA_STATUSES.has(normalized);

  return useQuery<MedicalStructuredDataDto | null>({
    queryKey: structuredDataKeys.structuredData(consultationId),
    queryFn: async () => (await structuredDataApi.getByConsultation(consultationId)) ?? null,
    enabled,
    retry: (count, error) => {
      const statusCode = (error as { statusCode?: number }).statusCode;
      if (statusCode === 404) return false;
      return count < 2;
    },
  });
}
