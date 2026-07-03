import { Link, useOutletContext } from 'react-router-dom';
import type { ConsultationHistoryItem } from '../../../shared/types/api';
import { EmptyState } from '../../../shared/components/EmptyState';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { consultationStatusLabel, formatDate } from '../../../shared/utils/format';
import { useConsultationsByPatient } from '../../consultations/hooks/useConsultations';
import { usePatientHistory } from '../hooks/usePatients';

interface PatientOutletContext {
  patientId: number;
}

export function PatientOverviewTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const consultations = useConsultationsByPatient(patientId);
  const history = usePatientHistory(patientId);

  if (consultations.isLoading || history.isLoading) {
    return <LoadingSkeleton label="Loading overview" />;
  }

  if (consultations.error || history.error) {
    return (
      <ErrorMessage
        message="Unable to load patient overview"
        onRetry={() => {
          void consultations.refetch();
          void history.refetch();
        }}
      />
    );
  }

  const latest = consultations.data?.[0];

  return (
    <div className="stack">
      <section className="panel">
        <h2>Latest consultation</h2>
        {!latest ? (
          <EmptyState
            title="No consultations yet"
            description="Start a consultation to record or upload visit audio."
            action={
              <Link to={`/patients/${patientId}/consultations/new`} className="button button--primary">
                New consultation
              </Link>
            }
          />
        ) : (
          <div className="summary-card">
            <div>
              <strong>{formatDate(latest.consultationDate)}</strong>
              <span className="badge">{consultationStatusLabel(latest.status)}</span>
            </div>
            <Link
              to={`/patients/${patientId}/consultations/${latest.id}`}
              className="button button--secondary button--small"
            >
              Open consultation
            </Link>
          </div>
        )}
      </section>

      <section className="panel">
        <h2>Recent history</h2>
        <ul className="item-list">
          {(history.data?.consultations ?? []).slice(0, 3).map((item: ConsultationHistoryItem) => (
            <li key={item.id}>
              <Link
                to={`/patients/${patientId}/consultations/${item.id}`}
                className="item-list__link"
              >
                <span>{formatDate(item.consultationDate)}</span>
                <span className="badge">{consultationStatusLabel(item.status)}</span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

export function PatientHistoryTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const { data, isLoading, error, refetch } = usePatientHistory(patientId);

  if (isLoading) return <LoadingSkeleton label="Loading history" />;
  if (error) {
    return <ErrorMessage message="Unable to load patient history" onRetry={() => refetch()} />;
  }

  return (
    <section className="panel">
      <h2>Consultation timeline</h2>
      {(data?.consultations ?? []).length === 0 ? (
        <EmptyState title="No consultation history" />
      ) : (
        <ul className="timeline">
          {data?.consultations.map((item: ConsultationHistoryItem) => (
            <li key={item.id} className="timeline__item">
              <div className="timeline__meta">
                <strong>{formatDate(item.consultationDate)}</strong>
                <span className="badge">{consultationStatusLabel(item.status)}</span>
              </div>
              {item.transcriptSnippet ? <p>{item.transcriptSnippet}</p> : null}
              {item.structuredSummary ? (
                <p className="muted">Summary: {item.structuredSummary}</p>
              ) : null}
              <Link
                to={`/patients/${patientId}/consultations/${item.id}`}
                className="button button--ghost button--small"
              >
                View details
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function PatientStructuredDataTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const { data, isLoading, error, refetch } = usePatientHistory(patientId);

  if (isLoading) return <LoadingSkeleton label="Loading structured data" />;
  if (error) {
    return <ErrorMessage message="Unable to load structured data" onRetry={() => refetch()} />;
  }

  const items = (data?.consultations ?? []).filter(
    (item: ConsultationHistoryItem) => item.structuredSummary,
  );

  return (
    <section className="panel">
      <h2>Longitudinal structured data</h2>
      {items.length === 0 ? (
        <EmptyState title="No structured data yet" description="Structured extractions appear after consultations are processed." />
      ) : (
        <ul className="timeline">
          {items.map((item) => (
            <li key={item.id} className="timeline__item">
              <strong>{formatDate(item.consultationDate)}</strong>
              <p>{item.structuredSummary}</p>
              <Link
                to={`/patients/${patientId}/consultations/${item.id}`}
                className="button button--ghost button--small"
              >
                Open consultation
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
