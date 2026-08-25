import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './app/App';
import { useAuthStore } from './features/auth';
import { configureHttpAuth } from './platform/http';
import { applyThemeToDocument, useThemeStore } from './features/theme';
import './styles/global.css';

applyThemeToDocument(useThemeStore.getState().theme);

// R29: the transport layer knows nothing about auth internals — app
// composition wires the two together once, here.
configureHttpAuth({
  getToken: () => useAuthStore.getState().token,
  onUnauthorized: () => useAuthStore.getState().logout(),
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
