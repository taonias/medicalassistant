import { describe, expect, it } from 'vitest';
import { applyDurationFallback } from './getAudioDuration';

describe('applyDurationFallback', () => {
  it('falls back to the timer when decoded duration is unavailable', () => {
    expect(applyDurationFallback(undefined, 12)).toBe(12);
  });

  it('returns undefined when decoded duration is unavailable and the timer never ran', () => {
    expect(applyDurationFallback(undefined, 0)).toBeUndefined();
  });

  it('prefers the timer over an implausibly short decoded duration for a long recording', () => {
    // Guards the WebM metadata bug that reports ~1s for genuinely long recordings.
    expect(applyDurationFallback(1, 45)).toBe(45);
  });

  it('trusts the decoded duration when it is plausible', () => {
    expect(applyDurationFallback(30, 45)).toBe(30);
  });

  it('trusts a short decoded duration when the timer is also short', () => {
    expect(applyDurationFallback(1, 1)).toBe(1);
  });
});
