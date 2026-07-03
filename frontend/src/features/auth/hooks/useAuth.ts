import { useMutation, useQuery } from '@tanstack/react-query';
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

export function useSession() {
  const token = useAuthStore((state) => state.token);

  return useQuery({
    queryKey: queryKeys.session,
    queryFn: authApi.getSession,
    enabled: Boolean(token),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function useLogout() {
  const logout = useAuthStore((state) => state.logout);
  return logout;
}
