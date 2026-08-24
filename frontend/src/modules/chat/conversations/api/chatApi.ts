import { httpClient } from '../../../../shared/api/httpClient';
import type { AskChatRequest, AskChatResponse } from '../types';

export const chatApi = {
  /** Ask one grounded turn; auto-creates a conversation when conversationId is absent. */
  ask: (request: AskChatRequest) =>
    httpClient<AskChatResponse>('/chat/ask', {
      method: 'POST',
      body: request,
    }),
};
