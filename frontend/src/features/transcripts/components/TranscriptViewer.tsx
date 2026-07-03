interface Props {
  text?: string;
  status?: string;
  failureReason?: string;
}

export function TranscriptViewer({ text, status, failureReason }: Props) {
  if (failureReason) {
    return (
      <div className="panel">
        <h3>Transcript</h3>
        <p className="field__error">{failureReason}</p>
      </div>
    );
  }

  if (!text) {
    return (
      <div className="panel">
        <h3>Transcript</h3>
        <p className="muted">Transcript {status ? `(${status})` : 'not available yet'}.</p>
      </div>
    );
  }

  return (
    <div className="panel">
      <h3>Transcript</h3>
      <div className="transcript-viewer">{text}</div>
    </div>
  );
}
