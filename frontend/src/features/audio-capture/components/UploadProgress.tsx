interface Props {
  progress: number;
  fileName?: string;
}

export function UploadProgress({ progress, fileName }: Props) {
  const clamped = Math.max(0, Math.min(100, progress));

  return (
    <div
      className="upload-transfer"
      role="status"
      aria-live="polite"
      aria-busy="true"
      aria-label={fileName ? `Uploading ${fileName}` : 'Uploading file'}
    >
      <div className="upload-transfer__stage" aria-hidden="true">
        <div className="upload-transfer__node upload-transfer__node--source">
          <svg viewBox="0 0 24 24" fill="none" className="upload-transfer__icon">
            <path
              d="M7 3h7l5 5v13a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinejoin="round"
            />
            <path d="M14 3v5h5" stroke="currentColor" strokeWidth="1.75" strokeLinejoin="round" />
          </svg>
        </div>

        <div className="upload-transfer__path">
          <span className="upload-transfer__rail" />
          <span className="upload-transfer__packet upload-transfer__packet--a" />
          <span className="upload-transfer__packet upload-transfer__packet--b" />
          <span className="upload-transfer__packet upload-transfer__packet--c" />
        </div>

        <div className="upload-transfer__node upload-transfer__node--target">
          <svg viewBox="0 0 24 24" fill="none" className="upload-transfer__icon">
            <path
              d="M7 18h10a4 4 0 0 0 .4-8 5.5 5.5 0 0 0-10.7-1.4A3.5 3.5 0 0 0 7 18Z"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinejoin="round"
            />
            <path
              d="M12 14V9M12 9l-2.2 2.2M12 9l2.2 2.2"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </div>
      </div>

      {fileName ? <p className="upload-transfer__filename muted">{fileName}</p> : null}

      <div className="upload-transfer__bar" aria-hidden="true">
        <div className="upload-transfer__fill" style={{ width: `${clamped}%` }} />
      </div>
    </div>
  );
}
