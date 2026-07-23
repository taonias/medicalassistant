import { useEffect, useRef, useState } from 'react';
import { getAudioDurationSeconds } from '../utils/getAudioDuration';
import {
  buildRecordingFile,
  preferredRecordingMimeType,
  setMicrophoneEnabled,
} from '../utils/recordingMedia';

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
      const recorder = mediaRecorderRef.current;
      if (recorder && recorder.state !== 'inactive') {
        recorder.ondataavailable = null;
        recorder.onstop = null;
        try {
          recorder.stop();
        } catch {
          // ignore
        }
      }
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
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          channelCount: 1,
        },
      });
      streamRef.current = stream;
      chunksRef.current = [];

      const mimeType = preferredRecordingMimeType();
      const recorder = mimeType
        ? new MediaRecorder(stream, { mimeType })
        : new MediaRecorder(stream);

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) chunksRef.current.push(event.data);
      };

      recorder.onstop = () => {
        void (async () => {
          const file = buildRecordingFile(
            chunksRef.current,
            recorder.mimeType || preferredRecordingMimeType(),
          );
          stream.getTracks().forEach((track) => track.stop());
          streamRef.current = null;

          if (!file) return;

          const measured = await getAudioDurationSeconds(file);
          const duration =
            measured == null
              ? elapsedRef.current > 0
                ? elapsedRef.current
                : undefined
              : measured <= 1 && elapsedRef.current > 2
                ? elapsedRef.current
                : measured;
          onRecordingComplete(file, duration);
        })();
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
    setMicrophoneEnabled(streamRef.current, false);
    setIsPaused(true);
    clearTimer();
  }

  function resumeRecording() {
    if (!isPaused || mediaRecorderRef.current?.state !== 'recording') return;
    setMicrophoneEnabled(streamRef.current, true);
    setIsPaused(false);
    startTimer();
  }

  function stopRecording() {
    const recorder = mediaRecorderRef.current;
    if (!recorder || recorder.state === 'inactive') return;
    setMicrophoneEnabled(streamRef.current, true);
    try {
      recorder.requestData();
    } catch {
      // best-effort
    }
    recorder.stop();
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
