import { useEffect, useRef, useState } from 'react';
import { startAudioCapture, type AudioCaptureSession } from '../../../../platform/browser-media';
import { resolveRecordingDuration } from '../utils/getAudioDuration';

interface Props {
  onRecordingComplete: (file: File, durationSeconds?: number) => void;
  disabled?: boolean;
  isProcessing?: boolean;
  variant?: 'default' | 'minimal';
}

function formatDuration(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

export function AudioRecorder({
  onRecordingComplete,
  disabled,
  isProcessing,
  variant = 'default',
}: Props) {
  const [isRecording, setIsRecording] = useState(false);
  const [isPaused, setIsPaused] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const sessionRef = useRef<AudioCaptureSession | null>(null);
  const elapsedRef = useRef(0);

  useEffect(() => {
    elapsedRef.current = elapsed;
  }, [elapsed]);

  useEffect(() => {
    return () => {
      sessionRef.current?.dispose();
      sessionRef.current = null;
    };
  }, []);

  async function startRecording() {
    setError(null);
    try {
      const session = await startAudioCapture({
        onTick: (value) => {
          setElapsed(value);
          elapsedRef.current = value;
        },
      });
      sessionRef.current = session;
      setIsRecording(true);
      setIsPaused(false);
      setElapsed(0);
      elapsedRef.current = 0;
    } catch {
      setError('Microphone access is required to record consultations.');
    }
  }

  function pauseRecording() {
    if (!sessionRef.current) return;
    sessionRef.current.pause();
    setIsPaused(true);
  }

  function resumeRecording() {
    if (!isPaused || !sessionRef.current) return;
    sessionRef.current.resume();
    setIsPaused(false);
  }

  function stopRecording() {
    const session = sessionRef.current;
    if (!session) return;
    sessionRef.current = null;
    setIsRecording(false);
    setIsPaused(false);

    void session.stop().then(async (file) => {
      if (!file) return;
      const duration = await resolveRecordingDuration(file, elapsedRef.current);
      onRecordingComplete(file, duration);
    });
  }

  const isMinimal = variant === 'minimal';
  const statusLabel = isProcessing
    ? 'Saving…'
    : isRecording
      ? isPaused
        ? `Paused ${formatDuration(elapsed)}`
        : formatDuration(elapsed)
      : isMinimal
        ? null
        : 'Ready to record';

  return (
    <div className={`audio-recorder${isMinimal ? ' audio-recorder--minimal' : ''}`}>
      {statusLabel ? (
        <div
          className={`audio-recorder__status${isRecording && !isPaused ? ' audio-recorder__status--live' : ''}`}
        >
          {statusLabel}
        </div>
      ) : null}
      {error ? <p className="field__error">{error}</p> : null}
      <div className="audio-recorder__actions">
        {!isRecording ? (
          <button
            type="button"
            className={`button button--primary${isMinimal ? ' audio-recorder__start' : ''}`}
            onClick={() => void startRecording()}
            disabled={disabled || isProcessing}
          >
            Start recording
          </button>
        ) : (
          <>
            <button
              type="button"
              className={`button button--secondary${isMinimal ? ' audio-recorder__control' : ''}`}
              onClick={isPaused ? resumeRecording : pauseRecording}
            >
              {isPaused ? 'Resume' : 'Pause'}
            </button>
            <button
              type="button"
              className={`button button--danger${isMinimal ? ' audio-recorder__control' : ''}`}
              onClick={stopRecording}
            >
              Stop
            </button>
          </>
        )}
      </div>
    </div>
  );
}
