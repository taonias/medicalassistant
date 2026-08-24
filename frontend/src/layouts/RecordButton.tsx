import type { MouseEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { RecordIcon } from './navigation/NavIcons';
import { useRecordSessionStore } from '../features/record';
import { useActivePatientContext } from '../shared/hooks/useActivePatientContext';

interface Props {
  className?: string;
  label?: string;
  disabled?: boolean;
}

export function RecordButton({ className, label, disabled }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const startNewRecording = useRecordSessionStore((state) => state.startNewRecording);
  const { patientId } = useActivePatientContext();

  const target =
    patientId != null ? `/record?patientId=${patientId}` : '/record';

  function handleClick(event: MouseEvent<HTMLAnchorElement>) {
    event.preventDefault();

    if (disabled) return;

    const onRecordPage = location.pathname === '/record';
    if (onRecordPage) {
      const currentPatient = new URLSearchParams(location.search).get('patientId');
      const nextPatient = patientId != null ? String(patientId) : null;
      if (currentPatient === nextPatient) {
        void startNewRecording();
        return;
      }
    }

    navigate(target);
  }

  const isActive = location.pathname === '/record';

  return (
    <a
      href={target}
      onClick={handleClick}
      aria-disabled={disabled}
      tabIndex={disabled ? -1 : undefined}
      className={[
        'record-button',
        isActive ? 'record-button--active' : undefined,
        disabled ? 'record-button--disabled' : undefined,
        className,
      ]
        .filter(Boolean)
        .join(' ')}
      aria-label={label ? undefined : 'Record consultation'}
      aria-current={isActive ? 'page' : undefined}
    >
      <span className="record-button__ring" aria-hidden="true" />
      <span className="record-button__icon" aria-hidden="true">
        <RecordIcon />
      </span>
      {label ? <span className="record-button__label">{label}</span> : null}
    </a>
  );
}
