import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '../../../shared/constants/queryKeys';
import type { ChangePasswordRequest, UpdateUserProfileRequest } from '../../../shared/types/api';
import { authApi } from '../../auth/api/authApi';
import { useAuthStore } from '../../auth/store/authStore';
import { logsApi } from '../api/logsApi';
import { usersApi } from '../api/usersApi';

export const SETTINGS_PAGE_SIZE = 20;

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const setSession = useAuthStore((state) => state.setSession);
  const roles = useAuthStore((state) => state.roles);

  return useMutation({
    mutationFn: (request: UpdateUserProfileRequest) => authApi.updateProfile(request),
    onSuccess: (session) => {
      setSession(session, roles);
      queryClient.setQueryData(queryKeys.session(session.id), session);
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (request: ChangePasswordRequest) => authApi.changePassword(request),
  });
}

export function useManagedUsers(enabled: boolean, page: number) {
  return useQuery({
    queryKey: queryKeys.users(page, SETTINGS_PAGE_SIZE),
    queryFn: () => usersApi.list({ page, pageSize: SETTINGS_PAGE_SIZE }),
    enabled,
  });
}

export function useSetUserApproval() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isApproved }: { id: string; isApproved: boolean }) =>
      usersApi.setApproval(id, { isApproved }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.usersPrefix });
    },
  });
}

export function useAuditLogs(enabled: boolean, page: number) {
  return useQuery({
    queryKey: queryKeys.auditLogs(page, SETTINGS_PAGE_SIZE),
    queryFn: () => logsApi.audit({ page, pageSize: SETTINGS_PAGE_SIZE }),
    enabled,
  });
}

export function useErrorLogs(enabled: boolean, page: number) {
  return useQuery({
    queryKey: queryKeys.errorLogs(page, SETTINGS_PAGE_SIZE),
    queryFn: () => logsApi.errors({ page, pageSize: SETTINGS_PAGE_SIZE }),
    enabled,
  });
}
