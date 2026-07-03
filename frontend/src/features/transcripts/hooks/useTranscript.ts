import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import { transcriptApi } from '../api/transcriptApi';

export function useTranscript(consultationId: number, enabled = true) {
  return useQuery({
    queryKey: queryKeys.transcript(consultationId),
    queryFn: () => transcriptApi.getByConsultation(consultationId),
    enabled: consultationId > 0 && enabled,
    retry: (count, error) => {
      if ((error as { statusCode?: number }).statusCode === 404) return false;
      return count < 2;
    },
  });
}
