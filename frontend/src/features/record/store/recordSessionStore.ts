import { create } from 'zustand';
import {
  buildRecordingFile,
  preferredRecordingMimeType,
  setMicrophoneEnabled,
} from '../../audio-capture';

export type RecordPhase = 'idle' | 'recording' | 'attach';

export function selectIsRecordingLocked(state: { phase: RecordPhase }) {
  return state.phase === 'recording';
}

interface RecordSessionState {
  phase: RecordPhase;
  elapsed: number;
  savedDuration: number;
  isPaused: boolean;
  audioFile: File | null;
  micError: string | null;
  startNewRecording: () => Promise<void>;
  pause: () => void;
  resume: () => void;
  stop: () => void;
  resetToIdle: () => void;
  clearTimer: () => void;
}

let timerId: number | null = null;
let mediaRecorder: MediaRecorder | null = null;
let mediaStream: MediaStream | null = null;
let chunks: BlobPart[] = [];

function clearTimerInterval() {
  if (timerId !== null) {
    window.clearInterval(timerId);
    timerId = null;
  }
}

function startTimerInterval(tick: () => void) {
  clearTimerInterval();
  timerId = window.setInterval(tick, 1000);
}

function stopMediaTracks() {
  mediaStream?.getTracks().forEach((track) => track.stop());
  mediaStream = null;
  mediaRecorder = null;
  chunks = [];
}

export const useRecordSessionStore = create<RecordSessionState>((set, get) => ({
  phase: 'idle',
  elapsed: 0,
  savedDuration: 0,
  isPaused: false,
  audioFile: null,
  micError: null,

  clearTimer: () => {
    clearTimerInterval();
  },

  resetToIdle: () => {
    get().clearTimer();
    if (mediaRecorder) {
      mediaRecorder.ondataavailable = null;
      mediaRecorder.onerror = null;
      mediaRecorder.onstop = null;
      if (mediaRecorder.state !== 'inactive') {
        try {
          mediaRecorder.stop();
        } catch {
          // ignore teardown errors
        }
      }
    }
    stopMediaTracks();
    set({
      phase: 'idle',
      elapsed: 0,
      savedDuration: 0,
      isPaused: false,
      audioFile: null,
      micError: null,
    });
  },

  startNewRecording: async () => {
    get().clearTimer();
    stopMediaTracks();
    set({
      phase: 'idle',
      elapsed: 0,
      savedDuration: 0,
      isPaused: false,
      audioFile: null,
      micError: null,
    });

    if (!navigator.mediaDevices?.getUserMedia) {
      set({
        micError: 'This browser does not support microphone recording.',
      });
      return;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          channelCount: 1,
        },
      });
      mediaStream = stream;
      chunks = [];

      const mimeType = preferredRecordingMimeType();
      const recorder = mimeType
        ? new MediaRecorder(stream, { mimeType })
        : new MediaRecorder(stream);

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) chunks.push(event.data);
      };

      recorder.onerror = () => {
        get().clearTimer();
        stopMediaTracks();
        set({
          phase: 'idle',
          isPaused: false,
          micError: 'Recording failed. Check your microphone and try again.',
        });
      };

      mediaRecorder = recorder;
      // No timeslice — a single blob on stop is far more reliably playable than chunked WebM.
      recorder.start();
      set({ phase: 'recording', elapsed: 0, isPaused: false, micError: null });
      startTimerInterval(() => {
        set((state) => ({ elapsed: state.elapsed + 1 }));
      });
    } catch {
      stopMediaTracks();
      set({
        phase: 'idle',
        micError: 'Microphone access is required to record consultations.',
      });
    }
  },

  pause: () => {
    const { phase, isPaused } = get();
    if (phase !== 'recording' || isPaused) return;
    // Soft-pause: keep MediaRecorder running so the WebM container stays valid.
    setMicrophoneEnabled(mediaStream, false);
    get().clearTimer();
    set({ isPaused: true });
  },

  resume: () => {
    const { phase, isPaused } = get();
    if (phase !== 'recording' || !isPaused) return;
    setMicrophoneEnabled(mediaStream, true);
    set({ isPaused: false });
    startTimerInterval(() => {
      set((state) => ({ elapsed: state.elapsed + 1 }));
    });
  },

  stop: () => {
    const { phase, elapsed } = get();
    if (phase !== 'recording') return;

    get().clearTimer();
    setMicrophoneEnabled(mediaStream, true);
    const recorder = mediaRecorder;

    const finishWithFile = (file: File | null) => {
      stopMediaTracks();
      set({
        phase: 'attach',
        savedDuration: elapsed,
        isPaused: false,
        audioFile: file,
      });
    };

    if (!recorder || recorder.state === 'inactive') {
      finishWithFile(null);
      return;
    }

    recorder.onstop = () => {
      const file = buildRecordingFile(chunks, recorder.mimeType || preferredRecordingMimeType());
      finishWithFile(file);
    };

    try {
      if (recorder.state === 'recording' || recorder.state === 'paused') {
        recorder.requestData();
      }
    } catch {
      // requestData is best-effort; stop still finalizes the blob.
    }

    recorder.stop();
  },
}));
