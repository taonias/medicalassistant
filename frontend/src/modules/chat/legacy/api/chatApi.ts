import { httpClient } from '../../../../platform/http';
import type { ChatQueryRequest, ChatResponse } from '../types';

export const chatApi = {
  /** Legacy stateless single-shot query (no history). */
  query: (request: ChatQueryRequest) =>
    httpClient<ChatResponse>('/chat/query', {
      method: 'POST',
      body: request,
    }),
};
