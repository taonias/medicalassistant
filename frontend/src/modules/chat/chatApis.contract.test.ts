import { describe, expect, it } from 'vitest';

import { recordApiRequests } from '../../test/recordApiRequests';
import { ActionType } from './legacy-actions/types';
import { actionApi } from './legacy-actions/api/actionApi';
import { chatApi as legacyChatApi } from './legacy/api/chatApi';
import { chatApi as conversationChatApi } from './conversations/api/chatApi';
import { conversationApi } from './conversations/api/conversationApi';

describe('Chat Query and Action Request API contract', () => {
  it('keeps Chat Query, conversation, and Action Request URLs unchanged', async () => {
    const { origins, requests } = recordApiRequests();

    await legacyChatApi.query({ patientId: 7, message: 'Question' });
    await conversationChatApi.ask({ patientId: 7, question: 'Question', askId: 'ask-1' });
    await conversationApi.listForPatient(7);
    await conversationApi.getThread(5);
    await conversationApi.create({ patientId: 7 });
    await conversationApi.rename(5, 'Follow-up');
    await conversationApi.remove(5);
    await conversationApi.retry(5, 9);
    await actionApi.trigger({
      actionType: ActionType.SummarizeConsultation,
      patientId: 7,
      consultationId: 42,
    });
    await actionApi.getStatus('correlation-1');

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
      { method: 'POST', path: '/api/chat/query' },
      { method: 'POST', path: '/api/chat/ask' },
      { method: 'GET', path: '/api/patients/7/conversations' },
      { method: 'GET', path: '/api/conversations/5/messages' },
      { method: 'POST', path: '/api/conversations' },
      { method: 'PATCH', path: '/api/conversations/5' },
      { method: 'DELETE', path: '/api/conversations/5' },
      { method: 'POST', path: '/api/conversations/5/messages/9/retry' },
      { method: 'POST', path: '/api/action/trigger' },
      { method: 'GET', path: '/api/action/correlation-1' },
    ]);
  });
});
