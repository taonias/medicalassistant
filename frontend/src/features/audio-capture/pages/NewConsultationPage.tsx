import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { AudioRecorder } from '../../audio-capture/components/AudioRecorder';
import { AudioUploader, isPdfFile } from '../../audio-capture/components/AudioUploader';
import { UploadProgress } from '../../audio-capture/components/UploadProgress';
import {
  useCreateConsultation,
  useUploadConsultationAudio,
  useUploadConsultationDocument,
} from '../../consultations/hooks/useConsultations';
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
  const uploadDocument = useUploadConsultationDocument();
  const [mode, setMode] = useState<CaptureMode>('record');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadProgress, setUploadProgress] = useState(0);

  async function submitFile(file: File, duration?: number) {
    setUploadProgress(10);
    const consultation = await createConsultation.mutateAsync({
      request: { patientId: id },
      idempotencyKey: createIdempotencyKey(),
    });
    setUploadProgress(45);

    if (isPdfFile(file)) {
      await uploadDocument.mutateAsync({
        consultationId: consultation.id,
        documentFile: file,
      });
    } else {
      await uploadAudio.mutateAsync({
        consultationId: consultation.id,
        audioFile: file,
        durationSeconds: duration,
      });
    }

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

  const isSubmitting =
    createConsultation.isPending || uploadAudio.isPending || uploadDocument.isPending;

  return (
    <div className="page">
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
              void submitFile(file, duration);
            }}
          />
        ) : (
          <>
            <AudioUploader
              disabled={isSubmitting}
              onFileSelected={(file, duration) => {
                setSelectedFile(file);
                void submitFile(file, duration);
              }}
            />
            {selectedFile ? <p className="muted">Selected: {selectedFile.name}</p> : null}
          </>
        )}

        {isSubmitting ? (
          <UploadProgress progress={uploadProgress} fileName={selectedFile?.name} />
        ) : null}

        {(createConsultation.error || uploadAudio.error || uploadDocument.error) && (
          <ErrorMessage message="Unable to create consultation or upload file. Please retry." />
        )}
      </section>

      <Link to={`/patients/${patient.id}`} className="button button--ghost">
        Back to patient
      </Link>
    </div>
  );
}
