import { describe, expect, it } from 'vitest';

import { useAuthStore } from './authStore';

const authenticatedDoctor = {
  id: 'doctor-1',
  userName: 'doctor',
  email: 'doctor@example.test',
  emailConfirmed: true,
  firstName: 'Test',
  lastName: 'Doctor',
  token: 'doctor-token',
  roles: ['Doctor'],
};

describe('Auth store persistence', () => {
  it('persists authentication under the existing browser storage key', () => {
    useAuthStore.getState().setAuth(authenticatedDoctor);

    expect(JSON.parse(localStorage.getItem('medical-assistant-auth') ?? '{}')).toMatchObject({
      state: {
        token: 'doctor-token',
        roles: ['Doctor'],
      },
    });
  });
});
