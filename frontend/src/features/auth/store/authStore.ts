import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { setAuthToken } from '../../../shared/api/httpClient';
import type { AuthResponse, UserSession } from '../../../shared/types/api';

interface AuthState {
  token: string | null;
  user: UserSession | null;
  roles: string[];
  setAuth: (response: AuthResponse) => void;
  setSession: (session: UserSession, roles?: string[]) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      user: null,
      roles: [],
      setAuth: (response) => {
        setAuthToken(response.token);
        set({
          token: response.token,
          roles: response.roles,
          user: {
            id: response.id,
            userName: response.userName ?? '',
            email: response.email ?? '',
            emailConfirmed: response.emailConfirmed,
            firstName: response.firstName ?? '',
            lastName: response.lastName ?? '',
          },
        });
      },
      setSession: (session, roles = []) => {
        set({ user: session, roles });
      },
      logout: () => {
        setAuthToken(null);
        set({ token: null, user: null, roles: [] });
      },
    }),
    {
      name: 'medical-assistant-auth',
      partialize: (state) => ({
        token: state.token,
        user: state.user,
        roles: state.roles,
      }),
      onRehydrateStorage: () => (state) => {
        if (state?.token) {
          setAuthToken(state.token);
        }
      },
    },
  ),
);
