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

// --- Legacy AI action trigger (pre-dates stateful conversations; see R20) ---

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
