import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { actionApi } from '../features/chat/api/actionApi';
import { chatApi } from '../features/chat/api/chatApi';
import { conversationApi } from '../features/chat/api/conversationApi';
import { consultationApi } from '../features/consultations/api/consultationApi';
import { doctorNotesApi } from '../features/doctor-notes/api/doctorNotesApi';
import { structuredDataApi } from '../features/medical-data/api/structuredDataApi';
import { patientApi } from '../features/patients/api/patientApi';
import { transcriptApi } from '../features/transcripts/api/transcriptApi';
import { authApi } from '../features/auth/api/authApi';
import { ActionType } from '../shared/types/api';
import { server } from './server';

type RequestContract = { method: string; path: string };

describe('frontend API URL contract', () => {
  it('keeps every feature API method and URL unchanged', async () => {
    const requests: RequestContract[] = [];
    server.use(
      http.all('*', ({ request }) => {
        const url = new URL(request.url);
        requests.push({ method: request.method, path: `${url.pathname}${url.search}` });
        return HttpResponse.json({});
      }),
    );
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:contract');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    await authApi.login({ userName: 'doctor', password: 'secret' });
    await authApi.getSession();
    await authApi.updateProfile({
      firstName: 'Test',
      lastName: 'Doctor',
      email: 'doctor@example.test',
    });
    await authApi.changePassword({ currentPassword: 'old', newPassword: 'new' });

    await patientApi.list();
    await patientApi.getById(7);
    await patientApi.getHistory(7, {
      fromDate: '2026-08-01',
      toDate: '2026-08-23',
      page: 2,
      pageSize: 25,
      source: 'Audio',
      includeStructuredData: false,
    });
    await patientApi.create({ firstName: 'Alex', lastName: 'Patient' });
    await patientApi.update({ id: 7, firstName: 'Alex', lastName: 'Patient' });

    await consultationApi.getById(42);
    await consultationApi.getByPatient(7);
    await consultationApi.getDrafts();
    await consultationApi.getUnattachedDrafts();
    await consultationApi.getAnalytics();
    await consultationApi.create({ patientId: 7 }, 'idempotency-key');
    await consultationApi.assignPatient(42, 7);
    await consultationApi.retryProcessing(42);
    await consultationApi.uploadAudio(42, new File(['audio'], 'recording.webm'), 10);
    await consultationApi.uploadDocument(42, new File(['pdf'], 'document.pdf'));
    await consultationApi.getAudioObjectUrl(42);
    await consultationApi.downloadDocument(42);
    await consultationApi.downloadAudio(42);
    await consultationApi.delete(42);

    await transcriptApi.getByConsultation(42);
    await transcriptApi.update(42, 'Current transcript');
    await doctorNotesApi.getByConsultation(42);
    await doctorNotesApi.getPatientLevel(7);
    await doctorNotesApi.create({ consultationId: 42, content: 'Current note' });
    await structuredDataApi.getByConsultation(42);
    await structuredDataApi.approve(42);
    await chatApi.query({ patientId: 7, message: 'Question' });
    await chatApi.ask({ patientId: 7, question: 'Question', askId: 'ask-1' });
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

    expect(requests).toEqual([
      { method: 'POST', path: '/api/auth/login' },
      { method: 'GET', path: '/api/auth/session' },
      { method: 'PUT', path: '/api/auth/profile' },
      { method: 'PUT', path: '/api/auth/password' },
      { method: 'GET', path: '/api/patient' },
      { method: 'GET', path: '/api/patient/7' },
      {
        method: 'GET',
        path: '/api/patient/7/history?fromDate=2026-08-01T00%3A00%3A00&toDate=2026-08-23T23%3A59%3A59&page=2&pageSize=25&source=Audio&includeStructuredData=false',
      },
      { method: 'POST', path: '/api/patient' },
      { method: 'PUT', path: '/api/patient' },
      { method: 'GET', path: '/api/consultation/42' },
      { method: 'GET', path: '/api/consultation/patient/7' },
      { method: 'GET', path: '/api/consultation/drafts' },
      { method: 'GET', path: '/api/consultation/drafts/unattached' },
      { method: 'GET', path: '/api/consultation/analytics' },
      { method: 'POST', path: '/api/consultation' },
      { method: 'PUT', path: '/api/consultation/42/patient' },
      { method: 'POST', path: '/api/consultation/42/retry' },
      { method: 'POST', path: '/api/consultation/42/audio' },
      { method: 'POST', path: '/api/consultation/42/document' },
      { method: 'GET', path: '/api/consultation/42/audio' },
      { method: 'GET', path: '/api/consultation/42/document' },
      { method: 'GET', path: '/api/consultation/42/audio' },
      { method: 'DELETE', path: '/api/consultation/42' },
      { method: 'GET', path: '/api/transcript/42' },
      { method: 'PUT', path: '/api/transcript/42' },
      { method: 'GET', path: '/api/doctorNotes/consultations/42' },
      { method: 'GET', path: '/api/doctorNotes/patients/7' },
      { method: 'POST', path: '/api/doctorNotes' },
      { method: 'GET', path: '/api/consultation/42/structured-data' },
      { method: 'POST', path: '/api/consultation/42/structured-data/approve' },
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
