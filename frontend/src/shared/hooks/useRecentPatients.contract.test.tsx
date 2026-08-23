import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { useRecentPatients } from './useRecentPatients';

describe('recent Patient browser-storage contract', () => {
  it('persists the current Patient shape under the existing storage key', () => {
    const patient = {
      id: 7,
      firstName: 'Alex',
      lastName: 'Patient',
      assignedDoctorId: 'doctor-1',
    };
    const { result } = renderHook(() => useRecentPatients());

    act(() => result.current.addRecentPatient(patient));

    expect(JSON.parse(localStorage.getItem('medical-assistant:recent-patients') ?? '[]')).toEqual([
      patient,
    ]);
  });
});
