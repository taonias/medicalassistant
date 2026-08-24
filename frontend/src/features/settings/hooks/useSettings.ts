import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ChangePasswordRequest, UpdateUserProfileRequest } from '../types';
import { authApi, useAuthStore, authKeys } from '../../auth';

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const setSession = useAuthStore((state) => state.setSession);
  const roles = useAuthStore((state) => state.roles);

  return useMutation({
    mutationFn: (request: UpdateUserProfileRequest) => authApi.updateProfile(request),
    onSuccess: (session) => {
      setSession(session, roles);
      queryClient.setQueryData(authKeys.session, session);
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (request: ChangePasswordRequest) => authApi.changePassword(request),
  });
}
