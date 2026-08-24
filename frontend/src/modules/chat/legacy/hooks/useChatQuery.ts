import { useMutation } from '@tanstack/react-query';
import type { ChatQueryRequest } from '../types';
import { chatApi } from '../api/chatApi';

export function useChatQuery() {
  return useMutation({
    mutationFn: (request: ChatQueryRequest) => chatApi.query(request),
  });
}
