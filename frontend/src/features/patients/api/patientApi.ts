import { httpClient } from '../../../shared/api/httpClient';
import type {
  CreatePatientRequest,
  Patient,
  PatientHistory,
  PatientListItem,
} from '../../../shared/types/api';

export const patientApi = {
  list: () => httpClient<PatientListItem[]>('/patient'),

  getById: (id: number) => httpClient<Patient>(`/patient/${id}`),

  getHistory: (id: number, params?: { fromDate?: string; toDate?: string }) => {
    const search = new URLSearchParams();
    if (params?.fromDate) search.set('fromDate', params.fromDate);
    if (params?.toDate) search.set('toDate', params.toDate);
    const query = search.toString();
    return httpClient<PatientHistory>(`/patient/${id}/history${query ? `?${query}` : ''}`);
  },

  create: (request: CreatePatientRequest) =>
    httpClient<Patient>('/patient', {
      method: 'POST',
      body: request,
    }),
};
