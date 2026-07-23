import { useEffect, useRef, useState } from 'react';
import { formatDuration } from '../../../shared/utils/format';
import { PlayIcon, PauseIcon, StopIcon } from '../../../layouts/navigation/NavIcons';

interface Props {
  durationSeconds: number;
  /** Object URL or authenticated media URL for real recording playback. */
  audioSrc?: string | null;
}

/** Plays uploaded consultation audio, or a soft tone preview when only duration is known. */
export function RecordingPreviewPlayer({ durationSeconds, audioSrc }: Props) {
  const totalHint = Math.max(0, durationSeconds || 0);
  const [playing, setPlaying] = useState(false);
  const [position, setPosition] = useState(0);
  const [mediaDuration, setMediaDuration] = useState(0);
  const rafRef = useRef<number | null>(null);
  const startedAtRef = useRef<number>(0);
  const offsetRef = useRef(0);
  const audioCtxRef = useRef<AudioContext | null>(null);
  const oscillatorRef = useRef<OscillatorNode | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const total = audioSrc
    ? Math.max(totalHint, mediaDuration || 0)
    : totalHint;

  function stopAudioNodes() {
    try {
      oscillatorRef.current?.stop();
    } catch {
      // already stopped
    }
    oscillatorRef.current?.disconnect();
    oscillatorRef.current = null;
  }

  function clearTick() {
    if (rafRef.current != null) {
      cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
    }
  }

  function stopPlayback() {
    clearTick();
    stopAudioNodes();
    const media = audioRef.current;
    if (media) {
      media.pause();
      media.currentTime = 0;
    }
    setPlaying(false);
    setPosition(0);
    offsetRef.current = 0;
  }

  function tickTone() {
    const elapsed = offsetRef.current + (performance.now() - startedAtRef.current) / 1000;
    if (elapsed >= total) {
      stopPlayback();
      return;
    }
    setPosition(elapsed);
    rafRef.current = requestAnimationFrame(tickTone);
  }

  function tickMedia() {
    const media = audioRef.current;
    if (!media) return;
    setPosition(media.currentTime);
    if (media.ended || media.paused) {
      if (media.ended) {
        stopPlayback();
      } else {
        clearTick();
        setPlaying(false);
        offsetRef.current = media.currentTime;
      }
      return;
    }
    rafRef.current = requestAnimationFrame(tickMedia);
  }

  function startTone() {
    const AudioContextCtor =
      window.AudioContext ||
      (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AudioContextCtor || total <= 0) return;

    const ctx = audioCtxRef.current ?? new AudioContextCtor();
    audioCtxRef.current = ctx;
    void ctx.resume();

    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();
    oscillator.type = 'sine';
    oscillator.frequency.value = 440;
    gain.gain.value = 0.04;
    oscillator.connect(gain);
    gain.connect(ctx.destination);
    oscillator.start();
    oscillatorRef.current = oscillator;
  }

  async function togglePlay() {
    if (total <= 0 && !audioSrc) return;

    if (playing) {
      clearTick();
      if (audioSrc && audioRef.current) {
        audioRef.current.pause();
        offsetRef.current = audioRef.current.currentTime;
      } else {
        stopAudioNodes();
        offsetRef.current = position;
      }
      setPlaying(false);
      return;
    }

    if (audioSrc && audioRef.current) {
      try {
        await audioRef.current.play();
        setPlaying(true);
        rafRef.current = requestAnimationFrame(tickMedia);
      } catch {
        setPlaying(false);
      }
      return;
    }

    startedAtRef.current = performance.now();
    startTone();
    setPlaying(true);
    rafRef.current = requestAnimationFrame(tickTone);
  }

  useEffect(() => {
    stopPlayback();
    if (!audioSrc) {
      audioRef.current = null;
      setMediaDuration(0);
      return;
    }

    const media = new Audio(audioSrc);
    media.preload = 'metadata';
    audioRef.current = media;

    const onLoaded = () => {
      if (Number.isFinite(media.duration)) {
        setMediaDuration(media.duration);
      }
    };
    const onEnded = () => stopPlayback();

    media.addEventListener('loadedmetadata', onLoaded);
    media.addEventListener('ended', onEnded);

    return () => {
      media.removeEventListener('loadedmetadata', onLoaded);
      media.removeEventListener('ended', onEnded);
      media.pause();
      audioRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset on source change only
  }, [audioSrc]);

  useEffect(
    () => () => {
      clearTick();
      stopAudioNodes();
      void audioCtxRef.current?.close();
    },
    [],
  );

  const progress = total > 0 ? Math.min(1, position / total) : 0;
  const canPlay = Boolean(audioSrc) || total > 0;

  return (
    <div className="recording-preview">
      <div className="recording-preview__row">
        <button
          type="button"
          className="icon-button recording-preview__play"
          onClick={() => void togglePlay()}
          disabled={!canPlay}
          aria-label={playing ? 'Pause recording' : 'Play recording'}
        >
          {playing ? <PauseIcon /> : <PlayIcon />}
        </button>
        <button
          type="button"
          className="icon-button recording-preview__stop"
          onClick={stopPlayback}
          disabled={!playing && position === 0}
          aria-label="Stop recording"
        >
          <StopIcon />
        </button>
        <div className="recording-preview__track" aria-hidden="true">
          <span className="recording-preview__fill" style={{ width: `${progress * 100}%` }} />
        </div>
        <span className="recording-preview__time muted">
          {formatDuration(Math.floor(position))} / {formatDuration(Math.floor(total) || undefined)}
        </span>
      </div>
    </div>
  );
}
