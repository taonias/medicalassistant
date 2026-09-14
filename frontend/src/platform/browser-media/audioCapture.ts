import { buildRecordingFile, preferredRecordingMimeType, setMicrophoneEnabled } from './mimeAndFile';

export interface AudioCaptureHandlers {
  /** Fires once per second while actively recording (not while paused). */
  onTick?: (elapsedSeconds: number) => void;
  /** Fires if the underlying MediaRecorder reports an error after starting. */
  onError?: () => void;
  /**
   * Fires when the input transitions to/from sustained silence — either the
   * track itself reports `muted` (the OS/device stopped delivering samples,
   * e.g. another app grabbed the mic) or the measured level has stayed near
   * zero for several seconds. Never fires while paused.
   */
  onSilenceChange?: (isSilent: boolean) => void;
}

export interface AudioCaptureSession {
  pause(): void;
  resume(): void;
  /** Finalizes the recording; resolves the built File (or null if empty/never started). */
  stop(): Promise<File | null>;
  /** Hard teardown without finalizing a file (unmount / reset / re-record). */
  dispose(): void;
  /**
   * Live input snapshot bucketed into `barCount` bars (each 0..1), for
   * driving a real-time "audio is being captured" visualization. Cheap
   * enough to call every animation frame. All-zero when metering is
   * unavailable (unsupported browser) or nothing is currently audible.
   */
  getSpectrum(barCount: number): number[];
}

// Tuned for voice: a few seconds of near-zero signal before warning, so a
// natural pause between sentences doesn't trigger a false positive.
const SILENCE_LEVEL_THRESHOLD = 0.02;
const SILENCE_HOLD_TICKS = 4;

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

  const meter = createLevelMeter(stream);
  let meterLive = true;

  let silentTicks = 0;
  let reportedSilent = false;

  const reportSilence = (isSilent: boolean) => {
    if (reportedSilent === isSilent) return;
    reportedSilent = isSilent;
    handlers.onSilenceChange?.(isSilent);
  };

  // Belt-and-suspenders: a `muted` track (OS/device stopped delivering
  // samples) is a stronger, immediate signal than waiting out the
  // level-based hold below.
  const audioTrack = stream.getAudioTracks()[0];
  const handleTrackMute = () => {
    silentTicks = SILENCE_HOLD_TICKS;
    reportSilence(true);
  };
  const handleTrackUnmute = () => {
    silentTicks = 0;
    reportSilence(false);
  };
  audioTrack?.addEventListener('mute', handleTrackMute);
  audioTrack?.addEventListener('unmute', handleTrackUnmute);

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

      const level = meter?.getLevel();
      if (level != null) {
        silentTicks = level < SILENCE_LEVEL_THRESHOLD ? silentTicks + 1 : 0;
        reportSilence(silentTicks >= SILENCE_HOLD_TICKS);
      }
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

  const teardownMetering = () => {
    audioTrack?.removeEventListener('mute', handleTrackMute);
    audioTrack?.removeEventListener('unmute', handleTrackUnmute);
    meterLive = false;
    meter?.dispose();
  };

  return {
    pause() {
      // Soft-pause: keep MediaRecorder running so the container stays valid.
      setMicrophoneEnabled(stream, false);
      clearTimer();
    },

    resume() {
      setMicrophoneEnabled(stream, true);
      silentTicks = 0;
      reportSilence(false);
      startTimer();
    },

    stop() {
      clearTimer();
      setMicrophoneEnabled(stream, true);

      if (recorder.state === 'inactive') {
        stopTracks();
        teardownMetering();
        return Promise.resolve(null);
      }

      return new Promise<File | null>((resolve) => {
        recorder.onstop = () => {
          const file = buildRecordingFile(chunks, recorder.mimeType || mimeType);
          stopTracks();
          teardownMetering();
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
      teardownMetering();
      chunks = [];
    },

    getSpectrum(barCount) {
      if (!meterLive) return new Array(barCount).fill(0);
      return meter?.getSpectrum(barCount) ?? new Array(barCount).fill(0);
    },
  };
}

interface LevelMeter {
  /** Overall 0..1 signal level for this instant, used for silence detection. */
  getLevel(): number;
  /** Frequency spectrum bucketed into `barCount` 0..1 bars, for visualization. */
  getSpectrum(barCount: number): number[];
  dispose(): void;
}

/** Wraps a Web Audio analyser tapped off the mic stream; null if unsupported. */
function createLevelMeter(stream: MediaStream): LevelMeter | null {
  const AudioContextCtor =
    window.AudioContext ||
    (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioContextCtor) return null;

  let context: AudioContext;
  try {
    context = new AudioContextCtor();
  } catch {
    return null;
  }

  const source = context.createMediaStreamSource(stream);
  const analyser = context.createAnalyser();
  analyser.fftSize = 256;
  analyser.smoothingTimeConstant = 0.6;
  // Tap the signal for metering only — never connect to context.destination,
  // that would loop the mic back out to the speakers.
  source.connect(analyser);

  const freqData = new Uint8Array(analyser.frequencyBinCount);
  const timeData = new Uint8Array(analyser.fftSize);

  return {
    getLevel() {
      analyser.getByteTimeDomainData(timeData);
      let sumSquares = 0;
      for (let i = 0; i < timeData.length; i += 1) {
        const centered = (timeData[i] - 128) / 128;
        sumSquares += centered * centered;
      }
      return Math.sqrt(sumSquares / timeData.length);
    },

    getSpectrum(barCount) {
      analyser.getByteFrequencyData(freqData);
      // Voice energy concentrates in the lower/mid bins; ignore the sparse
      // top of the range so the bars read as an active spectrum rather than
      // mostly-empty on the right.
      const usableBins = Math.max(barCount, Math.floor(freqData.length * 0.75));
      const binsPerBar = Math.max(1, Math.floor(usableBins / barCount));
      const bars: number[] = [];
      for (let i = 0; i < barCount; i += 1) {
        const start = i * binsPerBar;
        let sum = 0;
        for (let j = 0; j < binsPerBar; j += 1) {
          sum += freqData[start + j] ?? 0;
        }
        bars.push(sum / binsPerBar / 255);
      }
      return bars;
    },

    dispose() {
      try {
        source.disconnect();
        analyser.disconnect();
      } catch {
        // ignore teardown errors
      }
      void context.close().catch(() => {
        // ignore — context may already be closed
      });
    },
  };
}
