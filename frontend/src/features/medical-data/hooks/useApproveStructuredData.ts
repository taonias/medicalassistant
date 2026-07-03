import { useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import { structuredDataApi } from '../api/structuredDataApi';

export function useApproveStructuredData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (consultationId: number) =>
      structuredDataApi.approve(consultationId),
    onSuccess: (_data, consultationId) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.structuredData(consultationId),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeys.consultation(consultationId),
      });
    },
  });
}

