import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateConsultation } from '../../consultations/hooks/useConsultations';
import { AudioVisualizer } from '../components/AudioVisualizer';
import { PatientAttachPanel } from '../components/PatientAttachPanel';
import { RecordControls } from '../components/RecordControls';
import { useRecordSessionStore } from '../store/recordSessionStore';

function createIdempotencyKey() {
  return crypto.randomUUID();
}

function formatDuration(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

export function RecordPage() {
  const navigate = useNavigate();
  const createConsultation = useCreateConsultation();
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savingPatientId, setSavingPatientId] = useState<number | null>(null);

  const phase = useRecordSessionStore((state) => state.phase);
  const elapsed = useRecordSessionStore((state) => state.elapsed);
  const savedDuration = useRecordSessionStore((state) => state.savedDuration);
  const isPaused = useRecordSessionStore((state) => state.isPaused);
  const startNewRecording = useRecordSessionStore((state) => state.startNewRecording);
  const resetToIdle = useRecordSessionStore((state) => state.resetToIdle);
  const clearTimer = useRecordSessionStore((state) => state.clearTimer);

  const pause = useRecordSessionStore((state) => state.pause);
  const resume = useRecordSessionStore((state) => state.resume);
  const stop = useRecordSessionStore((state) => state.stop);

  useEffect(() => {
    startNewRecording();
    return () => {
      clearTimer();
      resetToIdle();
    };
  }, [clearTimer, resetToIdle, startNewRecording]);

  const isRecording = phase === 'recording';
  const isSaving = createConsultation.isPending;

  async function handleSave(patientId: number) {
    setSaveError(null);
    setSavingPatientId(patientId);
    try {
      const consultation = await createConsultation.mutateAsync({
        request: { patientId },
        idempotencyKey: createIdempotencyKey(),
      });
      navigate(`/patients/${patientId}/consultations/${consultation.id}`);
    } catch (error) {
      setSavingPatientId(null);
      setSaveError((error as Error).message ?? 'Unable to save recording.');
    }
  }

  if (phase === 'attach') {
    return (
      <div className="record-page record-page--attach">
        <PatientAttachPanel
          durationSeconds={savedDuration}
          isSaving={isSaving}
          savingPatientId={savingPatientId}
          saveError={saveError}
          onPatientSelect={(patientId) => void handleSave(patientId)}
        />
      </div>
    );
  }

  return (
    <>
      <div className="record-page">
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
