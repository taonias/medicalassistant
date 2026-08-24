import { httpClient } from '../../../../shared/api/httpClient';
import type {
  AskChatResponse,
  ConversationSummary,
  ConversationThread,
} from '../types';

export const conversationApi = {
  listForPatient: (patientId: number) =>
    httpClient<ConversationSummary[]>(`/patients/${patientId}/conversations`),

  getThread: (conversationId: number) =>
    httpClient<ConversationThread>(`/conversations/${conversationId}/messages`),

  create: (request: { patientId?: number; consultationId?: number }) =>
    httpClient<ConversationSummary>('/conversations', {
      method: 'POST',
      body: request,
    }),

  rename: (conversationId: number, title: string) =>
    httpClient<ConversationSummary>(`/conversations/${conversationId}`, {
      method: 'PATCH',
      body: { title },
    }),

  remove: (conversationId: number) =>
    httpClient<void>(`/conversations/${conversationId}`, {
      method: 'DELETE',
    }),

  retry: (conversationId: number, messageId: number) =>
    httpClient<AskChatResponse>(
      `/conversations/${conversationId}/messages/${messageId}/retry`,
      { method: 'POST' },
    ),
};
