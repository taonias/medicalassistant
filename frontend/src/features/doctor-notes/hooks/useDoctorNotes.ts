import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { DoctorNote } from '../../../shared/types/api';
import { doctorNotesApi, type CreateDoctorNoteRequest } from '../api/doctorNotesApi';

export function useDoctorNotes(consultationId: number) {
  return useQuery<DoctorNote[]>({
    queryKey: queryKeys.doctorNotes(consultationId),
    queryFn: () => doctorNotesApi.getByConsultation(consultationId),
    enabled: consultationId > 0,
    retry: (count, error) => {
      const statusCode = (error as { statusCode?: number }).statusCode;
      if (statusCode === 404) return false;
      return count < 2;
    },
  });
}

export function useCreateDoctorNote() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateDoctorNoteRequest) =>
      doctorNotesApi.create(request),
    onSuccess: (_created, variables) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.doctorNotes(variables.consultationId),
      });
    },
  });
}
