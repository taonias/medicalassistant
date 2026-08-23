import type { Page } from '@playwright/test';

export interface CaptureContract {
  recorderMimeTypes: string[];
  trackEnabled: boolean[];
  trackStops: number;
  recorderStarts: number;
  recorderStops: number;
  createdObjectUrls: string[];
  revokedObjectUrls: string[];
}

/** Replace browser hardware/decoding only; application recording modules stay real. */
export async function installDeterministicMediaCapture(page: Page) {
  await page.addInitScript(() => {
    const contract: CaptureContract = {
      recorderMimeTypes: [],
      trackEnabled: [],
      trackStops: 0,
      recorderStarts: 0,
      recorderStops: 0,
      createdObjectUrls: [],
      revokedObjectUrls: [],
    };

    Object.defineProperty(window, '__captureContract', { value: contract });

    let enabled = true;
    const track = {
      kind: 'audio',
      get enabled() {
        return enabled;
      },
      set enabled(value: boolean) {
        enabled = value;
        contract.trackEnabled.push(value);
      },
      stop() {
        contract.trackStops += 1;
      },
    };
    const stream = {
      getAudioTracks: () => [track],
      getTracks: () => [track],
    };

    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: async () => stream },
    });

    class DeterministicMediaRecorder {
      static isTypeSupported(type: string) {
        return type === 'audio/webm;codecs=opus' || type === 'audio/webm';
      }

      state: RecordingState = 'inactive';
      mimeType: string;
      ondataavailable: ((event: BlobEvent) => void) | null = null;
      onerror: ((event: Event) => void) | null = null;
      onstop: ((event: Event) => void) | null = null;

      constructor(_stream: unknown, options?: MediaRecorderOptions) {
        this.mimeType = options?.mimeType ?? 'audio/webm';
        contract.recorderMimeTypes.push(this.mimeType);
      }

      start() {
        this.state = 'recording';
        contract.recorderStarts += 1;
      }

      requestData() {
        const data = new Blob(['deterministic-browser-audio'], { type: this.mimeType });
        this.ondataavailable?.({ data } as BlobEvent);
      }

      stop() {
        if (this.state === 'inactive') return;
        this.state = 'inactive';
        contract.recorderStops += 1;
        queueMicrotask(() => this.onstop?.(new Event('stop')));
      }
    }

    Object.defineProperty(window, 'MediaRecorder', {
      configurable: true,
      value: DeterministicMediaRecorder,
    });

    class DeterministicAudioContext {
      async decodeAudioData() {
        return { duration: 2 };
      }

      async close() {}
    }

    Object.defineProperty(window, 'AudioContext', {
      configurable: true,
      value: DeterministicAudioContext,
    });

    const createObjectURL = URL.createObjectURL.bind(URL);
    const revokeObjectURL = URL.revokeObjectURL.bind(URL);
    URL.createObjectURL = (object: Blob | MediaSource) => {
      const url = createObjectURL(object);
      contract.createdObjectUrls.push(url);
      return url;
    };
    URL.revokeObjectURL = (url: string) => {
      contract.revokedObjectUrls.push(url);
      revokeObjectURL(url);
    };
  });
}

export async function readCaptureContract(page: Page): Promise<CaptureContract> {
  return page.evaluate(() =>
    structuredClone(
      (window as typeof window & { __captureContract: CaptureContract }).__captureContract,
    ),
  );
}
