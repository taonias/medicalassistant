import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { setAuthToken } from '../../../shared/api/httpClient';
import { server } from '../../../test/server';
import { consultationApi } from './consultationApi';

const apiBaseUrl = 'https://backend.test/api';

beforeEach(() => setAuthToken(null));

describe('Consultation upload contract', () => {
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
