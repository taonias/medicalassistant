import type { MouseEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { RecordIcon } from './navigation/NavIcons';
import { useRecordSessionStore } from '../features/record/store/recordSessionStore';

interface Props {
  to?: string;
  className?: string;
  label?: string;
  disabled?: boolean;
}

export function RecordButton({ to = '/record', className, label, disabled }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const startNewRecording = useRecordSessionStore((state) => state.startNewRecording);

  function handleClick(event: MouseEvent<HTMLAnchorElement>) {
    event.preventDefault();

    if (disabled) return;

    if (location.pathname === to) {
      startNewRecording();
      return;
    }

    navigate(to);
  }

  const isActive = location.pathname === to;

  return (
    <a
      href={to}
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
