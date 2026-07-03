import { Link } from 'react-router-dom';
import type { PatientListItem } from '../../../shared/types/api';
import {
  formatDateOfBirth,
  formatDateTime,
  formatPatientName,
} from '../../../shared/utils/format';

interface Props {
  patient: PatientListItem;
  onSelect?: (patientId: number) => void;
  disabled?: boolean;
  isSaving?: boolean;
}

export function PatientCard({ patient, onSelect, disabled, isSaving }: Props) {
  const className = [
    onSelect ? 'patient-card patient-card--button' : 'patient-card',
    isSaving ? 'patient-card--saving' : undefined,
  ]
    .filter(Boolean)
    .join(' ');

  const content = (
    <>
      <header className="patient-card__header">
        <div className="patient-card__meta-item">
          <span className="patient-card__meta-label">Added</span>
          <span className="patient-card__meta-value">
            {formatDateTime(patient.dateCreated)}
          </span>
        </div>
        <div className="patient-card__meta-item">
          <span className="patient-card__meta-label">Last consult</span>
          <span className="patient-card__meta-value">
            {formatDateTime(patient.lastConsultationDate)}
          </span>
        </div>
        <span className="patient-card__id">ID {patient.id}</span>
      </header>

      <div className="patient-card__body">
        <h3 className="patient-card__name">
          {formatPatientName(patient.firstName, patient.lastName)}
        </h3>
        <dl className="patient-card__details">
          <div>
            <dt>Date of birth</dt>
            <dd>{formatDateOfBirth(patient.dateOfBirth)}</dd>
          </div>
          <div>
            <dt>Consultations</dt>
            <dd>{patient.consultationCount}</dd>
          </div>
        </dl>
      </div>

      {!onSelect ? (
        <Link
          to={`/patients/${patient.id}`}
          className="patient-card__overlay-link"
          aria-label={`Open ${formatPatientName(patient.firstName, patient.lastName)}`}
        />
      ) : null}
    </>
  );

  if (onSelect) {
    return (
      <button
        type="button"
        className={className}
        onClick={() => onSelect(patient.id)}
        disabled={disabled}
        aria-label={`Save recording for ${formatPatientName(patient.firstName, patient.lastName)}`}
      >
        {content}
      </button>
    );
  }

  return <article className={className}>{content}</article>;
}
