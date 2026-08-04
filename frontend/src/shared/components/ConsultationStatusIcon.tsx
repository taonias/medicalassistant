import { consultationStatusLabel } from '../utils/format';

interface Props {
  status: string;
  className?: string;
}

function normalizeStatus(status: string) {
  return status.replace(/\s+/g, '');
}

export function ConsultationStatusIcon({ status, className }: Props) {
  const normalized = normalizeStatus(status);
  const label = consultationStatusLabel(status);

  return (
    <span
      className={[
        'consultation-status-icon',
        `consultation-status-icon--${normalized.toLowerCase()}`,
        className,
      ]
        .filter(Boolean)
        .join(' ')}
      title={label}
      aria-label={label}
    >
      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
        {normalized === 'Draft' ? (
          <>
            <path
              d="M7 3h7l5 5v13a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinejoin="round"
            />
            <path d="M14 3v5h5" stroke="currentColor" strokeWidth="1.75" strokeLinejoin="round" />
            <path
              d="M8 13h8M8 17h5"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
            />
          </>
        ) : null}

        {normalized === 'AudioUploaded' ? (
          <>
            <rect x="9" y="3" width="6" height="11" rx="3" stroke="currentColor" strokeWidth="1.75" />
            <path
              d="M6 11a6 6 0 0 0 12 0M12 17v4"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
            />
          </>
        ) : null}

        {normalized === 'DocumentUploaded' || normalized === 'DocumentProcessingPending' ? (
          <>
            <path
              d="M7 3h7l5 5v13a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinejoin="round"
            />
            <path d="M14 3v5h5" stroke="currentColor" strokeWidth="1.75" strokeLinejoin="round" />
          </>
        ) : null}

        {normalized === 'Transcribing' ? (
          <path
            d="M4 12h2l2-5 3 10 2-6 2 3h5"
            stroke="currentColor"
            strokeWidth="1.75"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        ) : null}

        {normalized === 'Transcribed' ? (
          <path
            d="M6 5h12M6 10h12M6 15h8M6 19h5"
            stroke="currentColor"
            strokeWidth="1.75"
            strokeLinecap="round"
          />
        ) : null}

        {normalized === 'StructuredDataPending' ? (
          <>
            <rect x="5" y="4" width="14" height="16" rx="2" stroke="currentColor" strokeWidth="1.75" />
            <path
              d="M9 9h6M9 13h6M9 17h3"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
            />
          </>
        ) : null}

        {normalized === 'Completed' ? (
          <>
            <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.75" />
            <path
              d="m8.5 12.5 2.5 2.5 4.5-5"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </>
        ) : null}

        {normalized === 'Failed' ? (
          <>
            <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.75" />
            <path
              d="M12 8v5M12 16.5h.01"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
            />
          </>
        ) : null}

        {![
          'Draft',
          'AudioUploaded',
          'DocumentUploaded',
          'DocumentProcessingPending',
          'Transcribing',
          'Transcribed',
          'StructuredDataPending',
          'Completed',
          'Failed',
        ].includes(normalized) ? (
          <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.75" />
        ) : null}
      </svg>
    </span>
  );
}
