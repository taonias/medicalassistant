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

export function usePatientDoctorNotes(patientId: number) {
  return useQuery<DoctorNote[]>({
    queryKey: queryKeys.patientDoctorNotes(patientId),
    queryFn: () => doctorNotesApi.getPatientLevel(patientId),
    enabled: patientId > 0,
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
    mutationFn: (request: CreateDoctorNoteRequest) => doctorNotesApi.create(request),
    onSuccess: (created, variables) => {
      if (variables.consultationId != null && variables.consultationId > 0) {
        void queryClient.invalidateQueries({
          queryKey: queryKeys.doctorNotes(variables.consultationId),
        });
      }

      const patientId = created.patientId || variables.patientId;
      if (patientId != null && patientId > 0 && (variables.consultationId == null || variables.consultationId === 0)) {
        void queryClient.invalidateQueries({
          queryKey: queryKeys.patientDoctorNotes(patientId),
        });
        void queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistoryPrefix(patientId),
        });
      }
    },
  });
}
