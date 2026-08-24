import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { ConsultationStatusIcon } from '../../../shared/components/ConsultationStatusIcon';
import {
  useConsultationPolling,
} from '../../../shared/components/ConsultationStatusStepper';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { queryKeys } from '../../../shared/constants/queryKeys';
import { parseStructuredSummary } from '../../../shared/utils/structuredData';
import { formatDate, formatDuration } from '../../../shared/utils/format';
import { DownloadIcon, SaveIcon } from '../../../layouts/navigation/NavIcons';
import { usePatient, usePatientHistory } from '../../patients';
import {
  useConsultation,
  useConsultationAudio,
  useRetryConsultationProcessing,
} from '../hooks/useConsultations';
import { consultationApi } from '../api/consultationApi';
import { useTranscript, TranscriptViewer } from '../../transcripts';
import { useStructuredData } from '../../medical-data';
import { useCreateDoctorNote, useDoctorNotes } from '../../doctor-notes';
import { RecordingPreviewPlayer } from '../../record';
import type { DoctorNote } from '../../../shared/types/api';

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
  const createNoteMutation = useCreateDoctorNote();
  const retryMutation = useRetryConsultationProcessing();
  const [content, setContent] = useState('');

  useEffect(() => {
    return () => {
      const objectUrl = queryClient.getQueryData<string | null>(
        queryKeys.consultationAudio(consultationIdNum),
      );
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [consultationIdNum, queryClient]);

  useConsultationPolling({
    status: consultation.data?.status ?? 'Draft',
    onPoll: () => {
      void consultation.refetch();
      void transcript.refetch();
      if (hasPatient) {
        void history.refetch();
        void queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistoryPrefix(resolvedPatientId),
        });
      }
      void queryClient.invalidateQueries({
        queryKey: queryKeys.structuredData(consultationIdNum),
      });
    },
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

  async function onCreateNote() {
    const trimmed = content.trim();
    if (!trimmed) return;

    await createNoteMutation.mutateAsync({
      consultationId: consultationIdNum,
      content: trimmed,
    });

    setContent('');
  }

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
        <ConsultationStatusIcon
          status={consultation.data.status}
          className="consultation-status-icon--header"
        />
      </div>

      {consultation.data.failureReason ? (
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
      ) : null}

      <div className="consultation-grid">
        {canShowPlayer ? (
          <section className="panel">
            <div className="panel-heading">
              <h3>Recording</h3>
              {hasStoredAudio ? (
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Download recording"
                  title="Download recording"
                  onClick={() => {
                    void consultationApi
                      .downloadAudio(consultationIdNum)
                      .catch((error: Error) => {
                        window.alert(error.message ?? 'Unable to download recording.');
                      });
                  }}
                >
                  <DownloadIcon />
                </button>
              ) : null}
            </div>
            {hasStoredAudio && audioQuery.isLoading ? (
              <p className="muted">Loading recording…</p>
            ) : hasStoredAudio && audioQuery.error ? (
              <ErrorMessage
                message={(audioQuery.error as Error).message ?? 'Unable to load recording'}
                onRetry={() => audioQuery.refetch()}
              />
            ) : (
              <RecordingPreviewPlayer
                durationSeconds={durationSeconds}
                audioSrc={hasStoredAudio ? audioQuery.data : null}
              />
            )}
          </section>
        ) : null}

        {hasStoredDocument ? (
          <section className="panel">
            <div className="panel-heading">
              <h3>Document</h3>
              <button
                type="button"
                className="icon-button"
                aria-label="Download PDF"
                title="Download PDF"
                onClick={() => {
                  void consultationApi
                    .downloadDocument(consultationIdNum, documentFileName)
                    .catch((error: Error) => {
                      window.alert(error.message ?? 'Unable to download document.');
                    });
                }}
              >
                <DownloadIcon />
              </button>
            </div>
            <p>{documentFileName}</p>
          </section>
        ) : null}

        {showTranscript ? (
          <TranscriptViewer
            consultationId={consultationIdNum}
            text={transcript.data?.transcript}
            status={transcript.data?.status ?? consultation.data.status}
            failureReason={transcript.data?.failureReason}
          />
        ) : null}

        <section className="panel">
          <h3>Summary</h3>
          {structuredDataQuery.isLoading ? (
            <p className="muted">Loading summary…</p>
          ) : summaryText ? (
            <p>{summaryText}</p>
          ) : (
            <p className="muted">
              {uploadedOnly
                ? 'No summary for this consultation.'
                : 'The summary is still processing.'}
            </p>
          )}
        </section>

        {hasPatient ? (
          <section className="panel">
            <h3>Doctor Notes</h3>

            {notesQuery.isLoading ? (
              <LoadingSkeleton label="Loading notes" />
            ) : notesQuery.error ? (
              <ErrorMessage
                message={(notesQuery.error as Error)?.message ?? 'Failed to load notes'}
              />
            ) : notesQuery.data && notesQuery.data.length > 0 ? (
              <div className="doctor-notes-list">
                {notesQuery.data.map((n: DoctorNote) => (
                  <div key={n.id} className="doctor-note">
                    <p className="muted doctor-note__meta">
                      {n.dateCreated ? new Date(n.dateCreated).toLocaleString() : null}
                    </p>
                    <p className="doctor-note__content">{n.content}</p>
                  </div>
                ))}
              </div>
            ) : (
              <p className="muted">No notes yet.</p>
            )}

            <div className="doctor-note-form">
              <label className="field">
                <span className="field__label">Note</span>
                <textarea
                  className="textarea"
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  placeholder="Write a clinical note for this consultation..."
                  rows={5}
                />
              </label>

              <div className="doctor-note-form__actions">
                <button
                  type="button"
                  className="icon-button icon-button--primary"
                  onClick={() => void onCreateNote()}
                  disabled={createNoteMutation.isPending || !content.trim()}
                  aria-label={createNoteMutation.isPending ? 'Saving note' : 'Save note'}
                  title="Save note"
                >
                  <SaveIcon />
                </button>
              </div>

              {createNoteMutation.error ? (
                <ErrorMessage
                  message={(createNoteMutation.error as Error)?.message ?? 'Failed to save note'}
                />
              ) : null}
            </div>
          </section>
        ) : null}
      </div>
    </div>
  );
}
