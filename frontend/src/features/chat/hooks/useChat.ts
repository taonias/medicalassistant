import { useMutation, useQuery } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { ChatQueryRequest, TriggerActionRequest } from '../../../shared/types/api';
import { actionApi } from '../api/actionApi';
import { chatApi } from '../api/chatApi';

export function useChatQuery() {
  return useMutation({
    mutationFn: (request: ChatQueryRequest) => chatApi.query(request),
  });
}

export function useTriggerAction() {
  return useMutation({
    mutationFn: (request: TriggerActionRequest) => actionApi.trigger(request),
  });
}

export function useActionStatus(correlationId?: string, enabled = false) {
  return useQuery({
    queryKey: queryKeys.action(correlationId ?? 'none'),
    queryFn: () => actionApi.getStatus(correlationId!),
    enabled: Boolean(correlationId) && enabled,
    refetchInterval: enabled ? 3000 : false,
  });
}
