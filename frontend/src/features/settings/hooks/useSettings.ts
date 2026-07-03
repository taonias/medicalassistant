import { useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { ChangePasswordRequest, UpdateUserProfileRequest } from '../../../shared/types/api';
import { authApi } from '../../auth/api/authApi';
import { useAuthStore } from '../../auth/store/authStore';

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const setSession = useAuthStore((state) => state.setSession);
  const roles = useAuthStore((state) => state.roles);

  return useMutation({
    mutationFn: (request: UpdateUserProfileRequest) => authApi.updateProfile(request),
    onSuccess: (session) => {
      setSession(session, roles);
      queryClient.setQueryData(queryKeys.session, session);
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (request: ChangePasswordRequest) => authApi.changePassword(request),
  });
}
