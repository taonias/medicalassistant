import { useState } from 'react';
import { getAudioDurationSeconds } from '../utils/getAudioDuration';

interface Props {
  onFileSelected: (file: File, durationSeconds?: number) => void;
  disabled?: boolean;
}

const AUDIO_TYPES = ['audio/wav', 'audio/mpeg', 'audio/mp3', 'audio/webm', 'audio/ogg'];
const AUDIO_EXTENSIONS = /\.(wav|mp3|mpeg|webm|ogg)$/i;
const PDF_TYPES = ['application/pdf'];
const PDF_EXTENSIONS = /\.pdf$/i;

export function isPdfFile(file: File) {
  return PDF_TYPES.includes(file.type) || PDF_EXTENSIONS.test(file.name);
}

export function isAudioFile(file: File) {
  return AUDIO_TYPES.includes(file.type) || AUDIO_EXTENSIONS.test(file.name);
}

export function AudioUploader({ onFileSelected, disabled }: Props) {
  const [error, setError] = useState<string | null>(null);
  const [isReading, setIsReading] = useState(false);
  const [isDragging, setIsDragging] = useState(false);

  function isAccepted(file: File) {
    return isAudioFile(file) || isPdfFile(file);
  }

  async function validateAndSelect(file: File) {
    if (!isAccepted(file)) {
      setError('Unsupported format. Use WAV, MP3, WebM, OGG, or PDF.');
      return;
    }

    setError(null);
    setIsReading(true);
    try {
      const durationSeconds = isPdfFile(file)
        ? undefined
        : await getAudioDurationSeconds(file);
      onFileSelected(file, durationSeconds);
    } finally {
      setIsReading(false);
    }
  }

  const busy = disabled || isReading;

  return (
    <div className="audio-uploader">
      <label
        className={[
          'upload-dropzone',
          isDragging ? 'upload-dropzone--active' : undefined,
          busy ? 'upload-dropzone--disabled' : undefined,
        ]
          .filter(Boolean)
          .join(' ')}
        onDragEnter={(event) => {
          event.preventDefault();
          if (!busy) setIsDragging(true);
        }}
        onDragOver={(event) => {
          event.preventDefault();
          if (!busy) setIsDragging(true);
        }}
        onDragLeave={(event) => {
          event.preventDefault();
          setIsDragging(false);
        }}
        onDrop={(event) => {
          event.preventDefault();
          setIsDragging(false);
          if (busy) return;
          const file = event.dataTransfer.files?.[0];
          if (file) void validateAndSelect(file);
        }}
      >
        <input
          type="file"
          accept={[
            ...AUDIO_TYPES,
            ...PDF_TYPES,
            '.wav',
            '.mp3',
            '.mpeg',
            '.webm',
            '.ogg',
            '.pdf',
          ].join(',')}
          disabled={busy}
          hidden
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) void validateAndSelect(file);
            event.target.value = '';
          }}
        />
        <span>
          {isReading
            ? 'Reading file…'
            : 'Drop audio or PDF here or click to browse'}
        </span>
      </label>
      {error ? <p className="field__error">{error}</p> : null}
      <p className="muted audio-uploader__hint">
        WAV, MP3, WebM, OGG, or PDF · max 100 MB
      </p>
    </div>
  );
}
