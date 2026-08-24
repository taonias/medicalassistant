// Public surface of the auth feature. Other features and the app shell import
// auth state and screens only through here — never by reaching into
// features/auth/{store,hooks,api,pages}/* directly.
export { useAuthStore } from './store/authStore';
export { useLogout, useSession } from './hooks/useAuth';
export { authApi } from './api/authApi';
export { LoginPage } from './pages/LoginPage';
export * from './types';
export { authKeys } from './queryKeys';
