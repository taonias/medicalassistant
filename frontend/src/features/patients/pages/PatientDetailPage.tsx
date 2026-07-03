import { Link, NavLink, Outlet, useParams } from 'react-router-dom';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { formatDate, formatPatientName } from '../../../shared/utils/format';
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
      <header className="patient-header">
        <div>
          <h1>{formatPatientName(patient.firstName, patient.lastName)}</h1>
          <p className="muted">
            Patient ID {patient.id}
            {patient.externalPatientId ? ` · MRN ${patient.externalPatientId}` : ''}
            {patient.dateOfBirth ? ` · DOB ${formatDate(patient.dateOfBirth)}` : ''}
          </p>
        </div>
        <div className="patient-header__actions">
          <Link to={`${basePath}/consultations/new`} className="button button--primary">
            New consultation
          </Link>
          <Link to={`${basePath}/chat`} className="button button--secondary">
            Chat
          </Link>
        </div>
      </header>

      <nav className="tab-nav" aria-label="Patient sections">
        <NavLink to={basePath} end className={({ isActive }) => (isActive ? 'active' : undefined)}>
          Overview
        </NavLink>
        <NavLink to={`${basePath}/history`} className={({ isActive }) => (isActive ? 'active' : undefined)}>
          History
        </NavLink>
        <NavLink
          to={`${basePath}/structured-data`}
          className={({ isActive }) => (isActive ? 'active' : undefined)}
        >
          Structured data
        </NavLink>
      </nav>

      <Outlet context={{ patientId: patient.id }} />
    </div>
  );
}
