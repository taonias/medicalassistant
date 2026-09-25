import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { queryKeys } from '../../../shared/constants/queryKeys';
import { authApi } from '../api/authApi';
import { useAuthStore } from '../store/authStore';

export function useLogin() {
  const setAuth = useAuthStore((state) => state.setAuth);

  return useMutation({
    mutationFn: authApi.login,
    onSuccess: setAuth,
  });
}

export function useRegister() {
  return useMutation({
    mutationFn: authApi.register,
  });
}

export function useSession() {
  const token = useAuthStore((state) => state.token);
  const userId = useAuthStore((state) => state.user?.id);

  return useQuery({
    queryKey: queryKeys.session(userId ?? ''),
    queryFn: authApi.getSession,
    enabled: Boolean(token && userId),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function useLogout() {
  const logout = useAuthStore((state) => state.logout);
  const queryClient = useQueryClient();

  return useCallback(() => {
    logout();
    queryClient.clear();
  }, [logout, queryClient]);
}
