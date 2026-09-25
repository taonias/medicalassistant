import { useState } from 'react';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { ConsultationPager } from '../../patients/components/ConsultationPager';
import { useAuthStore } from '../../auth/store/authStore';
import { SETTINGS_PAGE_SIZE, useManagedUsers, useSetUserApproval } from '../hooks/useSettings';

export function UserAccessPanel() {
  const currentUserId = useAuthStore((state) => state.user?.id);
  const [page, setPage] = useState(1);
  const users = useManagedUsers(true, page);
  const setApproval = useSetUserApproval();

  return (
    <section className="panel settings-section">
      <h2>User access</h2>
      <p className="muted">
        Enable or disable doctor accounts. New registrations stay disabled until you enable them.
      </p>

      {users.isLoading ? <p className="muted">Loading users…</p> : null}
      {users.error ? <ErrorMessage message={(users.error as Error).message} /> : null}
      {setApproval.error ? <ErrorMessage message={(setApproval.error as Error).message} /> : null}

      {users.data ? (
        <div className="settings-users">
          {users.data.items.length === 0 ? (
            <p className="muted">No users found.</p>
          ) : (
            <div className="table">
              <div className="table__head settings-users__row">
                <span>Name</span>
                <span>Username</span>
                <span>Email</span>
                <span>Status</span>
              </div>
              {users.data.items.map((managedUser) => {
                const isSelf = managedUser.id === currentUserId;
                const isEnabled = managedUser.isApproved;
                const displayName =
                  `${managedUser.firstName} ${managedUser.lastName}`.trim() || managedUser.userName;
                return (
                  <div key={managedUser.id} className="table__row settings-users__row">
                    <div className="settings-users__identity">
                      <span className="settings-users__name">
                        {managedUser.firstName} {managedUser.lastName}
                      </span>
                      <span className="settings-users__username">{managedUser.userName}</span>
                      <span className="settings-users__email">{managedUser.email}</span>
                    </div>
                    <span className="settings-users__action">
                      <button
                        type="button"
                        className="theme-toggle settings-users__switch"
                        role="switch"
                        aria-checked={isEnabled}
                        aria-label={
                          isEnabled
                            ? `Disable access for ${displayName}`
                            : `Enable access for ${displayName}`
                        }
                        disabled={isSelf || setApproval.isPending}
                        onClick={() =>
                          setApproval.mutate({
                            id: managedUser.id,
                            isApproved: !isEnabled,
                          })
                        }
                      >
                        <span className="theme-toggle__track" aria-hidden="true">
                          <span className="theme-toggle__thumb" />
                        </span>
                        <span className="theme-toggle__label">{isEnabled ? 'Enabled' : 'Pending'}</span>
                      </button>
                    </span>
                  </div>
                );
              })}
            </div>
          )}
          <ConsultationPager
            page={users.data.page}
            pageSize={users.data.pageSize || SETTINGS_PAGE_SIZE}
            totalCount={users.data.totalCount}
            onPageChange={setPage}
            disabled={users.isFetching}
            label="User pages"
            className="consultation-pager--bottom"
          />
        </div>
      ) : null}
    </section>
  );
}
