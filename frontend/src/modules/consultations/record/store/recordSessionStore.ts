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
  /** True once the input has read as sustained silence for a few seconds — surfaced as a non-blocking warning, recording continues. */
  isSilent: boolean;
  startNewRecording: () => Promise<void>;
  pause: () => void;
  resume: () => void;
  stop: () => void;
  resetToIdle: () => void;
  clearTimer: () => void;
  /** Live input spectrum for the recording visualizer; see AudioCaptureSession.getSpectrum. */
  getSpectrum: (barCount: number) => number[];
}

let session: AudioCaptureSession | null = null;
// Bumped by every disposal so an in-flight startAudioCapture() from an
// earlier startNewRecording() call can tell it's been superseded (e.g. React
// StrictMode's mount→cleanup→remount, or fast navigation away and back)
// before it resolves, and discard itself instead of overwriting `session`
// with a second, uncoordinated recording.
let sessionGeneration = 0;

function disposeSession() {
  sessionGeneration += 1;
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
  isSilent: false,

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
      isSilent: false,
    });
  },

  startNewRecording: async () => {
    get().clearTimer();
    const myGeneration = sessionGeneration;
    set({
      phase: 'idle',
      elapsed: 0,
      savedDuration: 0,
      isPaused: false,
      audioFile: null,
      micError: null,
      isSilent: false,
    });

    if (!navigator.mediaDevices?.getUserMedia) {
      set({
        micError: 'This browser does not support microphone recording.',
      });
      return;
    }

    try {
      const newSession = await startAudioCapture({
        onTick: (elapsed) => set({ elapsed }),
        onError: () => {
          session = null;
          set({
            phase: 'idle',
            isPaused: false,
            micError: 'Recording failed. Check your microphone and try again.',
            isSilent: false,
          });
        },
        onSilenceChange: (isSilent) => set({ isSilent }),
      });

      if (myGeneration !== sessionGeneration) {
        // Superseded while awaiting getUserMedia (StrictMode remount, a
        // second startNewRecording/resetToIdle, or unmount) — tear this one
        // down instead of adopting it alongside whatever is now current.
        newSession.dispose();
        return;
      }

      session = newSession;
      set({ phase: 'recording', elapsed: 0, isPaused: false, micError: null, isSilent: false });
    } catch {
      if (myGeneration !== sessionGeneration) return;
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
        isSilent: false,
      });
    });
  },

  getSpectrum: (barCount) => session?.getSpectrum(barCount) ?? new Array(barCount).fill(0),
}));
