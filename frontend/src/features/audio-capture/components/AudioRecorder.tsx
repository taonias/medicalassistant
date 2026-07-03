import { useEffect, useRef, useState } from 'react';

interface Props {
  onRecordingComplete: (file: File, durationSeconds: number) => void;
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
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const chunksRef = useRef<BlobPart[]>([]);
  const timerRef = useRef<number | null>(null);
  const elapsedRef = useRef(0);

  useEffect(() => {
    elapsedRef.current = elapsed;
  }, [elapsed]);

  useEffect(() => {
    return () => {
      if (timerRef.current) window.clearInterval(timerRef.current);
      mediaRecorderRef.current?.stop();
      streamRef.current?.getTracks().forEach((track) => track.stop());
    };
  }, []);

  function clearTimer() {
    if (timerRef.current) {
      window.clearInterval(timerRef.current);
      timerRef.current = null;
    }
  }

  function startTimer() {
    clearTimer();
    timerRef.current = window.setInterval(() => {
      setElapsed((value) => value + 1);
    }, 1000);
  }

  async function startRecording() {
    setError(null);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
      const recorder = new MediaRecorder(stream);
      chunksRef.current = [];

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) chunksRef.current.push(event.data);
      };

      recorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' });
        const file = new File([blob], `consultation-${Date.now()}.webm`, {
          type: blob.type,
        });
        onRecordingComplete(file, elapsedRef.current);
        stream.getTracks().forEach((track) => track.stop());
        streamRef.current = null;
      };

      mediaRecorderRef.current = recorder;
      recorder.start();
      setIsRecording(true);
      setIsPaused(false);
      setElapsed(0);
      elapsedRef.current = 0;
      startTimer();
    } catch {
      setError('Microphone access is required to record consultations.');
    }
  }

  function pauseRecording() {
    if (mediaRecorderRef.current?.state !== 'recording') return;
    mediaRecorderRef.current.pause();
    setIsPaused(true);
    clearTimer();
  }

  function resumeRecording() {
    if (mediaRecorderRef.current?.state !== 'paused') return;
    mediaRecorderRef.current.resume();
    setIsPaused(false);
    startTimer();
  }

  function stopRecording() {
    if (!mediaRecorderRef.current || mediaRecorderRef.current.state === 'inactive') return;
    mediaRecorderRef.current.stop();
    setIsRecording(false);
    setIsPaused(false);
    clearTimer();
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
