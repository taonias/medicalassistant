import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { startAudioCapture } from './audioCapture';

type DataAvailableHandler = ((event: { data: Blob }) => void) | null;
type TrackEventType = 'mute' | 'unmute';

class FakeTrack {
  enabled = true;
  stopped = false;
  private listeners: Record<TrackEventType, Array<() => void>> = { mute: [], unmute: [] };

  addEventListener(type: TrackEventType, listener: () => void) {
    this.listeners[type].push(listener);
  }
  removeEventListener(type: TrackEventType, listener: () => void) {
    this.listeners[type] = this.listeners[type].filter((l) => l !== listener);
  }
  dispatch(type: TrackEventType) {
    this.listeners[type].forEach((listener) => listener());
  }
  stop() {
    this.stopped = true;
  }
}

class FakeStream {
  track = new FakeTrack();
  getAudioTracks() {
    return [this.track];
  }
  getTracks() {
    return [this.track];
  }
}

/** Fixed sample value everywhere: 128 is the silent midpoint of a byte time-domain buffer. */
class FakeAnalyserNode {
  fftSize = 2048;
  smoothingTimeConstant = 0;
  timeDomainValue = 128;
  frequencyValue = 0;
  connected = false;

  get frequencyBinCount() {
    return this.fftSize / 2;
  }
  connect() {
    this.connected = true;
  }
  disconnect() {
    this.connected = false;
  }
  getByteTimeDomainData(array: Uint8Array) {
    array.fill(this.timeDomainValue);
  }
  getByteFrequencyData(array: Uint8Array) {
    array.fill(this.frequencyValue);
  }
}

class FakeAudioContext {
  static lastAnalyser: FakeAnalyserNode | null = null;
  /** Controls what decodeAudioData() resolves with, for the trim-on-stop tests. */
  static decodedBuffer: {
    sampleRate: number;
    numberOfChannels: number;
    length: number;
    getChannelData: (channel: number) => Float32Array;
  } | null = null;
  closed = false;

  createMediaStreamSource() {
    return { connect: vi.fn(), disconnect: vi.fn() };
  }
  createAnalyser() {
    const analyser = new FakeAnalyserNode();
    FakeAudioContext.lastAnalyser = analyser;
    return analyser;
  }
  decodeAudioData() {
    if (!FakeAudioContext.decodedBuffer) return Promise.reject(new Error('no decoded buffer configured'));
    return Promise.resolve(FakeAudioContext.decodedBuffer);
  }
  close() {
    this.closed = true;
    return Promise.resolve();
  }
}

class FakeMediaRecorder {
  static instances: FakeMediaRecorder[] = [];
  static callOrder: string[] = [];

  static isTypeSupported(type: string) {
    return type === 'audio/webm;codecs=opus' || type === 'audio/webm';
  }

  state: 'inactive' | 'recording' = 'inactive';
  mimeType: string;
  ondataavailable: DataAvailableHandler = null;
  onerror: (() => void) | null = null;
  onstop: (() => void) | null = null;
  requestDataCalls = 0;
  stopCalls = 0;
  stream: FakeStream;

  constructor(stream: FakeStream, options?: { mimeType?: string }) {
    this.stream = stream;
    this.mimeType = options?.mimeType ?? '';
    FakeMediaRecorder.instances.push(this);
  }

  start() {
    this.state = 'recording';
  }

  requestData() {
    this.requestDataCalls += 1;
    FakeMediaRecorder.callOrder.push('requestData');
    this.ondataavailable?.({ data: new Blob(['fake-audio'], { type: this.mimeType || 'audio/webm' }) });
  }

  stop() {
    this.stopCalls += 1;
    this.state = 'inactive';
    FakeMediaRecorder.callOrder.push('stop');
    this.onstop?.();
  }

  // Never expected to be called — recording uses soft-mute pause, not native pause.
  pause() {
    throw new Error('MediaRecorder.pause() should never be called by the adapter');
  }
}

let lastStream: FakeStream;

function installFakes() {
  FakeMediaRecorder.instances = [];
  FakeMediaRecorder.callOrder = [];
  FakeAudioContext.lastAnalyser = null;
  FakeAudioContext.decodedBuffer = null;
  lastStream = new FakeStream();

  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true,
    value: { getUserMedia: vi.fn(async () => lastStream) },
  });
  Object.defineProperty(window, 'MediaRecorder', {
    configurable: true,
    writable: true,
    value: FakeMediaRecorder,
  });
  Object.defineProperty(window, 'AudioContext', {
    configurable: true,
    writable: true,
    value: FakeAudioContext,
  });
}

describe('startAudioCapture', () => {
  beforeEach(() => {
    installFakes();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('selects the preferred MIME type and starts the recorder', async () => {
    await startAudioCapture();

    expect(FakeMediaRecorder.instances).toHaveLength(1);
    const recorder = FakeMediaRecorder.instances[0];
    expect(recorder.mimeType).toBe('audio/webm;codecs=opus');
    expect(recorder.state).toBe('recording');
  });

  it('pauses and resumes by toggling track.enabled, never the recorder', async () => {
    const session = await startAudioCapture();

    session.pause();
    expect(lastStream.track.enabled).toBe(false);

    session.resume();
    expect(lastStream.track.enabled).toBe(true);
  });

  it('ticks elapsed seconds once per second, cumulatively across pause/resume', async () => {
    vi.useFakeTimers();
    const ticks: number[] = [];

    const session = await startAudioCapture({ onTick: (seconds) => ticks.push(seconds) });
    vi.advanceTimersByTime(3000);

    expect(ticks).toEqual([1, 2, 3]);

    session.pause();
    vi.advanceTimersByTime(3000);
    expect(ticks).toEqual([1, 2, 3]); // no ticks while paused

    session.resume();
    vi.advanceTimersByTime(2000);
    // Resume continues counting from where it left off, not from zero.
    expect(ticks).toEqual([1, 2, 3, 4, 5]);
  });

  it('stop() requests the final chunk before stopping, then resolves the built file', async () => {
    const session = await startAudioCapture();

    const file = await session.stop();

    const recorder = FakeMediaRecorder.instances[0];
    expect(FakeMediaRecorder.callOrder).toEqual(['requestData', 'stop']);
    expect(recorder.requestDataCalls).toBe(1);
    expect(recorder.stopCalls).toBe(1);
    expect(file).toBeInstanceOf(File);
    expect(file?.type).toBe('audio/webm;codecs=opus');
    expect(lastStream.track.stopped).toBe(true);
  });

  it('trims paused time out of the file into a WAV when the session was paused', async () => {
    FakeAudioContext.decodedBuffer = {
      sampleRate: 8,
      numberOfChannels: 1,
      length: 8,
      getChannelData: () => new Float32Array([0, 1, 2, 3, 4, 5, 6, 7]),
    };
    const session = await startAudioCapture();

    session.pause();
    session.resume();
    const file = await session.stop();

    expect(file).toBeInstanceOf(File);
    expect(file?.type).toBe('audio/wav');
  });

  it('falls back to the raw file if decoding fails after a pause', async () => {
    // decodedBuffer left null so the fake's decodeAudioData() rejects.
    const session = await startAudioCapture();

    session.pause();
    session.resume();
    const file = await session.stop();

    expect(file?.type).toBe('audio/webm;codecs=opus');
  });

  it('dispose() is idempotent and tears down without finalizing a file', async () => {
    const session = await startAudioCapture();

    session.dispose();
    session.dispose();

    const recorder = FakeMediaRecorder.instances[0];
    expect(recorder.stopCalls).toBe(1);
    expect(lastStream.track.stopped).toBe(true);
  });

  it('invokes onError only when the caller supplies a handler', async () => {
    const onError = vi.fn();
    await startAudioCapture({ onError });

    const recorder = FakeMediaRecorder.instances[0];
    recorder.onerror?.();

    expect(onError).toHaveBeenCalledTimes(1);
  });

  it('does not throw when the recorder errors and no onError handler was supplied', async () => {
    await startAudioCapture();

    const recorder = FakeMediaRecorder.instances[0];
    expect(() => recorder.onerror?.()).not.toThrow();
  });

  describe('silence detection', () => {
    it('reports sustained silence only after several consecutive quiet ticks', async () => {
      vi.useFakeTimers();
      const onSilenceChange = vi.fn();
      await startAudioCapture({ onSilenceChange });

      FakeAudioContext.lastAnalyser!.timeDomainValue = 128; // silence (centered)

      vi.advanceTimersByTime(3000);
      expect(onSilenceChange).not.toHaveBeenCalled(); // a brief pause is not silence

      vi.advanceTimersByTime(1000);
      expect(onSilenceChange).toHaveBeenCalledTimes(1);
      expect(onSilenceChange).toHaveBeenCalledWith(true);
    });

    it('clears once real signal returns', async () => {
      vi.useFakeTimers();
      const onSilenceChange = vi.fn();
      await startAudioCapture({ onSilenceChange });

      const analyser = FakeAudioContext.lastAnalyser!;
      analyser.timeDomainValue = 128;
      vi.advanceTimersByTime(4000);
      expect(onSilenceChange).toHaveBeenLastCalledWith(true);

      analyser.timeDomainValue = 220; // loud signal, well above the threshold
      vi.advanceTimersByTime(1000);
      expect(onSilenceChange).toHaveBeenLastCalledWith(false);
    });

    it('treats a muted track as immediate silence, independent of level', async () => {
      const onSilenceChange = vi.fn();
      await startAudioCapture({ onSilenceChange });

      lastStream.track.dispatch('mute');
      expect(onSilenceChange).toHaveBeenCalledTimes(1);
      expect(onSilenceChange).toHaveBeenCalledWith(true);

      lastStream.track.dispatch('unmute');
      expect(onSilenceChange).toHaveBeenLastCalledWith(false);
    });

    it('does not warn while paused, and resets the hold on resume', async () => {
      vi.useFakeTimers();
      const onSilenceChange = vi.fn();
      const session = await startAudioCapture({ onSilenceChange });
      FakeAudioContext.lastAnalyser!.timeDomainValue = 128;

      vi.advanceTimersByTime(3000); // 3 quiet ticks, just short of the hold
      session.pause();
      vi.advanceTimersByTime(10_000); // no ticks fire while paused
      expect(onSilenceChange).not.toHaveBeenCalled();

      session.resume();
      vi.advanceTimersByTime(3000); // hold resets on resume, so 3 more ticks isn't enough
      expect(onSilenceChange).not.toHaveBeenCalled();
    });

    it('falls back to never reporting silence when Web Audio metering is unavailable', async () => {
      vi.useFakeTimers();
      Object.defineProperty(window, 'AudioContext', { configurable: true, value: undefined });
      const onSilenceChange = vi.fn();
      await startAudioCapture({ onSilenceChange });

      vi.advanceTimersByTime(10_000);
      expect(onSilenceChange).not.toHaveBeenCalled();
    });
  });

  describe('getSpectrum', () => {
    it('buckets frequency data into the requested number of 0..1 bars', async () => {
      const session = await startAudioCapture();
      FakeAudioContext.lastAnalyser!.frequencyValue = 191; // ~0.75 of 255

      const bars = session.getSpectrum(8);

      expect(bars).toHaveLength(8);
      bars.forEach((value) => {
        expect(value).toBeCloseTo(191 / 255, 2);
      });
    });

    it('returns all-zero bars once disposed, even if the input was loud', async () => {
      const session = await startAudioCapture();
      FakeAudioContext.lastAnalyser!.frequencyValue = 200;
      session.dispose();

      expect(session.getSpectrum(4)).toEqual([0, 0, 0, 0]);
    });

    it('returns all-zero bars when Web Audio metering is unavailable', async () => {
      Object.defineProperty(window, 'AudioContext', { configurable: true, value: undefined });
      const session = await startAudioCapture();

      expect(session.getSpectrum(5)).toEqual([0, 0, 0, 0, 0]);
    });
  });
});
