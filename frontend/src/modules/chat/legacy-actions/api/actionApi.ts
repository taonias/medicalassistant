import { httpClient } from '../../../../shared/api/httpClient';
import type { ActionRequest, TriggerActionRequest } from '../types';

export const actionApi = {
  trigger: (request: TriggerActionRequest) =>
    httpClient<ActionRequest>('/action/trigger', {
      method: 'POST',
      body: request,
    }),

  getStatus: (correlationId: string) =>
    httpClient<ActionRequest>(`/action/${correlationId}`),
};
