import { useMutation, useQueryClient } from '@tanstack/react-query';
import { structuredDataKeys } from '../queryKeys';
import { structuredDataApi } from '../api/structuredDataApi';
import { consultationKeys } from '../../../consultations';

export function useApproveStructuredData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (consultationId: number) =>
      structuredDataApi.approve(consultationId),
    onSuccess: (_data, consultationId) => {
      void queryClient.invalidateQueries({
        queryKey: structuredDataKeys.structuredData(consultationId),
      });
      void queryClient.invalidateQueries({
        queryKey: consultationKeys.consultation(consultationId),
      });
    },
  });
}

