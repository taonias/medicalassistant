import { useState } from "react";
import { ErrorMessage } from "../../../shared/components/ErrorMessage";
import { formatDateTime } from "../../../shared/utils/format";
import { ConsultationPager } from "../../patients/components/ConsultationPager";
import { SETTINGS_PAGE_SIZE, useAuditLogs } from "../hooks/useSettings";

export function AuditLogsPanel() {
  const [expanded, setExpanded] = useState(false);
  const [page, setPage] = useState(1);
  const logs = useAuditLogs(expanded, page);

  return (
    <details
      className="panel collapsible-panel settings-section"
      onToggle={(event) => setExpanded(event.currentTarget.open)}
    >
      <summary className="collapsible-panel__summary">
        <h2>Audit log</h2>
      </summary>
      <div className="collapsible-panel__body">
        <p className="muted">Application actions, newest first.</p>

        {logs.isLoading ? <p className="muted">Loading audit log…</p> : null}
        {logs.error ? (
          <ErrorMessage message={(logs.error as Error).message} />
        ) : null}

        {logs.data ? (
          <div className="settings-logs">
            {logs.data.items.length === 0 ? (
              <p className="muted">No audit entries yet.</p>
            ) : (
              <div className="table">
                <div className="table__head settings-logs__row settings-logs__row--audit">
                  <span>Time</span>
                  <span>User</span>
                  <span>Action</span>
                  <span>Entity</span>
                  <span>Details</span>
                </div>
                {logs.data.items.map((entry) => (
                  <div
                    key={entry.id}
                    className="table__row settings-logs__row settings-logs__row--audit"
                  >
                    <span className="settings-logs__time" data-label="Time">
                      {formatDateTime(entry.timestamp)}
                    </span>
                    <span data-label="User">
                      {entry.userName || entry.userId || "—"}
                    </span>
                    <span data-label="Action">{entry.action}</span>
                    <span data-label="Entity">
                      {[entry.entityType, entry.entityId]
                        .filter(Boolean)
                        .join(" ") || "—"}
                    </span>
                    <span
                      className="settings-logs__details"
                      data-label="Details"
                    >
                      {entry.details || "—"}
                    </span>
                  </div>
                ))}
              </div>
            )}
            <ConsultationPager
              page={logs.data.page}
              pageSize={logs.data.pageSize || SETTINGS_PAGE_SIZE}
              totalCount={logs.data.totalCount}
              onPageChange={setPage}
              disabled={logs.isFetching}
              label="Audit log pages"
              className="consultation-pager--bottom"
            />
          </div>
        ) : null}
      </div>
    </details>
  );
}
