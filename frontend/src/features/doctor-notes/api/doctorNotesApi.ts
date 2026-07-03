import { httpClient } from '../../../shared/api/httpClient';
import type { DoctorNote } from '../../../shared/types/api';

export interface CreateDoctorNoteRequest {
  consultationId: number;
  title?: string;
  content: string;
}

export const doctorNotesApi = {
  getByConsultation: (consultationId: number) =>
    httpClient<DoctorNote[]>(`/doctorNotes/consultations/${consultationId}`),

  create: (request: CreateDoctorNoteRequest) =>
    httpClient<DoctorNote>('/doctorNotes', {
      method: 'POST',
      body: request,
    }),
};
