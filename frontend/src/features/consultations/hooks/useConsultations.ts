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

export function useConsultationAudio(consultationId: number, enabled = true) {
  return useQuery({
    queryKey: queryKeys.consultationAudio(consultationId),
    queryFn: async () => {
      const objectUrl = await consultationApi.getAudioObjectUrl(consultationId);
      return objectUrl;
    },
    enabled: consultationId > 0 && enabled,
    staleTime: Infinity,
    gcTime: 0,
    retry: (count, error) => {
      if ((error as { statusCode?: number }).statusCode === 404) return false;
      return count < 1;
    },
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

export function useUnattachedDraftConsultations() {
  return useQuery({
    queryKey: queryKeys.unattachedDraftConsultations,
    queryFn: () => consultationApi.getUnattachedDrafts(),
  });
}

export function useDashboardAnalytics() {
  return useQuery({
    queryKey: queryKeys.dashboardAnalytics,
    queryFn: () => consultationApi.getAnalytics(),
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
        queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistoryPrefix(consultation.patientId),
        });
      }
      queryClient.invalidateQueries({ queryKey: queryKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardAnalytics });
      queryClient.setQueryData(queryKeys.consultation(consultation.id), consultation);
    },
  });
}

export function useAssignConsultationPatient() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      consultationId,
      patientId,
    }: {
      consultationId: number;
      patientId: number;
    }) => consultationApi.assignPatient(consultationId, patientId),
    onSuccess: (consultation) => {
      queryClient.setQueryData(queryKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: queryKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: queryKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistoryPrefix(consultation.patientId),
        });
      }
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
      queryClient.invalidateQueries({ queryKey: queryKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: queryKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistoryPrefix(consultation.patientId),
        });
      }
    },
  });
}

export function useUploadConsultationDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      consultationId,
      documentFile,
    }: {
      consultationId: number;
      documentFile: File;
    }) => consultationApi.uploadDocument(consultationId, documentFile),
    onSuccess: (consultation) => {
      queryClient.setQueryData(queryKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: queryKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: queryKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistoryPrefix(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: queryKeys.transcript(consultation.id),
        });
        queryClient.invalidateQueries({
          queryKey: queryKeys.structuredData(consultation.id),
        });
      }
    },
  });
}

export function useDeleteConsultation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ consultationId }: { consultationId: number; patientId: number }) =>
      consultationApi.delete(consultationId),
    onSuccess: (_data, variables) => {
      queryClient.removeQueries({ queryKey: queryKeys.consultation(variables.consultationId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardAnalytics });
      queryClient.invalidateQueries({
        queryKey: queryKeys.consultationsByPatient(variables.patientId),
      });
      queryClient.invalidateQueries({
        queryKey: queryKeys.patientHistoryPrefix(variables.patientId),
      });
    },
  });
}
