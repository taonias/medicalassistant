import { describe, expect, it } from 'vitest';

import { recordApiRequests } from '../../../test/recordApiRequests';
import { patientApi } from './patientApi';

describe('Patient API contract', () => {
  it('keeps every Patient method, URL, and history query unchanged', async () => {
    const { origins, requests } = recordApiRequests();

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

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
      { method: 'GET', path: '/api/patient' },
      { method: 'GET', path: '/api/patient/7' },
      {
        method: 'GET',
        path: '/api/patient/7/history?fromDate=2026-08-01T00%3A00%3A00&toDate=2026-08-23T23%3A59%3A59&page=2&pageSize=25&source=Audio&includeStructuredData=false',
      },
      { method: 'POST', path: '/api/patient' },
      { method: 'PUT', path: '/api/patient' },
    ]);
  });
});
