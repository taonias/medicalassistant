import { buildRecordingFile, preferredRecordingMimeType, setMicrophoneEnabled } from './mimeAndFile';

export interface AudioCaptureHandlers {
  /** Fires once per second while actively recording (not while paused). */
  onTick?: (elapsedSeconds: number) => void;
  /** Fires if the underlying MediaRecorder reports an error after starting. */
  onError?: () => void;
}

export interface AudioCaptureSession {
  pause(): void;
  resume(): void;
  /** Finalizes the recording; resolves the built File (or null if empty/never started). */
  stop(): Promise<File | null>;
  /** Hard teardown without finalizing a file (unmount / reset / re-record). */
  dispose(): void;
}

/**
 * Starts a microphone recording session: getUserMedia + MediaRecorder + a
 * one-second tick timer, encapsulated so callers hold a single handle
 * instead of separately tracked stream/recorder/chunk/timer state.
 *
 * Does not pre-check getUserMedia support or catch its rejection — callers
 * decide their own pre-flight checks and error messaging.
 */
export async function startAudioCapture(
  handlers: AudioCaptureHandlers = {},
): Promise<AudioCaptureSession> {
  const stream = await navigator.mediaDevices.getUserMedia({
    audio: {
      echoCancellation: true,
      noiseSuppression: true,
      channelCount: 1,
    },
  });

  let chunks: BlobPart[] = [];
  let timerId: number | null = null;
  let disposed = false;
  // Cumulative across pause/resume — only ever reset by starting a new
  // session, matching both callers' original "keep counting up" behavior.
  let elapsed = 0;

  const clearTimer = () => {
    if (timerId !== null) {
      window.clearInterval(timerId);
      timerId = null;
    }
  };

  const startTimer = () => {
    clearTimer();
    timerId = window.setInterval(() => {
      elapsed += 1;
      handlers.onTick?.(elapsed);
    }, 1000);
  };

  const mimeType = preferredRecordingMimeType();
  const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);

  recorder.ondataavailable = (event) => {
    if (event.data.size > 0) chunks.push(event.data);
  };

  recorder.onerror = () => {
    handlers.onError?.();
  };

  // No timeslice — a single blob on stop is far more reliably playable than chunked WebM.
  recorder.start();
  startTimer();

  const stopTracks = () => {
    stream.getTracks().forEach((track) => track.stop());
  };

  return {
    pause() {
      // Soft-pause: keep MediaRecorder running so the container stays valid.
      setMicrophoneEnabled(stream, false);
      clearTimer();
    },

    resume() {
      setMicrophoneEnabled(stream, true);
      startTimer();
    },

    stop() {
      clearTimer();
      setMicrophoneEnabled(stream, true);

      if (recorder.state === 'inactive') {
        stopTracks();
        return Promise.resolve(null);
      }

      return new Promise<File | null>((resolve) => {
        recorder.onstop = () => {
          const file = buildRecordingFile(chunks, recorder.mimeType || mimeType);
          stopTracks();
          resolve(file);
        };

        try {
          recorder.requestData();
        } catch {
          // requestData is best-effort; stop still finalizes the blob.
        }

        recorder.stop();
      });
    },

    dispose() {
      if (disposed) return;
      disposed = true;
      clearTimer();
      recorder.ondataavailable = null;
      recorder.onerror = null;
      recorder.onstop = null;
      if (recorder.state !== 'inactive') {
        try {
          recorder.stop();
        } catch {
          // ignore teardown errors
        }
      }
      stopTracks();
      chunks = [];
    },
  };
}
