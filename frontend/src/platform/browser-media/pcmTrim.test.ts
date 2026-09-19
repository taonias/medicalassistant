import { describe, expect, it } from 'vitest';
import {
  computeActiveRanges,
  encodeWavPcm16,
  extractActiveSamples,
  resamplePcm,
  buildTrimmedWav,
  type DecodedAudioLike,
} from './pcmTrim';

describe('computeActiveRanges', () => {
  it('returns the whole range when nothing was paused', () => {
    expect(computeActiveRanges(1000, [])).toEqual([{ startMs: 0, endMs: 1000 }]);
  });

  it('cuts a single paused range out of the middle', () => {
    expect(computeActiveRanges(1000, [{ startMs: 300, endMs: 600 }])).toEqual([
      { startMs: 0, endMs: 300 },
      { startMs: 600, endMs: 1000 },
    ]);
  });

  it('handles a pause that runs to the end (stopped while paused)', () => {
    expect(computeActiveRanges(1000, [{ startMs: 700, endMs: 1000 }])).toEqual([
      { startMs: 0, endMs: 700 },
    ]);
  });

  it('merges overlapping and out-of-order paused ranges', () => {
    const result = computeActiveRanges(1000, [
      { startMs: 500, endMs: 800 },
      { startMs: 100, endMs: 200 },
      { startMs: 150, endMs: 250 },
    ]);
    expect(result).toEqual([
      { startMs: 0, endMs: 100 },
      { startMs: 250, endMs: 500 },
      { startMs: 800, endMs: 1000 },
    ]);
  });

  it('clamps ranges outside the total duration', () => {
    expect(computeActiveRanges(1000, [{ startMs: -50, endMs: 1500 }])).toEqual([]);
  });
});

describe('extractActiveSamples', () => {
  it('concatenates only the samples within the active ranges', () => {
    // 10 samples at 10Hz => 100ms each; keep [0,20)ms and [60,100)ms.
    const data = new Float32Array([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
    const result = extractActiveSamples(data, 10, [
      { startMs: 0, endMs: 200 },
      { startMs: 600, endMs: 1000 },
    ]);
    expect(Array.from(result)).toEqual([0, 1, 6, 7, 8, 9]);
  });
});

describe('resamplePcm', () => {
  it('returns the same array when rates match', () => {
    const input = new Float32Array([1, 2, 3]);
    expect(resamplePcm(input, 16000, 16000)).toBe(input);
  });

  it('halves the sample count when downsampling by 2x', () => {
    const input = new Float32Array([0, 1, 2, 3, 4, 5, 6, 7]);
    const result = resamplePcm(input, 16000, 8000);
    expect(result.length).toBe(4);
  });
});

describe('encodeWavPcm16', () => {
  it('writes a valid RIFF/WAVE header for the given sample rate', () => {
    const blob = encodeWavPcm16(new Float32Array([0, 0.5, -0.5, 1, -1]), 16000);
    expect(blob.type).toBe('audio/wav');
    expect(blob.size).toBe(44 + 5 * 2);
  });
});

describe('buildTrimmedWav', () => {
  it('drops the paused segment and downsamples to the target rate', () => {
    // 1 second of samples at 8Hz, paused from 250ms to 500ms.
    const buffer: DecodedAudioLike = {
      sampleRate: 8,
      numberOfChannels: 1,
      length: 8,
      getChannelData: () => new Float32Array([0, 1, 2, 3, 4, 5, 6, 7]),
    };

    const blob = buildTrimmedWav(buffer, [{ startMs: 250, endMs: 500 }], 4);

    // Active samples after cutting [2,4) at 8Hz: [0,1,4,5,6,7] (6 samples),
    // downsampled from 8Hz to 4Hz (halved) => 3 samples => 44 + 3*2 bytes.
    expect(blob.type).toBe('audio/wav');
    expect(blob.size).toBe(44 + 3 * 2);
  });

  it('mixes multi-channel input down to mono', () => {
    const buffer: DecodedAudioLike = {
      sampleRate: 4,
      numberOfChannels: 2,
      length: 4,
      getChannelData: (channel) =>
        channel === 0 ? new Float32Array([1, 1, 1, 1]) : new Float32Array([-1, -1, -1, -1]),
    };

    const blob = buildTrimmedWav(buffer, [], 4);

    expect(blob.size).toBe(44 + 4 * 2); // fully silent after mixing +1/-1 down to 0
  });
});
