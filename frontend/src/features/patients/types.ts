import type { DoctorNote } from '../doctor-notes';

export interface Patient {
  id: number;
  externalPatientId?: string;
  firstName: string;
  lastName: string;
  dateOfBirth?: string;
  assignedDoctorId: string;
  summary?: string;
}

export interface PatientListItem {
  id: number;
  firstName: string;
  lastName: string;
  dateOfBirth?: string;
  dateCreated?: string;
  lastConsultationDate?: string;
  consultationCount: number;
}

export interface CreatePatientRequest {
  externalPatientId?: string;
  firstName: string;
  lastName: string;
  dateOfBirth?: string;
}

export interface UpdatePatientRequest {
  id: number;
  externalPatientId?: string | null;
  firstName: string;
  lastName: string;
  dateOfBirth?: string | null;
}

export interface ConsultationHistoryItem {
  id: number;
  consultationDate: string;
  status: string;
  durationSeconds?: number;
  hasAudio?: boolean;
  hasDocument?: boolean;
  /** "Audio" | "Pdf" | "Unknown" */
  source?: string;
  transcriptSnippet?: string;
  structuredSummary?: string;
}

export interface PatientHistory {
  patient: Patient;
  consultations: ConsultationHistoryItem[];
  doctorNotes: DoctorNote[];
  totalConsultations?: number;
  page?: number;
  pageSize?: number;
}
