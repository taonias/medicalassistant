interface Props {
  label?: string;
}

export function LoadingSkeleton({ label = 'Loading…' }: Props) {
  return (
    <div className="loading-skeleton" role="status" aria-live="polite">
      <div className="loading-skeleton__bar" />
      <div className="loading-skeleton__bar loading-skeleton__bar--short" />
      <span className="sr-only">{label}</span>
    </div>
  );
}
