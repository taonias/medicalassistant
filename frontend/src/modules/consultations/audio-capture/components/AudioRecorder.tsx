import { useEffect, useRef, useState } from 'react';
import { startAudioCapture, type AudioCaptureSession } from '../../../../platform/browser-media';
import { resolveRecordingDuration } from '../utils/getAudioDuration';
import { AudioVisualizer } from '../../record/components/AudioVisualizer';

interface Props {
  onRecordingComplete: (file: File, durationSeconds?: number) => void;
  disabled?: boolean;
  isProcessing?: boolean;
  variant?: 'default' | 'minimal';
}

function formatDuration(totalSeconds: number) {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
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
  const [isSilent, setIsSilent] = useState(false);
  const sessionRef = useRef<AudioCaptureSession | null>(null);
  const elapsedRef = useRef(0);
  // Guards against a getUserMedia prompt resolving after the component has
  // already unmounted (e.g. the user navigates away mid-permission-prompt):
  // without this, the resulting session would be adopted and start ticking
  // on a component nothing is listening to anymore, leaking the mic stream.
  const mountedRef = useRef(true);
  const getSpectrum = (barCount: number) =>
    sessionRef.current?.getSpectrum(barCount) ?? new Array(barCount).fill(0);

  useEffect(() => {
    elapsedRef.current = elapsed;
  }, [elapsed]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      sessionRef.current?.dispose();
      sessionRef.current = null;
    };
  }, []);

  async function startRecording() {
    setError(null);
    setIsSilent(false);
    try {
      const newSession = await startAudioCapture({
        onTick: (value) => {
          setElapsed(value);
          elapsedRef.current = value;
        },
        onSilenceChange: setIsSilent,
      });

      if (!mountedRef.current) {
        newSession.dispose();
        return;
      }

      sessionRef.current = newSession;
      setIsRecording(true);
      setIsPaused(false);
      setElapsed(0);
      elapsedRef.current = 0;
    } catch {
      if (!mountedRef.current) return;
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
    setIsSilent(false);

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
      {isRecording ? (
        <AudioVisualizer active={isRecording} paused={isPaused} getSpectrum={getSpectrum} />
      ) : null}
      {isSilent && isRecording && !isPaused ? (
        <p className="record-session__silence-warning" role="status" aria-live="polite">
          No sound detected — check your microphone.
        </p>
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
