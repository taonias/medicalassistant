import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { setAuthToken } from '../../../shared/api/httpClient';
import { recordApiRequests } from '../../../test/recordApiRequests';
import { server } from '../../../test/server';
import { consultationApi } from './consultationApi';

const apiBaseUrl = 'https://backend.test/api';

beforeEach(() => setAuthToken(null));

describe('Consultation upload contract', () => {
  it('keeps every Consultation method and URL unchanged', async () => {
    const { origins, requests } = recordApiRequests();
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:contract');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

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

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
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
    ]);
  });

  it('uploads a Recording with the current URL and multipart field names', async () => {
    let requestContract:
      | { authorization: string | null; contentType: string | null; fields: Record<string, string> }
      | undefined;
    server.use(
      http.post(`${apiBaseUrl}/consultation/42/audio`, async ({ request }) => {
        const formData = await request.formData();
        const audioFile = formData.get('audioFile');
        requestContract = {
          authorization: request.headers.get('Authorization'),
          contentType: request.headers.get('Content-Type'),
          fields: {
            audioFile: audioFile === null ? 'missing' : 'present',
            durationSeconds: String(formData.get('durationSeconds')),
          },
        };
        return HttpResponse.json({ id: 42 });
      }),
    );
    setAuthToken('doctor-token');

    await consultationApi.uploadAudio(
      42,
      new File(['recording'], 'consultation.webm', { type: 'audio/webm' }),
      75,
    );

    expect(requestContract).toEqual({
      authorization: 'Bearer doctor-token',
      contentType: expect.stringMatching(/^multipart\/form-data; boundary=/),
      fields: { audioFile: 'present', durationSeconds: '75' },
    });
  });

  it('uploads a Consultation Document with the current URL and multipart field name', async () => {
    let fields: Record<string, string> | undefined;
    server.use(
      http.post(`${apiBaseUrl}/consultation/42/document`, async ({ request }) => {
        const formData = await request.formData();
        const documentFile = formData.get('documentFile');
        fields = { documentFile: documentFile === null ? 'missing' : 'present' };
        return HttpResponse.json({ id: 42 });
      }),
    );

    await consultationApi.uploadDocument(
      42,
      new File(['document'], 'referral.pdf', { type: 'application/pdf' }),
    );

    expect(fields).toEqual({ documentFile: 'present' });
  });
});
