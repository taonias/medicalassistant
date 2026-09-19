export interface TimeRangeMs {
  startMs: number;
  endMs: number;
}

/** Minimal AudioBuffer surface this module needs, so tests can pass a plain object. */
export interface DecodedAudioLike {
  readonly sampleRate: number;
  readonly numberOfChannels: number;
  readonly length: number;
  getChannelData(channel: number): Float32Array;
}

/**
 * Inverts a set of paused time ranges (which may be unsorted or overlapping)
 * against the recording's total duration, returning the ranges that were
 * actually active.
 */
export function computeActiveRanges(totalMs: number, pausedRanges: TimeRangeMs[]): TimeRangeMs[] {
  const sorted = pausedRanges
    .map(({ startMs, endMs }) => ({
      startMs: Math.max(0, Math.min(startMs, totalMs)),
      endMs: Math.max(0, Math.min(endMs, totalMs)),
    }))
    .filter(({ startMs, endMs }) => endMs > startMs)
    .sort((a, b) => a.startMs - b.startMs);

  const active: TimeRangeMs[] = [];
  let cursor = 0;
  for (const { startMs, endMs } of sorted) {
    if (startMs > cursor) active.push({ startMs: cursor, endMs: startMs });
    cursor = Math.max(cursor, endMs);
  }
  if (cursor < totalMs) active.push({ startMs: cursor, endMs: totalMs });
  return active;
}

function msToSampleIndex(ms: number, sampleRate: number): number {
  return Math.round((ms / 1000) * sampleRate);
}

function mixToMono(buffer: DecodedAudioLike): Float32Array {
  const mono = new Float32Array(buffer.length);
  for (let channel = 0; channel < buffer.numberOfChannels; channel += 1) {
    const data = buffer.getChannelData(channel);
    for (let i = 0; i < data.length; i += 1) {
      mono[i] += data[i] / buffer.numberOfChannels;
    }
  }
  return mono;
}

/** Concatenates only the samples that fall within the given active ranges. */
export function extractActiveSamples(
  channelData: Float32Array,
  sampleRate: number,
  activeRanges: TimeRangeMs[],
): Float32Array {
  const sampleRanges = activeRanges.map(({ startMs, endMs }) => ({
    start: msToSampleIndex(startMs, sampleRate),
    end: Math.min(msToSampleIndex(endMs, sampleRate), channelData.length),
  }));
  const totalSamples = sampleRanges.reduce((sum, { start, end }) => sum + Math.max(0, end - start), 0);

  const result = new Float32Array(totalSamples);
  let offset = 0;
  for (const { start, end } of sampleRanges) {
    if (end <= start) continue;
    result.set(channelData.subarray(start, end), offset);
    offset += end - start;
  }
  return result;
}

/** Linear-interpolation resampler — good enough for speech, no external deps. */
export function resamplePcm(input: Float32Array, inputRate: number, outputRate: number): Float32Array {
  if (inputRate === outputRate || input.length === 0) return input;

  const ratio = inputRate / outputRate;
  const outputLength = Math.max(1, Math.round(input.length / ratio));
  const output = new Float32Array(outputLength);
  for (let i = 0; i < outputLength; i += 1) {
    const srcIndex = i * ratio;
    const lower = Math.floor(srcIndex);
    const upper = Math.min(lower + 1, input.length - 1);
    const weight = srcIndex - lower;
    output[i] = input[lower] * (1 - weight) + input[upper] * weight;
  }
  return output;
}

function writeAsciiString(view: DataView, offset: number, text: string) {
  for (let i = 0; i < text.length; i += 1) {
    view.setUint8(offset + i, text.charCodeAt(i));
  }
}

/** Encodes mono Float32 samples as a 16-bit PCM WAV file. */
export function encodeWavPcm16(samples: Float32Array, sampleRate: number): Blob {
  const bytesPerSample = 2;
  const dataSize = samples.length * bytesPerSample;
  const buffer = new ArrayBuffer(44 + dataSize);
  const view = new DataView(buffer);

  writeAsciiString(view, 0, 'RIFF');
  view.setUint32(4, 36 + dataSize, true);
  writeAsciiString(view, 8, 'WAVE');
  writeAsciiString(view, 12, 'fmt ');
  view.setUint32(16, 16, true); // fmt chunk size
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, 1, true); // mono
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * bytesPerSample, true); // byte rate
  view.setUint16(32, bytesPerSample, true); // block align
  view.setUint16(34, 16, true); // bits per sample
  writeAsciiString(view, 36, 'data');
  view.setUint32(40, dataSize, true);

  let offset = 44;
  for (let i = 0; i < samples.length; i += 1) {
    const clamped = Math.max(-1, Math.min(1, samples[i]));
    view.setInt16(offset, clamped < 0 ? clamped * 0x8000 : clamped * 0x7fff, true);
    offset += bytesPerSample;
  }

  return new Blob([buffer], { type: 'audio/wav' });
}

/** Cuts the paused ranges out of a decoded recording and re-encodes it as a 16kHz mono WAV. */
export function buildTrimmedWav(
  buffer: DecodedAudioLike,
  pausedRangesMs: TimeRangeMs[],
  targetSampleRate = 16000,
): Blob {
  const totalMs = (buffer.length / buffer.sampleRate) * 1000;
  const activeRanges = computeActiveRanges(totalMs, pausedRangesMs);
  const mono = buffer.numberOfChannels > 1 ? mixToMono(buffer) : buffer.getChannelData(0);
  const trimmed = extractActiveSamples(mono, buffer.sampleRate, activeRanges);
  const resampled = resamplePcm(trimmed, buffer.sampleRate, targetSampleRate);
  return encodeWavPcm16(resampled, targetSampleRate);
}
