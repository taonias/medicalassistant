/**
 * Reads duration in whole seconds from an audio File.
 * Prefers AudioContext decoding — MediaRecorder WebM often reports a bogus
 * ~1s (or Infinity) via HTMLMediaElement metadata until fully seeked.
 */
export async function getAudioDurationSeconds(file: File): Promise<number | undefined> {
  const fromContext = await readDurationViaAudioContext(file);
  if (fromContext != null) return fromContext;

  return readDurationViaAudioElement(file);
}

/** Prefer a decoded duration; fall back to the timer if it's missing or implausibly short. */
export function applyDurationFallback(
  measured: number | undefined,
  timerSeconds: number,
): number | undefined {
  if (measured == null) {
    return timerSeconds > 0 ? timerSeconds : undefined;
  }
  // Guard against the WebM metadata bug that reports ~1s for long recordings.
  if (measured <= 1 && timerSeconds > 2) {
    return timerSeconds;
  }
  return measured;
}

/** Prefer decoded file duration; fall back to the timer if metadata is missing/bogus. */
export async function resolveRecordingDuration(
  file: File,
  timerSeconds: number,
): Promise<number | undefined> {
  const measured = await getAudioDurationSeconds(file);
  return applyDurationFallback(measured, timerSeconds);
}

async function readDurationViaAudioContext(file: File): Promise<number | undefined> {
  const AudioContextCtor =
    window.AudioContext ||
    (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;

  if (!AudioContextCtor) return undefined;

  let context: AudioContext | null = null;
  try {
    const buffer = await file.arrayBuffer();
    context = new AudioContextCtor();
    const decoded = await context.decodeAudioData(buffer.slice(0));
    return normalizeDurationSeconds(decoded.duration);
  } catch {
    return undefined;
  } finally {
    if (context) {
      void context.close();
    }
  }
}

function readDurationViaAudioElement(file: File): Promise<number | undefined> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file);
    const audio = document.createElement('audio');
    audio.preload = 'metadata';

    let settled = false;

    const cleanup = () => {
      audio.onloadedmetadata = null;
      audio.onerror = null;
      audio.removeAttribute('src');
      audio.load();
      URL.revokeObjectURL(url);
    };

    const finish = (seconds?: number) => {
      if (settled) return;
      settled = true;
      cleanup();
      resolve(normalizeDurationSeconds(seconds));
    };

    const timeoutId = window.setTimeout(() => finish(undefined), 8000);

    audio.onerror = () => {
      window.clearTimeout(timeoutId);
      finish(undefined);
    };

    audio.onloadedmetadata = () => {
      if (Number.isFinite(audio.duration) && audio.duration > 0) {
        window.clearTimeout(timeoutId);
        finish(audio.duration);
        return;
      }

      // Chrome/Edge WebM from MediaRecorder often reports Infinity until seeked.
      const onSeeked = () => {
        audio.removeEventListener('seeked', onSeeked);
        audio.removeEventListener('timeupdate', onSeeked);
        window.clearTimeout(timeoutId);
        const duration = audio.duration;
        audio.currentTime = 0;
        // Reject implausibly short results from a premature seek event.
        if (!Number.isFinite(duration) || duration < 0.5) {
          finish(undefined);
          return;
        }
        finish(duration);
      };

      audio.addEventListener('seeked', onSeeked);
      audio.addEventListener('timeupdate', onSeeked);
      try {
        audio.currentTime = 1e101;
      } catch {
        audio.removeEventListener('seeked', onSeeked);
        audio.removeEventListener('timeupdate', onSeeked);
        window.clearTimeout(timeoutId);
        finish(undefined);
      }
    };

    audio.src = url;
  });
}

function normalizeDurationSeconds(duration?: number) {
  if (duration == null || !Number.isFinite(duration) || duration <= 0) {
    return undefined;
  }
  return Math.max(1, Math.round(duration));
}
