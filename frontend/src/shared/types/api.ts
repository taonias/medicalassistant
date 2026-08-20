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
  consultationId?: number;
  message: string;
  sessionId?: string;
}

export interface ChatResponse {
  answer: string;
  citations: string[];
  suggestedActions: string[];
}

// --- Stateful conversations (doctor ↔ AI) ---

// Enums are serialized as numbers by the backend (System.Text.Json default).
export const MessageRole = { User: 0, Assistant: 1 } as const;
export const MessageState = { Pending: 0, Completed: 1, Refused: 2, Failed: 3 } as const;
export const ConversationStatus = { Active: 0, Archived: 1 } as const;

export interface ChatCitation {
  label: string;
  chunkId: string;
  documentId: string;
  documentType: string;
  sessionId?: string | null;
  documentDate?: string | null;
  sourceRef?: string | null;
  quote: string;
  score: number;
}

export interface AskChatRequest {
  conversationId?: number;
  patientId?: number;
  consultationId?: number;
  question: string;
  askId: string;
}

export interface AskChatResponse {
  conversationId: number;
  title: string;
  messageId: number;
  state: number;
  answer: string;
  language?: string | null;
  refused: boolean;
  failureReason?: string | null;
  askId: string;
  citations: ChatCitation[];
}

export interface ConversationSummary {
  id: number;
  title: string;
  patientId: number;
  consultationId?: number | null;
  status: number;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface ConversationMessage {
  id: number;
  sequence: number;
  role: number;
  content: string;
  state: number;
  language?: string | null;
  failureReason?: string | null;
  askId: string;
  createdAt?: string | null;
  citations: ChatCitation[];
}

export interface ConversationThread {
  conversation: ConversationSummary;
  messages: ConversationMessage[];
}

export interface ChatProgressEvent {
  askId: string;
  phase: string;
  message: string;
  occurredAt: string;
}

export const ActionType = {
  SummarizeConsultation: 0,
  ExtractStructuredData: 1,
  ChatInsight: 2,
  CustomWorkflow: 3,
} as const;

export type ActionType = (typeof ActionType)[keyof typeof ActionType];

export interface TriggerActionRequest {
  actionType: ActionType;
  patientId?: number;
  consultationId?: number;
  parametersJson?: string;
  correlationId?: string;
}

export interface ActionRequest {
  id: number;
  correlationId: string;
  doctorId: string;
  patientId?: number;
  consultationId?: number;
  actionType: string;
  status: string;
  requestPayload?: string;
  responsePayload?: string;
  externalJobId?: string;
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
