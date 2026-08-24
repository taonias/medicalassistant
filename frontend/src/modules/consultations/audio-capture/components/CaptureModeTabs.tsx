export type CaptureMode = 'record' | 'upload';

type Props = {
  mode: CaptureMode;
  onChange: (mode: CaptureMode) => void;
};

export function CaptureModeTabs({ mode, onChange }: Props) {
  return (
    <div className="capture-tabs" role="tablist" aria-label="Capture method">
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'record'}
        className={mode === 'record' ? 'active' : undefined}
        onClick={() => onChange('record')}
      >
        Record
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'upload'}
        className={mode === 'upload' ? 'active' : undefined}
        onClick={() => onChange('upload')}
      >
        Upload
      </button>
    </div>
  );
}
