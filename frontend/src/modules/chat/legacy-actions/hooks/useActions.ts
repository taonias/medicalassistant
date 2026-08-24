import { useMutation, useQuery } from '@tanstack/react-query';
import { actionKeys } from '../queryKeys';
import type { TriggerActionRequest } from '../types';
import { actionApi } from '../api/actionApi';

export function useTriggerAction() {
  return useMutation({
    mutationFn: (request: TriggerActionRequest) => actionApi.trigger(request),
  });
}

export function useActionStatus(correlationId?: string, enabled = false) {
  return useQuery({
    queryKey: actionKeys.action(correlationId ?? 'none'),
    queryFn: () => actionApi.getStatus(correlationId!),
    enabled: Boolean(correlationId) && enabled,
    refetchInterval: enabled ? 3000 : false,
  });
}
