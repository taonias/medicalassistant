import { httpClient } from '../../../../platform/http';
import type { DoctorNote } from '../types';

export interface CreateDoctorNoteRequest {
  consultationId?: number | null;
  patientId?: number;
  content: string;
}

export const doctorNotesApi = {
  getByConsultation: (consultationId: number) =>
    httpClient<DoctorNote[]>(`/doctorNotes/consultations/${consultationId}`),

  getPatientLevel: (patientId: number) =>
    httpClient<DoctorNote[]>(`/doctorNotes/patients/${patientId}`),

  create: (request: CreateDoctorNoteRequest) =>
    httpClient<DoctorNote>('/doctorNotes', {
      method: 'POST',
      body: request,
    }),
};
