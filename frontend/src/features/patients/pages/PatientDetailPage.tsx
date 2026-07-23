import { NavLink, Outlet, useParams } from 'react-router-dom';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { usePatient } from '../hooks/usePatients';

export function PatientDetailPage() {
  const { patientId = '0' } = useParams();
  const id = Number(patientId);
  const { data: patient, isLoading, error, refetch } = usePatient(id);

  if (isLoading) return <LoadingSkeleton label="Loading patient" />;
  if (error || !patient) {
    return (
      <ErrorMessage
        message={(error as Error)?.message ?? 'Patient not found'}
        onRetry={() => refetch()}
      />
    );
  }

  const basePath = `/patients/${patient.id}`;

  return (
    <div className="page">
      <nav className="tab-nav" aria-label="Patient sections">
        <NavLink to={basePath} end className={({ isActive }) => (isActive ? 'active' : undefined)}>
          Overview
        </NavLink>
        <NavLink
          to={`${basePath}/history`}
          className={({ isActive }) => (isActive ? 'active' : undefined)}
        >
          Consultations
        </NavLink>
      </nav>

      <Outlet context={{ patientId: patient.id }} />
    </div>
  );
}
