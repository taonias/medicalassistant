interface Props {
  status: string;
  processingStatuses?: string[];
}

const DEFAULT_PROCESSING = [
  'AudioUploaded',
  'DocumentUploaded',
  'DocumentProcessingPending',
  'Transcribing',
  'Transcribed',
  'StructuredDataPending',
];

export function ConsultationStatusStepper({
  status,
  processingStatuses = DEFAULT_PROCESSING,
}: Props) {
  const steps = ['Draft', 'Uploaded', 'Transcribing', 'Transcribed', 'StructuredDataPending', 'Completed'];
  const normalizedStatus =
    status === 'AudioUploaded' ||
    status === 'DocumentUploaded' ||
    status === 'DocumentProcessingPending'
      ? 'Uploaded'
      : status;
  const currentIndex = steps.indexOf(normalizedStatus);
  const isFailed = status === 'Failed';
  const isProcessing = processingStatuses.includes(status);

  return (
    <div className={`status-stepper ${isFailed ? 'status-stepper--failed' : ''}`}>
      {isFailed ? (
        <span className="status-stepper__failed">Processing failed</span>
      ) : (
        steps.map((step, index) => {
          const state =
            index < currentIndex
              ? 'complete'
              : index === currentIndex
                ? isProcessing && step !== 'Completed'
                  ? 'active'
                  : 'complete'
                : 'pending';

          return (
            <div key={step} className={`status-stepper__step status-stepper__step--${state}`}>
              <span className="status-stepper__dot" />
              <span className="status-stepper__label">{step.replace(/([A-Z])/g, ' $1').trim()}</span>
            </div>
          );
        })
      )}
    </div>
  );
}
