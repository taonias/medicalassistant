import { httpClient } from '../../../platform/http';
import type {
  CreatePatientRequest,
  Patient,
  PatientHistory,
  PatientListItem,
  UpdatePatientRequest,
} from '../types';

export type PatientHistoryQueryParams = {
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
  source?: string;
  includeStructuredData?: boolean;
};

export const patientApi = {
  list: () => httpClient<PatientListItem[]>('/patient'),

  getById: (id: number) => httpClient<Patient>(`/patient/${id}`),

  getHistory: (id: number, params?: PatientHistoryQueryParams) => {
    const search = new URLSearchParams();
    // Full-day bounds so a single-day selection includes all consultations that day.
    if (params?.fromDate) search.set('fromDate', `${params.fromDate}T00:00:00`);
    if (params?.toDate) search.set('toDate', `${params.toDate}T23:59:59`);
    if (params?.page != null) search.set('page', String(params.page));
    if (params?.pageSize != null) search.set('pageSize', String(params.pageSize));
    if (params?.source && params.source !== 'all') search.set('source', params.source);
    if (params?.includeStructuredData === false) {
      search.set('includeStructuredData', 'false');
    }
    const query = search.toString();
    return httpClient<PatientHistory>(`/patient/${id}/history${query ? `?${query}` : ''}`);
  },

  create: (request: CreatePatientRequest) =>
    httpClient<Patient>('/patient', {
      method: 'POST',
      body: request,
    }),

  update: (request: UpdatePatientRequest) =>
    httpClient<Patient>('/patient', {
      method: 'PUT',
      body: request,
    }),
};
