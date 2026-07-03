import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { MedicalStructuredDataDto } from '../../../shared/types/api';
import { structuredDataApi } from '../api/structuredDataApi';

export function useStructuredData(consultationId: number) {
  return useQuery<MedicalStructuredDataDto | null>({
    queryKey: queryKeys.structuredData(consultationId),
    queryFn: () => structuredDataApi.getByConsultation(consultationId),
    enabled: consultationId > 0,
    retry: (count, error) => {
      const statusCode = (error as { statusCode?: number }).statusCode;
      if (statusCode === 404) return false;
      return count < 2;
    },
  });
}
