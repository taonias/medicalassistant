interface Props {
  isLoading: boolean;
  summaryText: string | undefined;
  uploadedOnly: boolean;
}

export function SummaryPanel({ isLoading, summaryText, uploadedOnly }: Props) {
  return (
    <section className="panel">
      <h3>Summary</h3>
      {isLoading ? (
        <p className="muted">Loading summary…</p>
      ) : summaryText ? (
        <p>{summaryText}</p>
      ) : (
        <p className="muted">
          {uploadedOnly
            ? 'No summary for this consultation.'
            : 'The summary is still processing.'}
        </p>
      )}
    </section>
  );
}
