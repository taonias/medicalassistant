import { useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import type { ConsultationHistoryItem } from '../types';
import { EmptyState } from '../../../shared/components/EmptyState';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { formatDate } from '../../../shared/utils/format';
import { PatientDetailsPanel } from '../components/PatientDetailsPanel';
import { PatientOverviewCharts } from '../components/PatientOverviewCharts';
import { ConsultationUploadPanel } from '../components/ConsultationUploadPanel';
import { ConsultationFiltersPanel } from '../components/ConsultationFiltersPanel';
import { ConsultationHistoryPanel } from '../components/ConsultationHistoryPanel';
import { PatientDoctorNotesPanel } from '../../../modules/clinical-record';
import { usePatient, usePatientHistory } from '../hooks/usePatients';
import { useConsultationHistoryFilters } from '../hooks/useConsultationHistoryFilters';

interface PatientOutletContext {
  patientId: number;
}

export function PatientOverviewTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const [isEditingDetails, setIsEditingDetails] = useState(false);
  const patientQuery = usePatient(patientId);
  const history = usePatientHistory(patientId);

  const patientSummary = patientQuery.data?.summary?.trim();
  const consultations = history.data?.consultations ?? [];
  const patientDataItems = consultations.filter(
    (item: ConsultationHistoryItem) => item.structuredSummary,
  );

  return (
    <div className="stack">
      {patientQuery.isLoading ? (
        <LoadingSkeleton label="Loading patient details" />
      ) : patientQuery.error || !patientQuery.data ? (
        <ErrorMessage
          message="Unable to load patient details"
          onRetry={() => {
            void patientQuery.refetch();
          }}
        />
      ) : (
        <PatientDetailsPanel
          patient={patientQuery.data}
          isEditing={isEditingDetails}
          onEdit={() => setIsEditingDetails(true)}
          onCancel={() => setIsEditingDetails(false)}
          onSaved={() => setIsEditingDetails(false)}
        />
      )}

      <section className="panel">
        <h2>Patient data</h2>
        {history.isLoading ? (
          <LoadingSkeleton label="Loading patient data" />
        ) : history.error ? (
          <ErrorMessage
            message="Unable to load patient data"
            onRetry={() => {
              void history.refetch();
            }}
          />
        ) : patientDataItems.length === 0 ? (
          <EmptyState
            title="No patient data yet"
            description="Structured extractions appear after consultations are processed."
          />
        ) : (
          <ul className="timeline">
            {patientDataItems.map((item) => (
              <li key={item.id} className="timeline__item timeline__item--block">
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

      <section className="panel">
        <h2>History summary</h2>
        {patientQuery.isLoading ? (
          <LoadingSkeleton label="Loading overview" />
        ) : patientQuery.error ? (
          <ErrorMessage
            message="Unable to load patient overview"
            onRetry={() => {
              void patientQuery.refetch();
            }}
          />
        ) : (
          <p>
            {patientSummary || 'No history summary is available for this patient yet.'}
          </p>
        )}
      </section>

      {history.isLoading ? (
        <section className="panel">
          <h2>Activity</h2>
          <LoadingSkeleton label="Loading activity charts" />
        </section>
      ) : history.error ? null : (
        <PatientOverviewCharts consultations={consultations} />
      )}

      <PatientDoctorNotesPanel patientId={patientId} />
    </div>
  );
}

export function PatientConsultationsTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const { fromDate, toDate, sourceFilter, page, updateDateRange, updateSourceFilter, setPage } =
    useConsultationHistoryFilters();

  return (
    <div className="stack">
      <ConsultationUploadPanel patientId={patientId} />

      <ConsultationFiltersPanel
        fromDate={fromDate}
        toDate={toDate}
        sourceFilter={sourceFilter}
        onDateRangeChange={updateDateRange}
        onSourceFilterChange={updateSourceFilter}
      />

      <ConsultationHistoryPanel
        patientId={patientId}
        fromDate={fromDate}
        toDate={toDate}
        sourceFilter={sourceFilter}
        page={page}
        onPageChange={setPage}
      />
    </div>
  );
}
