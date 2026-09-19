import { buildTrimmedWav, type TimeRangeMs } from './pcmTrim';

function readAsArrayBuffer(file: File): Promise<ArrayBuffer> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(file);
  });
}

/**
 * Decodes a recorded file and re-encodes it as 16kHz mono WAV with the given
 * paused time ranges cut out (the soft-pause approach keeps MediaRecorder
 * running, so paused time is otherwise baked into the file as silence).
 * Returns null when decoding isn't possible — trimming is a best-effort
 * improvement, not something a completed recording should fail over.
 */
export async function trimPausedAudio(file: File, pausedRangesMs: TimeRangeMs[]): Promise<File | null> {
  const AudioContextCtor =
    window.AudioContext ||
    (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioContextCtor) return null;

  let context: AudioContext | null = null;
  try {
    const arrayBuffer = await readAsArrayBuffer(file);
    context = new AudioContextCtor();
    const decoded = await context.decodeAudioData(arrayBuffer);
    const wavBlob = buildTrimmedWav(decoded, pausedRangesMs);
    return new File([wavBlob], `consultation-${Date.now()}.wav`, { type: 'audio/wav' });
  } catch {
    return null;
  } finally {
    if (context) void context.close();
  }
}
