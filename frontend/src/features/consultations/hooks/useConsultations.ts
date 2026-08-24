import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { consultationKeys } from '../queryKeys';
import type { CreateConsultationRequest } from '../types';
import { consultationApi } from '../api/consultationApi';
import { patientKeys } from '../../patients';
import { structuredDataKeys } from '../../medical-data';
import { transcriptKeys } from '../../transcripts';

export function useConsultation(id: number) {
  return useQuery({
    queryKey: consultationKeys.consultation(id),
    queryFn: () => consultationApi.getById(id),
    enabled: id > 0,
  });
}

export function useConsultationAudio(consultationId: number, enabled = true) {
  return useQuery({
    queryKey: consultationKeys.consultationAudio(consultationId),
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
    queryKey: consultationKeys.consultationsByPatient(patientId),
    queryFn: () => consultationApi.getByPatient(patientId),
    enabled: patientId > 0,
  });
}

export function useDraftConsultations() {
  return useQuery({
    queryKey: consultationKeys.draftConsultations,
    queryFn: () => consultationApi.getDrafts(),
  });
}

export function useUnattachedDraftConsultations() {
  return useQuery({
    queryKey: consultationKeys.unattachedDraftConsultations,
    queryFn: () => consultationApi.getUnattachedDrafts(),
  });
}

export function useDashboardAnalytics() {
  return useQuery({
    queryKey: consultationKeys.dashboardAnalytics,
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
          queryKey: consultationKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: patientKeys.patientHistoryPrefix(consultation.patientId),
        });
      }
      queryClient.invalidateQueries({ queryKey: consultationKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
      queryClient.setQueryData(consultationKeys.consultation(consultation.id), consultation);
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
      queryClient.setQueryData(consultationKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: consultationKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: consultationKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: patientKeys.patientHistoryPrefix(consultation.patientId),
        });
      }
    },
  });
}

export function useRetryConsultationProcessing() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ consultationId }: { consultationId: number }) =>
      consultationApi.retryProcessing(consultationId),
    onSuccess: (consultation) => {
      queryClient.setQueryData(consultationKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: transcriptKeys.transcript(consultation.id) });
      queryClient.invalidateQueries({ queryKey: structuredDataKeys.structuredData(consultation.id) });
      queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: consultationKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: patientKeys.patientHistoryPrefix(consultation.patientId),
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
      queryClient.setQueryData(consultationKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: consultationKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: consultationKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: patientKeys.patientHistoryPrefix(consultation.patientId),
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
      queryClient.setQueryData(consultationKeys.consultation(consultation.id), consultation);
      queryClient.invalidateQueries({ queryKey: consultationKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
      if (consultation.patientId) {
        queryClient.invalidateQueries({
          queryKey: consultationKeys.consultationsByPatient(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: patientKeys.patientHistoryPrefix(consultation.patientId),
        });
        queryClient.invalidateQueries({
          queryKey: transcriptKeys.transcript(consultation.id),
        });
        queryClient.invalidateQueries({
          queryKey: structuredDataKeys.structuredData(consultation.id),
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
      queryClient.removeQueries({ queryKey: consultationKeys.consultation(variables.consultationId) });
      queryClient.invalidateQueries({ queryKey: consultationKeys.draftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.unattachedDraftConsultations });
      queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
      queryClient.invalidateQueries({
        queryKey: consultationKeys.consultationsByPatient(variables.patientId),
      });
      queryClient.invalidateQueries({
        queryKey: patientKeys.patientHistoryPrefix(variables.patientId),
      });
    },
  });
}
