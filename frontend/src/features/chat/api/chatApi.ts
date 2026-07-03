import { httpClient } from '../../../shared/api/httpClient';
import type { ChatQueryRequest, ChatResponse } from '../../../shared/types/api';

export const chatApi = {
  query: (request: ChatQueryRequest) =>
    httpClient<ChatResponse>('/chat/query', {
      method: 'POST',
      body: request,
    }),
};
