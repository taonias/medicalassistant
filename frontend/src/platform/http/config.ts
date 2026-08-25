export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7037/api';

export interface HttpAuthConfig {
  /** Reads the current bearer token, or null when signed out. */
  getToken: () => string | null;
  /** Called once when a non-skip-auth request comes back 401 — typically signs the doctor out. */
  onUnauthorized: () => void;
}

let authConfig: HttpAuthConfig = {
  getToken: () => null,
  onUnauthorized: () => {},
};

/**
 * Wires this transport layer to the app's session state. Call once, at app
 * composition (see main.tsx) — nothing under platform/http imports the auth
 * feature directly, which is what breaks the authStore↔httpClient cycle (R29).
 */
export function configureHttpAuth(config: HttpAuthConfig): void {
  authConfig = config;
}

export function resolveAuthToken(skipAuth?: boolean): string | null {
  return skipAuth ? null : authConfig.getToken();
}

export function notifyUnauthorized(): void {
  authConfig.onUnauthorized();
}
