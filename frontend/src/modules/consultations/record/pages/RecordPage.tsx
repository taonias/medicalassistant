import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  useCreateConsultation,
  useUploadConsultationAudio,
} from '../../hooks/useConsultations';
import { usePatient } from '../../../../features/patients';
import { formatPatientName } from '../../../../shared/utils/format';
import { getAudioDurationSeconds } from '../../audio-capture/utils/getAudioDuration';
import { AudioVisualizer } from '../components/AudioVisualizer';
import { RecordControls } from '../components/RecordControls';
import { useRecordSessionStore } from '../store/recordSessionStore';

function createIdempotencyKey() {
  return crypto.randomUUID();
}

/** Prefer decoded file duration; fall back to timer if metadata is missing/bogus. */
async function resolveRecordingDuration(file: File, timerSeconds: number) {
  const measured = await getAudioDurationSeconds(file);
  if (measured == null) {
    return timerSeconds > 0 ? timerSeconds : undefined;
  }
  // Guard against the WebM metadata bug that reports ~1s for long recordings.
  if (measured <= 1 && timerSeconds > 2) {
    return timerSeconds;
  }
  return measured;
}

function formatDuration(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

export function RecordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const createConsultation = useCreateConsultation();
  const uploadAudio = useUploadConsultationAudio();
  const [saveError, setSaveError] = useState<string | null>(null);
  const autoSaveStarted = useRef(false);

  const preselectedPatientId = Number(searchParams.get('patientId') ?? '0');
  const hasPreselectedPatient = preselectedPatientId > 0;
  const { data: preselectedPatient } = usePatient(
    hasPreselectedPatient ? preselectedPatientId : 0,
  );

  const phase = useRecordSessionStore((state) => state.phase);
  const elapsed = useRecordSessionStore((state) => state.elapsed);
  const isPaused = useRecordSessionStore((state) => state.isPaused);
  const micError = useRecordSessionStore((state) => state.micError);
  const startNewRecording = useRecordSessionStore((state) => state.startNewRecording);
  const resetToIdle = useRecordSessionStore((state) => state.resetToIdle);
  const clearTimer = useRecordSessionStore((state) => state.clearTimer);

  const pause = useRecordSessionStore((state) => state.pause);
  const resume = useRecordSessionStore((state) => state.resume);
  const stop = useRecordSessionStore((state) => state.stop);

  useEffect(() => {
    autoSaveStarted.current = false;
    void startNewRecording();
    return () => {
      clearTimer();
      resetToIdle();
    };
  }, [clearTimer, resetToIdle, startNewRecording, preselectedPatientId]);

  const isRecording = phase === 'recording';
  const isSaving = createConsultation.isPending || uploadAudio.isPending;

  async function handleSave(patientId?: number) {
    setSaveError(null);
    const { savedDuration, audioFile: recordedFile } = useRecordSessionStore.getState();

    if (!recordedFile) {
      autoSaveStarted.current = false;
      setSaveError('No audio was captured. Check your microphone and try again.');
      return;
    }

    try {
      const measuredDuration = await resolveRecordingDuration(
        recordedFile,
        savedDuration,
      );

      const consultation = await createConsultation.mutateAsync({
        request: {
          ...(patientId != null ? { patientId } : {}),
          ...(measuredDuration != null ? { durationSeconds: measuredDuration } : {}),
        },
        idempotencyKey: createIdempotencyKey(),
      });

      await uploadAudio.mutateAsync({
        consultationId: consultation.id,
        audioFile: recordedFile,
        durationSeconds: measuredDuration,
      });

      if (patientId != null) {
        navigate(`/patients/${patientId}/consultations/${consultation.id}`);
      } else {
        navigate('/');
      }
    } catch (error) {
      autoSaveStarted.current = false;
      setSaveError((error as Error).message ?? 'Unable to save recording.');
    }
  }

  useEffect(() => {
    if (phase !== 'attach' || autoSaveStarted.current) return;
    autoSaveStarted.current = true;
    void handleSave(hasPreselectedPatient ? preselectedPatientId : undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- save once when entering attach after stop
  }, [phase, hasPreselectedPatient, preselectedPatientId]);

  const patientLabel = preselectedPatient
    ? formatPatientName(preselectedPatient.firstName, preselectedPatient.lastName)
    : hasPreselectedPatient
      ? `Patient ${preselectedPatientId}`
      : null;

  if (micError && phase === 'idle') {
    return (
      <div className="record-page record-page--attach">
        <div className="record-patient-banner panel">
          <p className="field__error" role="alert">
            {micError}
          </p>
          <button
            type="button"
            className="button button--primary"
            style={{ marginTop: 12 }}
            onClick={() => {
              void startNewRecording();
            }}
          >
            Try again
          </button>
        </div>
      </div>
    );
  }

  if (phase === 'attach') {
    const savingLabel = hasPreselectedPatient
      ? `Saving recording for ${patientLabel}…`
      : 'Saving recording…';

    return (
      <div className="record-page record-page--attach">
        <div className="record-patient-banner panel">
          <p>
            {isSaving || !saveError
              ? savingLabel
              : hasPreselectedPatient
                ? `Unable to save recording for ${patientLabel}.`
                : 'Unable to save recording.'}
          </p>
          {saveError ? (
            <div className="stack" style={{ marginTop: 12 }}>
              <p className="field__error" role="alert">
                {saveError}
              </p>
              <button
                type="button"
                className="button button--primary"
                onClick={() => {
                  autoSaveStarted.current = false;
                  void handleSave(hasPreselectedPatient ? preselectedPatientId : undefined);
                }}
              >
                Retry save
              </button>
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="record-page">
        {patientLabel ? (
          <div className="record-patient-banner" role="status">
            Recording for <strong>{patientLabel}</strong>
          </div>
        ) : null}

        <div className="record-session">
          <div
            className={`record-session__timer${isRecording && !isPaused ? ' record-session__timer--live' : ''}`}
          >
            {formatDuration(elapsed)}
          </div>

          <AudioVisualizer active={isRecording} paused={isPaused} />
        </div>
      </div>

      <div className="record-controls-dock">
        <RecordControls
          isPaused={isPaused}
          onPause={pause}
          onResume={resume}
          onStop={stop}
        />
      </div>
    </>
  );
}
