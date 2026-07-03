import { useState } from 'react';

interface Props {
  onFileSelected: (file: File) => void;
  disabled?: boolean;
}

const ACCEPTED_TYPES = ['audio/wav', 'audio/mpeg', 'audio/mp3', 'audio/webm', 'audio/ogg'];

export function AudioUploader({ onFileSelected, disabled }: Props) {
  const [error, setError] = useState<string | null>(null);

  function validateAndSelect(file: File) {
    if (!ACCEPTED_TYPES.includes(file.type) && !file.name.match(/\.(wav|mp3|mpeg|webm|ogg)$/i)) {
      setError('Unsupported audio format. Use WAV, MP3, WebM, or OGG.');
      return;
    }

    setError(null);
    onFileSelected(file);
  }

  return (
    <div className="audio-uploader">
      <label className="upload-dropzone">
        <input
          type="file"
          accept={ACCEPTED_TYPES.join(',')}
          disabled={disabled}
          hidden
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) validateAndSelect(file);
          }}
        />
        <span>Drop audio file here or click to browse</span>
      </label>
      {error ? <p className="field__error">{error}</p> : null}
    </div>
  );
}
