import { useEffect } from 'react';
import { Outlet } from 'react-router-dom';
import { useLogout, useSession, useAuthStore } from '../features/auth';
import { selectIsRecordingLocked, useRecordSessionStore } from '../features/record';
import { AppBreadcrumbs } from '../shared/components/AppBreadcrumbs';
import { AppBrand } from '../shared/components/AppBrand';
import { FooterNav } from './FooterNav';
import { SignOutIcon } from './navigation/NavIcons';
import { SideNav } from './SideNav';
import { UserMenu } from './UserMenu';

export function AppShell() {
  const setSession = useAuthStore((state) => state.setSession);
  const logout = useLogout();
  const session = useSession();
  const isRecordingLocked = useRecordSessionStore(selectIsRecordingLocked);

  useEffect(() => {
    if (session.data) {
      setSession(session.data);
    }
  }, [session.data, setSession]);

  return (
    <div className={`app-shell${isRecordingLocked ? ' app-shell--recording-locked' : ''}`}>
      <aside className="app-shell__sidebar" aria-label="Application sidebar">
        <AppBrand variant="vertical" className="app-shell__brand" />
        <SideNav />
        <div className="app-shell__sidebar-footer">
          <UserMenu variant="sidebar" />
          <button
            type="button"
            className="icon-button icon-button--sidebar icon-button--danger"
            onClick={logout}
            disabled={isRecordingLocked}
            aria-label="Sign out"
            title="Sign out"
          >
            <SignOutIcon />
          </button>
        </div>
      </aside>

      <div className="app-shell__main">
        <div className="app-shell__page-header">
          <header className="app-shell__topbar">
            <div className="app-shell__topbar-start">
              <AppBrand variant="horizontal" className="app-shell__mobile-brand" />
            </div>
            <div className="app-shell__user app-shell__user--mobile">
              <UserMenu />
              <button
                type="button"
                className="icon-button icon-button--danger"
                onClick={logout}
                disabled={isRecordingLocked}
                aria-label="Sign out"
                title="Sign out"
              >
                <SignOutIcon />
              </button>
            </div>
          </header>

          <div className="app-shell__breadcrumb-bar">
            <AppBreadcrumbs />
          </div>
        </div>

        <main className="app-shell__content" id="main-content">
          <Outlet />
        </main>

        <FooterNav />
      </div>
    </div>
  );
}
