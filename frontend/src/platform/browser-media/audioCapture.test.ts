import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { startAudioCapture } from './audioCapture';

type DataAvailableHandler = ((event: { data: Blob }) => void) | null;

class FakeTrack {
  enabled = true;
  stopped = false;
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
});
