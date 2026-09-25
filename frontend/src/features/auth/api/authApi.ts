import { httpClient } from '../../../shared/api/httpClient';
import type {
  AuthRequest,
  AuthResponse,
  ChangePasswordRequest,
  RegistrationRequest,
  RegistrationResponse,
  UpdateUserProfileRequest,
  UserSession,
} from '../../../shared/types/api';

export const authApi = {
  login: (request: AuthRequest) =>
    httpClient<AuthResponse>('/auth/login', {
      method: 'POST',
      body: request,
      skipAuth: true,
    }),

  register: (request: RegistrationRequest) =>
    httpClient<RegistrationResponse>('/auth/register', {
      method: 'POST',
      body: request,
      skipAuth: true,
    }),

  getSession: () => httpClient<UserSession>('/auth/session'),

  updateProfile: (request: UpdateUserProfileRequest) =>
    httpClient<UserSession>('/auth/profile', {
      method: 'PUT',
      body: request,
    }),

  changePassword: (request: ChangePasswordRequest) =>
    httpClient<void>('/auth/password', {
      method: 'PUT',
      body: request,
    }),
};