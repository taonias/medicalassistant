import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { transcriptKeys } from '../queryKeys';
import type { Transcript } from '../types';
import { transcriptApi } from '../api/transcriptApi';

function isTranscriptApiError(error: unknown): error is { statusCode?: number } {
  return typeof error === 'object' && error !== null;
}

export function useTranscript(consultationId: number, enabled = true) {
  return useQuery({
    queryKey: transcriptKeys.transcript(consultationId),
    queryFn: async (): Promise<Transcript | null> => {
      try {
        return await transcriptApi.getByConsultation(consultationId);
      } catch (error) {
        // Transcript may not exist yet while processing.
        if (isTranscriptApiError(error) && error.statusCode === 404) {
          return null;
        }
        throw error;
      }
    },
    enabled: consultationId > 0 && enabled,
    retry: (count, error) => {
      if (isTranscriptApiError(error) && error.statusCode === 404) return false;
      return count < 2;
    },
  });
}

export function useUpdateTranscript(consultationId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (transcript: string) => transcriptApi.update(consultationId, transcript),
    onSuccess: (updated) => {
      queryClient.setQueryData(transcriptKeys.transcript(consultationId), updated);
      void queryClient.invalidateQueries({ queryKey: transcriptKeys.transcript(consultationId) });
    },
  });
}
