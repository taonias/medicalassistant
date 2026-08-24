import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import { useRecentPatients } from '../../../shared/hooks/useRecentPatients';
import type { CreatePatientRequest, UpdatePatientRequest } from '../../../shared/types/api';
import { useAuthStore } from '../../auth';
import { patientApi, type PatientHistoryQueryParams } from '../api/patientApi';

export function usePatients() {
  const token = useAuthStore((state) => state.token);

  return useQuery({
    queryKey: queryKeys.patients,
    queryFn: () => patientApi.list(),
    enabled: Boolean(token),
  });
}

export function usePatient(id: number) {
  const { addRecentPatient } = useRecentPatients();

  return useQuery({
    queryKey: queryKeys.patient(id),
    queryFn: async () => {
      const patient = await patientApi.getById(id);
      addRecentPatient(patient);
      return patient;
    },
    enabled: id > 0,
  });
}

export function usePatientHistory(id: number, params?: PatientHistoryQueryParams) {
  const fromDate = params?.fromDate;
  const toDate = params?.toDate;
  const page = params?.page;
  const pageSize = params?.pageSize;
  const source = params?.source;
  const includeStructuredData = params?.includeStructuredData;

  return useQuery({
    queryKey: queryKeys.patientHistory(id, fromDate, toDate, page, pageSize, source),
    queryFn: () =>
      patientApi.getHistory(id, {
        fromDate,
        toDate,
        page,
        pageSize,
        source,
        includeStructuredData,
      }),
    enabled: id > 0,
  });
}

export function useCreatePatient() {
  const queryClient = useQueryClient();
  const { addRecentPatient } = useRecentPatients();

  return useMutation({
    mutationFn: (request: CreatePatientRequest) => patientApi.create(request),
    onSuccess: (patient) => {
      addRecentPatient(patient);
      queryClient.setQueryData(queryKeys.patient(patient.id), patient);
      queryClient.invalidateQueries({ queryKey: queryKeys.patients });
    },
  });
}

export function useUpdatePatient() {
  const queryClient = useQueryClient();
  const { addRecentPatient } = useRecentPatients();

  return useMutation({
    mutationFn: (request: UpdatePatientRequest) => patientApi.update(request),
    onSuccess: (patient) => {
      addRecentPatient(patient);
      queryClient.setQueryData(queryKeys.patient(patient.id), patient);
      queryClient.invalidateQueries({ queryKey: queryKeys.patients });
      queryClient.invalidateQueries({ queryKey: queryKeys.patientHistoryPrefix(patient.id) });
    },
  });
}
