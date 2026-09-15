import { consultationStatusLabel } from '../utils/format';

interface Props {
  status: string;
  className?: string;
  /** Also render the status as visible text next to the icon, not just a hover tooltip/aria-label. */
  showLabel?: boolean;
}

// Every status between "a file exists" and a terminal outcome renders as the same
// spinning indicator — the doctor cares that it's still working, not which pipeline
// stage it's in right now.
const PENDING_STATUSES = new Set([
  'AudioUploaded',
  'DocumentUploaded',
  'DocumentProcessingPending',
  'Transcribing',
  'Transcribed',
  'StructuredDataPending',
]);

function normalizeStatus(status: string) {
  return status.replace(/\s+/g, '');
}

export function ConsultationStatusIcon({ status, className, showLabel }: Props) {
  const normalized = normalizeStatus(status);
  const label = consultationStatusLabel(status);
  const isPending = PENDING_STATUSES.has(normalized);

  const badge = (
    <span
      className={[
        'consultation-status-icon',
        `consultation-status-icon--${normalized.toLowerCase()}`,
        isPending ? 'consultation-status-icon--spinning' : undefined,
        className,
      ]
        .filter(Boolean)
        .join(' ')}
      title={showLabel ? undefined : label}
      aria-label={showLabel ? undefined : label}
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

        {isPending ? (
          // A three-quarter ring with an arrowhead — reads as "spinning" once the
          // --spinning class rotates it, and as a static loader if motion is reduced.
          <>
            <path
              d="M20 12a8 8 0 1 1-2.34-5.66"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
            />
            <path
              d="M20 4v4h-4"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
              strokeLinejoin="round"
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
          <path
            d="M7 7l10 10M17 7 7 17"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
          />
        ) : null}

        {!isPending && !['Draft', 'Completed', 'Failed'].includes(normalized) ? (
          <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.75" />
        ) : null}
      </svg>
    </span>
  );

  if (!showLabel) return badge;

  return (
    <span
      className={[
        'consultation-status-badge',
        `consultation-status-icon--${normalized.toLowerCase()}`,
        isPending ? 'consultation-status-icon--spinning' : undefined,
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {badge}
      <span className="consultation-status-badge__label">{label}</span>
    </span>
  );
}
