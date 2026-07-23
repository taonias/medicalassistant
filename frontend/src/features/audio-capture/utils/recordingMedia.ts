/** Prefer a widely playable MediaRecorder MIME type. */
export function preferredRecordingMimeType() {
  if (typeof MediaRecorder === 'undefined') return '';
  if (MediaRecorder.isTypeSupported('audio/webm;codecs=opus')) {
    return 'audio/webm;codecs=opus';
  }
  if (MediaRecorder.isTypeSupported('audio/webm')) return 'audio/webm';
  if (MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')) {
    return 'audio/ogg;codecs=opus';
  }
  return '';
}

/** Build a File from MediaRecorder chunks using the recorder’s declared MIME type. */
export function buildRecordingFile(chunks: BlobPart[], mimeType: string) {
  const fullType = mimeType || 'audio/webm';
  const baseType = fullType.split(';', 1)[0]?.trim() || 'audio/webm';
  const extension = baseType.includes('ogg') ? 'ogg' : 'webm';
  // Keep codec parameters on the Blob — browsers decode WebM/Opus more reliably with them.
  const blob = new Blob(chunks, { type: fullType });
  if (blob.size <= 0) return null;
  return new File([blob], `consultation-${Date.now()}.${extension}`, { type: fullType });
}

/** Soft-pause: mute tracks so MediaRecorder keeps a continuous, playable container. */
export function setMicrophoneEnabled(stream: MediaStream | null, enabled: boolean) {
  stream?.getAudioTracks().forEach((track) => {
    track.enabled = enabled;
  });
}
