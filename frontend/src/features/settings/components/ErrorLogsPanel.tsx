import { useState } from "react";
import { ErrorMessage } from "../../../shared/components/ErrorMessage";
import { formatDateTime } from "../../../shared/utils/format";
import { ConsultationPager } from "../../patients/components/ConsultationPager";
import { SETTINGS_PAGE_SIZE, useErrorLogs } from "../hooks/useSettings";

export function ErrorLogsPanel() {
  const [expanded, setExpanded] = useState(false);
  const [page, setPage] = useState(1);
  const logs = useErrorLogs(expanded, page);

  return (
    <details
      className="panel collapsible-panel settings-section"
      onToggle={(event) => setExpanded(event.currentTarget.open)}
    >
      <summary className="collapsible-panel__summary">
        <h2>Error log</h2>
      </summary>
      <div className="collapsible-panel__body">
        <p className="muted">Application errors, newest first.</p>

        {logs.isLoading ? <p className="muted">Loading error log…</p> : null}
        {logs.error ? (
          <ErrorMessage message={(logs.error as Error).message} />
        ) : null}

        {logs.data ? (
          <div className="settings-logs">
            {logs.data.items.length === 0 ? (
              <p className="muted">No error entries yet.</p>
            ) : (
              <div className="table">
                <div className="table__head settings-logs__row settings-logs__row--error">
                  <span>Time</span>
                  <span>Request</span>
                  <span>Message</span>
                  <span>Stack</span>
                </div>
                {logs.data.items.map((entry) => (
                  <div
                    key={entry.id}
                    className="table__row settings-logs__row settings-logs__row--error"
                  >
                    <span className="settings-logs__time" data-label="Time">
                      {formatDateTime(entry.timestamp)}
                    </span>
                    <span
                      className="settings-logs__details"
                      data-label="Request"
                    >
                      {[entry.method, entry.path].filter(Boolean).join(" ") ||
                        "—"}
                    </span>
                    <span
                      className="settings-logs__details"
                      data-label="Message"
                    >
                      {entry.message || "—"}
                    </span>
                    <span data-label="Stack">
                      {entry.stackTrace ? (
                        <details className="settings-logs__stack">
                          <summary>Stack trace</summary>
                          <pre>{entry.stackTrace}</pre>
                        </details>
                      ) : (
                        "—"
                      )}
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
              label="Error log pages"
              className="consultation-pager--bottom"
            />
          </div>
        ) : null}
      </div>
    </details>
  );
}
