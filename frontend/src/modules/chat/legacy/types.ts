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
