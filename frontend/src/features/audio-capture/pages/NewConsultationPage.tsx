import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { AudioRecorder } from '../../audio-capture/components/AudioRecorder';
import { AudioUploader } from '../../audio-capture/components/AudioUploader';
import { UploadProgress } from '../../audio-capture/components/UploadProgress';
import { useCreateConsultation, useUploadConsultationAudio } from '../../consultations/hooks/useConsultations';
import { usePatient } from '../../patients/hooks/usePatients';

import { CaptureModeTabs, type CaptureMode } from '../../audio-capture/components/CaptureModeTabs';

function createIdempotencyKey() {
  return crypto.randomUUID();
}

export function NewConsultationPage() {
  const { patientId = '0' } = useParams();
  const id = Number(patientId);
  const navigate = useNavigate();
  const { data: patient, isLoading, error, refetch } = usePatient(id);
  const createConsultation = useCreateConsultation();
  const uploadAudio = useUploadConsultationAudio();
  const [mode, setMode] = useState<CaptureMode>('record');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [durationSeconds, setDurationSeconds] = useState<number | undefined>();
  const [uploadProgress, setUploadProgress] = useState(0);

  async function submitAudio(file: File, duration?: number) {
    setUploadProgress(10);
    const consultation = await createConsultation.mutateAsync({
      request: { patientId: id },
      idempotencyKey: createIdempotencyKey(),
    });
    setUploadProgress(45);
    await uploadAudio.mutateAsync({
      consultationId: consultation.id,
      audioFile: file,
      durationSeconds: duration,
    });
    setUploadProgress(100);
    navigate(`/patients/${id}/consultations/${consultation.id}`);
  }

  if (isLoading) return <LoadingSkeleton label="Loading patient" />;
  if (error || !patient) {
    return (
      <ErrorMessage
        message={(error as Error)?.message ?? 'Patient not found'}
        onRetry={() => refetch()}
      />
    );
  }

  const isSubmitting = createConsultation.isPending || uploadAudio.isPending;

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1>New consultation</h1>
          <p className="muted">Record live audio or upload an existing file</p>
        </div>
      </header>

      <CaptureModeTabs mode={mode} onChange={setMode} />

      <section className="panel" role="tabpanel">
        <p className="consent-notice">
          Ensure patient consent for recording per your organization&apos;s policy. Access is logged.
        </p>

        {mode === 'record' ? (
          <AudioRecorder
            disabled={isSubmitting}
            onRecordingComplete={(file, duration) => {
              setSelectedFile(file);
              setDurationSeconds(duration);
              void submitAudio(file, duration);
            }}
          />
        ) : (
          <>
            <AudioUploader
              disabled={isSubmitting}
              onFileSelected={(file) => {
                setSelectedFile(file);
                void submitAudio(file, durationSeconds);
              }}
            />
            {selectedFile ? <p className="muted">Selected: {selectedFile.name}</p> : null}
          </>
        )}

        {isSubmitting ? <UploadProgress progress={uploadProgress} /> : null}

        {(createConsultation.error || uploadAudio.error) && (
          <ErrorMessage message="Unable to create consultation or upload audio. Please retry." />
        )}
      </section>

      <Link to={`/patients/${patient.id}`} className="button button--ghost">
        Back to patient
      </Link>
    </div>
  );
}
