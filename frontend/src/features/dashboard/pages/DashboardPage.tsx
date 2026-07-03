import { Link } from 'react-router-dom';
import { PatientsIcon } from '../../../layouts/navigation/NavIcons';
import { EmptyState } from '../../../shared/components/EmptyState';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { formatDateOfBirth, formatDuration, formatPatientName } from '../../../shared/utils/format';
import { useDraftConsultations } from '../../consultations/hooks/useConsultations';

export function DashboardPage() {
  const { data: draftGroups, isLoading, error, refetch } = useDraftConsultations();

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1>Dashboard</h1>
        </div>
      </header>

      {isLoading ? (
        <LoadingSkeleton label="Loading draft recordings" />
      ) : error ? (
        <ErrorMessage message={(error as Error).message} onRetry={() => refetch()} />
      ) : !draftGroups || draftGroups.length === 0 ? (
        <EmptyState
          title="No draft recordings"
          description="Draft consultations will appear here grouped by patient after you save a recording."
          action={
            <Link to="/record" className="button button--primary">
              Start recording
            </Link>
          }
        />
      ) : (
        <div className="accordion-list">
          {draftGroups.map((group) => (
            <details key={group.patientId} className="accordion-item">
              <summary className="accordion-item__summary">
                <span className="accordion-item__title">
                  {formatPatientName(group.firstName, group.lastName)}
                </span>
                <Link
                  to={`/patients/${group.patientId}`}
                  className="icon-button accordion-item__patient-link"
                  aria-label={`Open ${formatPatientName(group.firstName, group.lastName)} patient page`}
                  onClick={(event) => event.stopPropagation()}
                >
                  <PatientsIcon />
                </Link>
                <span className="accordion-item__meta muted">
                  {group.consultations.length} draft
                  {group.consultations.length === 1 ? '' : 's'}
                </span>
              </summary>

              <ul className="item-list dashboard-recording-list accordion-item__content">
                {group.consultations.map((consultation) => (
                  <li key={consultation.id}>
                    <Link
                      to={`/patients/${group.patientId}/consultations/${consultation.id}`}
                      className="item-list__link dashboard-recording-link"
                    >
                      <span className="dashboard-recording-link__date">
                        {formatDateOfBirth(consultation.consultationDate)}
                      </span>
                      <span className="dashboard-recording-link__duration">
                        {formatDuration(consultation.durationSeconds)}
                      </span>
                      <span className="badge badge--accent dashboard-recording-link__badge">
                        Draft
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </details>
          ))}
        </div>
      )}
    </div>
  );
}
