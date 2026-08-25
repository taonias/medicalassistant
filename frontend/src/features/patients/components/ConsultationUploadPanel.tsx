import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import {
  AudioUploader,
  isPdfFile,
  UploadProgress,
  useCreateConsultation,
  useUploadConsultationAudio,
  useUploadConsultationDocument,
} from '../../../modules/consultations';

interface Props {
  patientId: number;
}

function createIdempotencyKey() {
  return crypto.randomUUID();
}

export function ConsultationUploadPanel({ patientId }: Props) {
  const navigate = useNavigate();
  const createConsultation = useCreateConsultation();
  const uploadAudio = useUploadConsultationAudio();
  const uploadDocument = useUploadConsultationDocument();
  const [uploadProgress, setUploadProgress] = useState(0);
  const [selectedFileName, setSelectedFileName] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [pendingUpload, setPendingUpload] = useState<{
    file: File;
    durationSeconds?: number;
  } | null>(null);
  const [uploadExpanded, setUploadExpanded] = useState(false);

  const isUploading =
    createConsultation.isPending ||
    uploadAudio.isPending ||
    uploadDocument.isPending ||
    pendingUpload !== null;

  useEffect(() => {
    if (isUploading || uploadError) setUploadExpanded(true);
  }, [isUploading, uploadError]);

  async function handleUpload(file: File, durationSeconds?: number) {
    setUploadError(null);
    setSelectedFileName(file.name);
    setPendingUpload({ file, durationSeconds });
    setUploadProgress(10);

    try {
      const consultation = await createConsultation.mutateAsync({
        request: {
          patientId,
          ...(durationSeconds != null ? { durationSeconds } : {}),
        },
        idempotencyKey: createIdempotencyKey(),
      });
      setUploadProgress(50);

      if (isPdfFile(file)) {
        await uploadDocument.mutateAsync({
          consultationId: consultation.id,
          documentFile: file,
        });
      } else {
        await uploadAudio.mutateAsync({
          consultationId: consultation.id,
          audioFile: file,
          durationSeconds,
        });
      }
      setUploadProgress(100);
      navigate(`/patients/${patientId}/consultations/${consultation.id}`);
    } catch (error) {
      setPendingUpload({ file, durationSeconds });
      setUploadError((error as Error).message ?? 'Unable to upload consultation file.');
    }
  }

  function resetUpload() {
    setPendingUpload(null);
    setUploadError(null);
    setSelectedFileName(null);
    setUploadProgress(0);
  }

  return (
    <details
      className="panel collapsible-panel consultations-upload"
      open={uploadExpanded}
      onToggle={(event) => {
        setUploadExpanded(event.currentTarget.open);
      }}
    >
      <summary className="collapsible-panel__summary">
        <h2>Upload audio or PDF</h2>
      </summary>
      <div className="collapsible-panel__body">
        {uploadError ? (
          <div className="stack consultations-upload__status">
            {selectedFileName ? <p className="muted">{selectedFileName}</p> : null}
            <ErrorMessage message={uploadError} />
            <div className="consultations-upload__actions">
              <button
                type="button"
                className="button button--primary button--small"
                onClick={() => {
                  if (!pendingUpload) return;
                  void handleUpload(pendingUpload.file, pendingUpload.durationSeconds);
                }}
              >
                Retry upload
              </button>
              <button
                type="button"
                className="button button--ghost button--small"
                onClick={resetUpload}
              >
                Choose another file
              </button>
            </div>
          </div>
        ) : isUploading ? (
          <div className="stack consultations-upload__status">
            <UploadProgress progress={uploadProgress} fileName={selectedFileName ?? undefined} />
          </div>
        ) : (
          <AudioUploader
            onFileSelected={(file, durationSeconds) => {
              void handleUpload(file, durationSeconds);
            }}
          />
        )}
      </div>
    </details>
  );
}
