import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { MedicalStructuredDataDto } from '../../../shared/types/api';
import { structuredDataApi } from '../api/structuredDataApi';

const STRUCTURED_DATA_STATUSES = new Set([
  'Transcribed',
  'StructuredDataPending',
  'Completed',
]);

const POLLING_STATUSES = new Set(['Transcribed', 'StructuredDataPending']);

export function useStructuredData(consultationId: number, status?: string) {
  const normalized = status?.replace(/\s+/g, '') ?? '';
  const enabled =
    consultationId > 0 &&
    status != null &&
    STRUCTURED_DATA_STATUSES.has(normalized);

  return useQuery<MedicalStructuredDataDto | null>({
    queryKey: queryKeys.structuredData(consultationId),
    queryFn: async () => (await structuredDataApi.getByConsultation(consultationId)) ?? null,
    enabled,
    refetchInterval: (query) => {
      if (!POLLING_STATUSES.has(normalized)) return false;
      if (query.state.data) return false;
      return 3000;
    },
    retry: (count, error) => {
      const statusCode = (error as { statusCode?: number }).statusCode;
      if (statusCode === 404) return false;
      return count < 2;
    },
  });
}
