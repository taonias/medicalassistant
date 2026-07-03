import { create } from 'zustand';

export type RecordPhase = 'idle' | 'recording' | 'attach';

export function selectIsRecordingLocked(state: { phase: RecordPhase }) {
  return state.phase === 'recording';
}

interface RecordSessionState {
  phase: RecordPhase;
  elapsed: number;
  savedDuration: number;
  isPaused: boolean;
  startNewRecording: () => void;
  pause: () => void;
  resume: () => void;
  stop: () => void;
  resetToIdle: () => void;
  clearTimer: () => void;
}

let timerId: number | null = null;

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

export const useRecordSessionStore = create<RecordSessionState>((set, get) => ({
  phase: 'idle',
  elapsed: 0,
  savedDuration: 0,
  isPaused: false,

  clearTimer: () => {
    clearTimerInterval();
  },

  resetToIdle: () => {
    get().clearTimer();
    set({ phase: 'idle', elapsed: 0, savedDuration: 0, isPaused: false });
  },

  startNewRecording: () => {
    get().clearTimer();
    set({ phase: 'recording', elapsed: 0, savedDuration: 0, isPaused: false });
    startTimerInterval(() => {
      set((state) => ({ elapsed: state.elapsed + 1 }));
    });
  },

  pause: () => {
    const { phase, isPaused } = get();
    if (phase !== 'recording' || isPaused) return;
    get().clearTimer();
    set({ isPaused: true });
  },

  resume: () => {
    const { phase, isPaused } = get();
    if (phase !== 'recording' || !isPaused) return;
    set({ isPaused: false });
    startTimerInterval(() => {
      set((state) => ({ elapsed: state.elapsed + 1 }));
    });
  },

  stop: () => {
    get().clearTimer();
    const { elapsed } = get();
    set({ phase: 'attach', savedDuration: elapsed, isPaused: false });
  },
}));
