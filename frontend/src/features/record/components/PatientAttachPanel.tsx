import { useMemo, useState } from 'react';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { PatientCard } from '../../patients/components/PatientCard';
import { PatientSearchField } from '../../patients/components/PatientSearchField';
import { usePatients } from '../../patients/hooks/usePatients';
import { matchesPatientSearch } from '../../patients/utils/matchesPatientSearch';

interface Props {
  durationSeconds: number;
  isSaving: boolean;
  savingPatientId: number | null;
  saveError?: string | null;
  onPatientSelect: (patientId: number) => void;
  compact?: boolean;
}

function formatDuration(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

export function PatientAttachPanel({
  durationSeconds,
  isSaving,
  savingPatientId,
  saveError,
  onPatientSelect,
  compact = false,
}: Props) {
  const { data: patients, isLoading, error, refetch } = usePatients();
  const [search, setSearch] = useState('');

  const visiblePatients = useMemo(() => {
    if (!patients) return [];
    return patients.filter((patient) => matchesPatientSearch(patient, search));
  }, [patients, search]);

  return (
    <section className={`patient-attach-panel${compact ? ' patient-attach-panel--compact' : ''}`}>
      {compact ? (
        <header className="patient-attach-panel__header">
          <h3>Attach to patient</h3>
        </header>
      ) : (
        <header className="patient-attach-panel__header">
          <h2>Attach to patient</h2>
          <p className="muted">Recording length {formatDuration(durationSeconds)}</p>
        </header>
      )}

      <PatientSearchField value={search} onChange={setSearch} />

      <div className="patient-attach-panel__scroll">
        {isLoading ? (
          <LoadingSkeleton label="Loading patients" />
        ) : error ? (
          <ErrorMessage message={(error as Error).message} onRetry={() => refetch()} />
        ) : visiblePatients.length === 0 ? (
          <p className="muted patient-attach-panel__empty">No patients match your search.</p>
        ) : (
          <div className="patient-card-grid">
            {visiblePatients.map((patient) => (
              <PatientCard
                key={patient.id}
                patient={patient}
                onSelect={onPatientSelect}
                disabled={isSaving}
                isSaving={savingPatientId === patient.id}
              />
            ))}
          </div>
        )}
      </div>

      {saveError ? <ErrorMessage message={saveError} /> : null}
    </section>
  );
}
