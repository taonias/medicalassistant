import { describe, expect, it } from 'vitest';

import { recordApiRequests } from '../../../../test/recordApiRequests';
import { structuredDataApi } from './structuredDataApi';

describe('Structured Medical Data API contract', () => {
  it('keeps retrieval and approval URLs unchanged', async () => {
    const { origins, requests } = recordApiRequests();

    await structuredDataApi.getByConsultation(42);
    await structuredDataApi.approve(42);

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
      { method: 'GET', path: '/api/consultation/42/structured-data' },
      { method: 'POST', path: '/api/consultation/42/structured-data/approve' },
    ]);
  });
});
