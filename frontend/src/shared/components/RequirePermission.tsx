import type { ReactNode } from 'react';
import { useAuthStore } from '../../features/auth/store/authStore';

interface Props {
  permission?: string;
  children: ReactNode;
  fallback?: ReactNode;
}

export function RequirePermission({
  permission,
  children,
  fallback = null,
}: Props) {
  const roles = useAuthStore((state) => state.roles);

  if (!permission) {
    return children;
  }

  const allowed = roles.includes('Admin') || roles.includes(permission);
  return allowed ? children : fallback;
}
