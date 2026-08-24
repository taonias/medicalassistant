import { httpClient } from '../../../shared/api/httpClient';
import type {
  AskChatRequest,
  AskChatResponse,
  ChatQueryRequest,
  ChatResponse,
} from '../types';

export const chatApi = {
  /** Legacy stateless single-shot query (no history). */
  query: (request: ChatQueryRequest) =>
    httpClient<ChatResponse>('/chat/query', {
      method: 'POST',
      body: request,
    }),

  /** Ask one grounded turn; auto-creates a conversation when conversationId is absent. */
  ask: (request: AskChatRequest) =>
    httpClient<AskChatResponse>('/chat/ask', {
      method: 'POST',
      body: request,
    }),
};
