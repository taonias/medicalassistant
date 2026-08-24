export interface Consultation {
  id: number;
  patientId?: number;
  doctorId: string;
  consultationDate: string;
  status: string;
  audioBlobUri?: string;
  audioContentType?: string;
  documentBlobUri?: string;
  documentContentType?: string;
  documentFileName?: string;
  durationSeconds?: number;
  idempotencyKey?: string;
  failureReason?: string;
}

export interface ConsultationSummary {
  id: number;
  consultationDate: string;
  status: string;
  hasAudio: boolean;
  hasDocument?: boolean;
  durationSeconds?: number;
}

export interface DraftConsultationGroup {
  patientId: number;
  firstName: string;
  lastName: string;
  consultations: ConsultationSummary[];
}

export interface CreateConsultationRequest {
  patientId?: number;
  consultationDate?: string;
  durationSeconds?: number;
  idempotencyKey?: string;
}
