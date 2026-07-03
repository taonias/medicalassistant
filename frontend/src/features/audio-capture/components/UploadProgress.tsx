interface Props {
  progress: number;
  label?: string;
}

export function UploadProgress({ progress, label = 'Uploading audio…' }: Props) {
  return (
    <div className="upload-progress">
      <div className="upload-progress__label">{label}</div>
      <div className="upload-progress__bar">
        <div className="upload-progress__fill" style={{ width: `${progress}%` }} />
      </div>
    </div>
  );
}
