import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import {
  ConsultationStatusStepper,
  useConsultationPolling,
} from '../../../shared/components/ConsultationStatusStepper';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { queryKeys } from '../../../shared/constants/queryKeys';
import { parseStructuredSummary } from '../../../shared/utils/structuredData';
import {
  consultationStatusLabel,
  formatDate,
  formatDuration,
} from '../../../shared/utils/format';
import { StructuredDataPanel } from '../../medical-data/components/StructuredDataPanel';
import { usePatient, usePatientHistory } from '../../patients/hooks/usePatients';
import { useConsultation } from '../hooks/useConsultations';
import { useTranscript } from '../../transcripts/hooks/useTranscript';
import { TranscriptViewer } from '../../transcripts/components/TranscriptViewer';
import { useStructuredData } from '../../medical-data/hooks/useStructuredData';
import { useApproveStructuredData } from '../../medical-data/hooks/useApproveStructuredData';
import {
  useCreateDoctorNote,
  useDoctorNotes,
} from '../../doctor-notes/hooks/useDoctorNotes';
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
  const transcript = useTranscript(
    consultationIdNum,
    consultation.data?.status !== 'Draft',
  );
  const history = usePatientHistory(hasPatient ? resolvedPatientId : 0);
  const structuredDataQuery = useStructuredData(consultationIdNum);
  const approveStructuredDataMutation = useApproveStructuredData();

  const notesQuery = useDoctorNotes(consultationIdNum);
  const createNoteMutation = useCreateDoctorNote();
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');

  useConsultationPolling({
    status: consultation.data?.status ?? 'Draft',
    onPoll: () => {
      void consultation.refetch();
      void transcript.refetch();
      if (hasPatient) {
        void history.refetch();
        void queryClient.invalidateQueries({
          queryKey: queryKeys.patientHistory(resolvedPatientId),
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

  async function onCreateNote() {
    const trimmed = content.trim();
    if (!trimmed) return;

    await createNoteMutation.mutateAsync({
      consultationId: consultationIdNum,
      title: title.trim() ? title.trim() : undefined,
      content: trimmed,
    });

    setTitle('');
    setContent('');
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1>Consultation</h1>
          <p className="muted">
            {formatDate(consultation.data.consultationDate)}{' · '}
            {formatDuration(consultation.data.durationSeconds)}
          </p>
        </div>
        {hasPatient ? (
          <div className="page-header__actions">
            <Link
              to={`/patients/${resolvedPatientId}/chat?consultation=${consultationId}`}
              className="button button--secondary"
            >
              Ask in chat
            </Link>
          </div>
        ) : null}
      </header>

      <ConsultationStatusStepper status={consultation.data.status} />

      {consultation.data.failureReason ? (
        <ErrorMessage message={consultation.data.failureReason} />
      ) : null}

      <div className="consultation-grid">
        <TranscriptViewer
          text={transcript.data?.rawText}
          status={transcript.data?.status ?? consultation.data.status}
          failureReason={transcript.data?.failureReason}
        />

        <section className="panel">
          <h3>Summary</h3>
          {structuredDataQuery.isLoading ? (
            <p className="muted">Loading summary…</p>
          ) : structuredData.summary ? (
            <p>{structuredData.summary}</p>
          ) : (
            <p className="muted">
              Summary will appear after structured data extraction ({consultationStatusLabel(consultation.data.status)}).
            </p>
          )}

          {structuredDataQuery.data && !structuredDataQuery.data.approved ? (
            <div style={{ marginTop: 12 }}>
              <button
                type="button"
                className="button button--primary"
                onClick={() =>
                  void approveStructuredDataMutation.mutateAsync(consultationIdNum)
                }
                disabled={approveStructuredDataMutation.isPending}
              >
                {approveStructuredDataMutation.isPending
                  ? 'Approving...'
                  : 'Approve Structured Data'}
              </button>

              {approveStructuredDataMutation.error ? (
                <ErrorMessage
                  message={
                    (approveStructuredDataMutation.error as Error)?.message ??
                    'Failed to approve structured data'
                  }
                />
              ) : null}
            </div>
          ) : null}
        </section>

        <StructuredDataPanel data={structuredData} />

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
                    {n.title ? <strong>{n.title}</strong> : null}
                    <p className="muted" style={{ marginTop: 4 }}>
                      {n.dateCreated ? new Date(n.dateCreated).toLocaleString() : null}
                    </p>
                    <p style={{ whiteSpace: 'pre-wrap' }}>{n.content}</p>
                  </div>
                ))}
              </div>
            ) : (
              <p className="muted">No notes yet.</p>
            )}

            <div className="doctor-note-form" style={{ marginTop: 16 }}>
              <label className="field">
                <span className="field__label">Title (optional)</span>
                <input
                  className="input"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="e.g. Assessment"
                />
              </label>

              <label className="field" style={{ marginTop: 8 }}>
                <span className="field__label">Note</span>
                <textarea
                  className="textarea"
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  placeholder="Write a clinical note for this consultation..."
                  rows={5}
                />
              </label>

              <div style={{ marginTop: 12 }}>
                <button
                  type="button"
                  className="button button--primary"
                  onClick={() => void onCreateNote()}
                  disabled={createNoteMutation.isPending}
                >
                  {createNoteMutation.isPending ? 'Saving...' : 'Save note'}
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
