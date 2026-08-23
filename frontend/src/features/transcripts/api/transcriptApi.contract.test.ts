import { describe, expect, it } from 'vitest';

import { recordApiRequests } from '../../../test/recordApiRequests';
import { transcriptApi } from './transcriptApi';

describe('Transcript API contract', () => {
  it('keeps Transcript read and update URLs unchanged', async () => {
    const { origins, requests } = recordApiRequests();

    await transcriptApi.getByConsultation(42);
    await transcriptApi.update(42, 'Current transcript');

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
      { method: 'GET', path: '/api/transcript/42' },
      { method: 'PUT', path: '/api/transcript/42' },
    ]);
  });
});
