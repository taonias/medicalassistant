import { useMutation, useQuery } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { ChatQueryRequest } from '../../../shared/types/api';
import { chatApi } from '../api/chatApi';

export function useChatQuery() {
  return useMutation({
    mutationFn: (request: ChatQueryRequest) => chatApi.query(request),
  });
}

export function useChatStatus(correlationId?: string, enabled = false) {
  return useQuery({
    queryKey: queryKeys.chat(correlationId ?? 'none'),
    queryFn: () => chatApi.getStatus(correlationId!),
    enabled: Boolean(correlationId) && enabled,
    refetchInterval: enabled ? 3000 : false,
  });
}
