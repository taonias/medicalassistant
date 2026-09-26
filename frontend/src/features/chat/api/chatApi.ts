import { httpClient } from '../../../shared/api/httpClient';
import type { ChatJob, ChatQueryRequest } from '../../../shared/types/api';

export const chatApi = {
  query: (request: ChatQueryRequest) =>
    httpClient<ChatJob>('/chat/query', {
      method: 'POST',
      body: request,
    }),

  getStatus: (correlationId: string) => httpClient<ChatJob>(`/chat/${correlationId}`),
};
