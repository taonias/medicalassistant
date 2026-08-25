import { create } from 'zustand';
import { startAudioCapture, type AudioCaptureSession } from '../../../../platform/browser-media';

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

let session: AudioCaptureSession | null = null;

function disposeSession() {
  session?.dispose();
  session = null;
}

export const useRecordSessionStore = create<RecordSessionState>((set, get) => ({
  phase: 'idle',
  elapsed: 0,
  savedDuration: 0,
  isPaused: false,
  audioFile: null,
  micError: null,

  clearTimer: () => {
    disposeSession();
  },

  resetToIdle: () => {
    get().clearTimer();
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
      session = await startAudioCapture({
        onTick: (elapsed) => set({ elapsed }),
        onError: () => {
          session = null;
          set({
            phase: 'idle',
            isPaused: false,
            micError: 'Recording failed. Check your microphone and try again.',
          });
        },
      });
      set({ phase: 'recording', elapsed: 0, isPaused: false, micError: null });
    } catch {
      session = null;
      set({
        phase: 'idle',
        micError: 'Microphone access is required to record consultations.',
      });
    }
  },

  pause: () => {
    const { phase, isPaused } = get();
    if (phase !== 'recording' || isPaused || !session) return;
    session.pause();
    set({ isPaused: true });
  },

  resume: () => {
    const { phase, isPaused } = get();
    if (phase !== 'recording' || !isPaused || !session) return;
    session.resume();
    set({ isPaused: false });
  },

  stop: () => {
    const { phase, elapsed } = get();
    if (phase !== 'recording' || !session) return;

    const current = session;
    session = null;

    void current.stop().then((file) => {
      set({
        phase: 'attach',
        savedDuration: elapsed,
        isPaused: false,
        audioFile: file,
      });
    });
  },
}));
