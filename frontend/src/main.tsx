import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './app/App';
import { useAuthStore } from './features/auth/store/authStore';
import { setAuthToken } from './shared/api/httpClient';
import { applyThemeToDocument, useThemeStore } from './features/theme/store/themeStore';
import './styles/global.css';

applyThemeToDocument(useThemeStore.getState().theme);

const storedToken = useAuthStore.getState().token;
if (storedToken) {
  setAuthToken(storedToken);
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
