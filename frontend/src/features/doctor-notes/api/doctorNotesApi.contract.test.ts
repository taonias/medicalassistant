import { describe, expect, it } from 'vitest';

import { recordApiRequests } from '../../../test/recordApiRequests';
import { doctorNotesApi } from './doctorNotesApi';

describe('Doctor Note API contract', () => {
  it('keeps Consultation-level and Patient-level Doctor Note URLs unchanged', async () => {
    const { origins, requests } = recordApiRequests();

    await doctorNotesApi.getByConsultation(42);
    await doctorNotesApi.getPatientLevel(7);
    await doctorNotesApi.create({ consultationId: 42, content: 'Current note' });

    expect(origins).toEqual(new Set(['https://backend.test']));
    expect(requests).toEqual([
      { method: 'GET', path: '/api/doctorNotes/consultations/42' },
      { method: 'GET', path: '/api/doctorNotes/patients/7' },
      { method: 'POST', path: '/api/doctorNotes' },
    ]);
  });
});
