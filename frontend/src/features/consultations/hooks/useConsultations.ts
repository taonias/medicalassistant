import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { CreateConsultationRequest } from '../../../shared/types/api';
import { consultationApi } from '../api/consultationApi';

export function useConsultation(id: number) {
  return useQuery({
    queryKey: queryKeys.consultation(id),
    queryFn: () => consultationApi.getById(id),
    enabled: id > 0,
  });
}

export function useConsultationsByPatient(patientId: number) {
  return useQuery({
    queryKey: queryKeys.consultationsByPatient(patientId),
    queryFn: () => consultationApi.getByPatient(patientId),
    enabled: patientId > 0,
  });
}

export function useDraftConsultations() {
  return useQuery({
    queryKey: queryKeys.draftConsultations,
    queryFn: () => consultationApi.getDrafts(),
  });
}

export function useCreateConsultation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      request,
      idempotencyKey,
    }: {
      request: CreateConsultationRequest;
      idempotencyKey?: string;
    }) => consultationApi.create(request, idempotencyKey),
    onSuccess: (consultation) => {
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: queryKeys.consultationsByPatient(consultation.patientId),
        });
      }
      queryClient.invalidateQueries({ queryKey: queryKeys.draftConsultations });
      queryClient.setQueryData(queryKeys.consultation(consultation.id), consultation);
    },
  });
}

export function useUploadConsultationAudio() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      consultationId,
      audioFile,
      durationSeconds,
    }: {
      consultationId: number;
      audioFile: File;
      durationSeconds?: number;
    }) => consultationApi.uploadAudio(consultationId, audioFile, durationSeconds),
    onSuccess: (consultation) => {
      queryClient.setQueryData(queryKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: queryKeys.draftConsultations });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: queryKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistory(consultation.patientId),
        });
      }
    },
  });
}
