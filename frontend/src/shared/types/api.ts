export interface AuthRequest {
  userName: string;
  password: string;
}

export interface AuthResponse {
  id: string;
  userName?: string;
  email?: string;
  emailConfirmed: boolean;
  firstName?: string;
  lastName?: string;
  token: string;
  roles: string[];
}

export interface RegistrationRequest {
  email: string;
  userName: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface RegistrationResponse {
  userId: string;
  isApproved: boolean;
}

export interface ManagedUser {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  isApproved: boolean;
  roles: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogEntry {
  id: number;
  userId?: string | null;
  userName?: string | null;
  action: string;
  entityType?: string | null;
  entityId?: string | null;
  details?: string | null;
  ipAddress?: string | null;
  timestamp: string;
}

export interface ErrorLogEntry {
  id: number;
  message?: string | null;
  stackTrace?: string | null;
  path?: string | null;
  method?: string | null;
  timestamp: string;
}

export interface SetUserApprovalRequest {
  isApproved: boolean;
}

export interface UserSession {
  id: string;
  userName: string;
  email: string;
  emailConfirmed: boolean;
  firstName: string;
  lastName: string;
}

export interface UpdateUserProfileRequest {
  firstName: string;
  lastName: string;
  email: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

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

export interface MedicalStructuredDataDto {
  id: number;
  consultationId: number;
  transcriptId?: number | null;
  schemaVersion: string;
  structuredPayload: string;
  extractedAt: string;
  approved: boolean;
}

export interface PatientHistory {
  patient: Patient;
  consultations: ConsultationHistoryItem[];
  doctorNotes: DoctorNote[];
  totalConsultations?: number;
  page?: number;
  pageSize?: number;
}

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

export interface DashboardStatusCount {
  status: string;
  count: number;
}

export interface DashboardDailyVolume {
  date: string;
  count: number;
}

export interface DashboardAnalytics {
  totalPatients: number;
  patientsWithConsultations: number;
  patientsWithoutConsultations: number;
  totalConsultations: number;
  unassignedRecordingCount: number;
  processingCount: number;
  completedCount: number;
  failedCount: number;
  averageDurationSeconds?: number;
  statusBreakdown: DashboardStatusCount[];
  consultationsLast14Days: DashboardDailyVolume[];
}

export interface CreateConsultationRequest {
  patientId?: number;
  consultationDate?: string;
  durationSeconds?: number;
  idempotencyKey?: string;
}

export interface Transcript {
  id: number;
  consultationId: number;
  status: string;
  transcript?: string;
  externalJobId?: string;
  processedAt?: string;
  failureReason?: string;
}

export interface ChatQueryRequest {
  patientId?: number;
  message: string;
  sessionId?: string;
}

export interface ChatJob {
  correlationId: string;
  status: string;
  answer?: string;
  citations: string[];
  suggestedActions: string[];
  failureReason?: string;
}

export interface ApiError {
  message: string;
  statusCode?: number;
}

export interface StructuredDataSection {
  type: string;
  label: string;
  fields: StructuredDataField[];
}

export interface StructuredDataField {
  key: string;
  label: string;
  value: string | number | boolean | null;
  confidence?: number;
  source?: string;
}

export interface ParsedStructuredSummary {
  summary?: string;
  sections: StructuredDataSection[];
}

export interface DoctorNote {
  id: number;
  doctorId: string;
  patientId: number;
  consultationId?: number | null;
  content: string;
  dateCreated?: string | null;
  dateModified?: string | null;
}
