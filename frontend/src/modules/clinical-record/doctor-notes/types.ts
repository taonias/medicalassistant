export interface DoctorNote {
  id: number;
  doctorId: string;
  patientId: number;
  consultationId?: number | null;
  content: string;
  dateCreated?: string | null;
  dateModified?: string | null;
}
