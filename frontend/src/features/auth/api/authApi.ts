import { httpClient } from '../../../shared/api/httpClient';
import type { AuthRequest, AuthResponse, UserSession } from '../types';
import type { ChangePasswordRequest, UpdateUserProfileRequest } from '../../settings';

export const authApi = {
  login: (request: AuthRequest) =>
    httpClient<AuthResponse>('/auth/login', {
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