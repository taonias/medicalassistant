import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { consultationKeys } from '../queryKeys';
import { parseStructuredSummary } from '../../../shared/utils/structuredData';
import { formatDate, formatDateTime, formatDuration } from '../../../shared/utils/format';
import { usePatient, usePatientHistory, patientKeys } from '../../../features/patients';
import {
  useConsultation,
  useConsultationAudio,
  useRetryConsultationProcessing,
} from '../hooks/useConsultations';
import { useConsultationStatusUpdates } from '../hooks/useConsultationStatusUpdates';
import {
  useTranscript,
  TranscriptViewer,
  useStructuredData,
  structuredDataKeys,
  useDoctorNotes,
} from '../../clinical-record';
import { RecordingPanel } from './consultation-detail/RecordingPanel';
import { DocumentPanel } from './consultation-detail/DocumentPanel';
import { SummaryPanel } from './consultation-detail/SummaryPanel';
import { ConsultationDoctorNotesPanel } from './consultation-detail/ConsultationDoctorNotesPanel';

export function ConsultationDetailPage() {
  const { patientId: patientIdParam, consultationId = '0' } = useParams();
  const consultationIdNum = Number(consultationId);
  const queryClient = useQueryClient();

  const consultation = useConsultation(consultationIdNum);
  const resolvedPatientId =
    patientIdParam != null
      ? Number(patientIdParam)
      : consultation.data?.patientId ?? 0;
  const hasPatient = resolvedPatientId > 0;

  const patient = usePatient(hasPatient ? resolvedPatientId : 0);
  const consultationStatus = consultation.data?.status;
  const hasStoredAudio = Boolean(consultation.data?.audioBlobUri);
  const hasStoredDocument = Boolean(consultation.data?.documentBlobUri);
  const isPdfConsultation = hasStoredDocument && !hasStoredAudio;
  // Always try to load transcript when present; 404 is treated as null in the hook.
  const transcript = useTranscript(consultationIdNum, consultationIdNum > 0);
  const showTranscript =
    !isPdfConsultation || Boolean(transcript.data?.transcript);
  const audioQuery = useConsultationAudio(consultationIdNum, hasStoredAudio);
  const history = usePatientHistory(hasPatient ? resolvedPatientId : 0);
  const structuredDataQuery = useStructuredData(consultationIdNum, consultationStatus);

  const notesQuery = useDoctorNotes(consultationIdNum);
  const retryMutation = useRetryConsultationProcessing();

  useEffect(() => {
    return () => {
      const objectUrl = queryClient.getQueryData<string | null>(
        consultationKeys.consultationAudio(consultationIdNum),
      );
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [consultationIdNum, queryClient]);

  useConsultationStatusUpdates((event) => {
    if (event.consultationId !== consultationIdNum) return;

    void consultation.refetch();
    void transcript.refetch();
    if (hasPatient) {
      void history.refetch();
      void queryClient.invalidateQueries({
        queryKey: patientKeys.patientHistoryPrefix(resolvedPatientId),
      });
    }
    void queryClient.invalidateQueries({
      queryKey: structuredDataKeys.structuredData(consultationIdNum),
    });
  });

  if (consultation.isLoading || (hasPatient && patient.isLoading)) {
    return <LoadingSkeleton label="Loading consultation" />;
  }

  if (consultation.error || !consultation.data) {
    return (
      <ErrorMessage
        message={(consultation.error as Error)?.message ?? 'Consultation not found'}
        onRetry={() => consultation.refetch()}
      />
    );
  }

  const structuredData = parseStructuredSummary(
    structuredDataQuery.data?.structuredPayload,
  );
  const summaryText = structuredData.summary?.trim();
  const durationSeconds = consultation.data.durationSeconds ?? 0;
  const canShowPlayer = hasStoredAudio || (!isPdfConsultation && durationSeconds > 0);
  const uploadedOnly =
    consultation.data.status === 'AudioUploaded' ||
    consultation.data.status === 'DocumentUploaded' ||
    consultation.data.status === 'DocumentProcessingPending';
  const documentFileName = consultation.data.documentFileName ?? 'consultation.pdf';

  return (
    <div className="page">
      <div className="consultation-meta">
        <p className="muted consultation-meta__info">
          {formatDate(consultation.data.consultationDate)}
          {!isPdfConsultation ? (
            <>
              {' · '}
              {formatDuration(consultation.data.durationSeconds)}
            </>
          ) : null}
        </p>
      </div>

      {consultation.data.failureReason ? (
        <div className="stack">
          <p className="muted">
            Failed{consultation.data.dateModified
              ? ` at ${formatDateTime(consultation.data.dateModified)}`
              : ''}
            {' · '}consultation #{consultation.data.id}
          </p>
          <ErrorMessage
            message={
              retryMutation.isPending
                ? 'Retrying…'
                : consultation.data.failureReason
            }
            onRetry={
              retryMutation.isPending
                ? undefined
                : () =>
                    retryMutation.mutate({ consultationId: consultationIdNum })
            }
          />
        </div>
      ) : null}

      <div className="consultation-grid">
        {canShowPlayer ? (
          <RecordingPanel
            consultationId={consultationIdNum}
            status={consultation.data.status}
            durationSeconds={durationSeconds}
            hasStoredAudio={hasStoredAudio}
            audioIsLoading={audioQuery.isLoading}
            audioError={audioQuery.error}
            audioSrc={audioQuery.data}
            onRetryAudio={() => audioQuery.refetch()}
          />
        ) : null}

        {hasStoredDocument ? (
          <DocumentPanel
            consultationId={consultationIdNum}
            documentFileName={documentFileName}
            status={consultation.data.status}
          />
        ) : null}

        {showTranscript ? (
          <TranscriptViewer
            consultationId={consultationIdNum}
            text={transcript.data?.transcript}
            status={transcript.data?.status ?? consultation.data.status}
            failureReason={transcript.data?.failureReason}
          />
        ) : null}

        <SummaryPanel
          isLoading={structuredDataQuery.isLoading}
          summaryText={summaryText}
          uploadedOnly={uploadedOnly}
        />

        {hasPatient ? (
          <ConsultationDoctorNotesPanel
            consultationId={consultationIdNum}
            notes={notesQuery.data}
            isLoading={notesQuery.isLoading}
            error={notesQuery.error}
          />
        ) : null}
      </div>
    </div>
  );
}
