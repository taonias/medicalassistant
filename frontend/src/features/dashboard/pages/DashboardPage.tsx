import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { EmptyState } from '../../../shared/components/EmptyState';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { formatDate, formatDuration } from '../../../shared/utils/format';
import type { ConsultationSummary } from '../../consultations';
import type { DashboardAnalytics } from '../types';
import { useQueryClient } from '@tanstack/react-query';
import {
  useAssignConsultationPatient,
  useConsultationAudio,
  useDashboardAnalytics,
  useUnattachedDraftConsultations,
  consultationKeys,
} from '../../consultations';
import { PatientAttachPanel, RecordingPreviewPlayer } from '../../record';
import {
  PatientMixChart,
  StatusDonutChart,
  VolumeBarChart,
} from '../components/DashboardCharts';

export function DashboardPage() {
  const analyticsQuery = useDashboardAnalytics();
  const unattachedQuery = useUnattachedDraftConsultations();

  const analytics = analyticsQuery.data;
  const unattached = unattachedQuery.data ?? [];

  const isLoading = analyticsQuery.isLoading || unattachedQuery.isLoading;
  const error = analyticsQuery.error ?? unattachedQuery.error;

  function refetchAll() {
    void analyticsQuery.refetch();
    void unattachedQuery.refetch();
  }

  return (
    <div className="page">
      {isLoading ? (
        <LoadingSkeleton label="Loading dashboard" />
      ) : error ? (
        <ErrorMessage message={(error as Error).message} onRetry={refetchAll} />
      ) : (
        <div className="dashboard-sections stack">
          {analytics ? <OverviewSection analytics={analytics} /> : null}

          <section className="dashboard-section">
            <header className="dashboard-section__header">
              <h2>Unassigned recordings</h2>
              <p className="muted">
                Play each recording and attach it to a patient.
              </p>
            </header>

            {unattached.length > 0 ? (
              <div className="accordion-list">
                {unattached.map((consultation) => (
                  <UnattachedRecordingCard
                    key={consultation.id}
                    consultation={consultation}
                  />
                ))}
              </div>
            ) : (
              <EmptyState
                title="No unassigned recordings"
                description="Recordings saved without a patient appear here so you can link them."
              />
            )}
          </section>

          {!analytics && unattached.length === 0 ? (
            <EmptyState
              title="No dashboard data yet"
              description="Create patients and consultations to see activity here."
              action={
                <Link to="/record" className="button button--primary">
                  Start recording
                </Link>
              }
            />
          ) : null}
        </div>
      )}
    </div>
  );
}

function OverviewSection({ analytics }: { analytics: DashboardAnalytics }) {
  const averageDuration =
    analytics.averageDurationSeconds != null
      ? formatDuration(Math.round(analytics.averageDurationSeconds))
      : '—';

  return (
    <section className="dashboard-section">
      <header className="dashboard-section__header">
        <h2>Practice overview</h2>
        <p className="muted">
          Patient coverage, consultation pipeline, and recent volume.
        </p>
      </header>

      <div className="dashboard-kpi-grid">
        <article className="dashboard-kpi panel">
          <p className="dashboard-kpi__label muted">Patients</p>
          <p className="dashboard-kpi__value">{analytics.totalPatients}</p>
        </article>
        <article className="dashboard-kpi panel">
          <p className="dashboard-kpi__label muted">Consultations</p>
          <p className="dashboard-kpi__value">{analytics.totalConsultations}</p>
        </article>
        <article className="dashboard-kpi panel">
          <p className="dashboard-kpi__label muted">Processing</p>
          <p className="dashboard-kpi__value">{analytics.processingCount}</p>
        </article>
        <article className="dashboard-kpi panel">
          <p className="dashboard-kpi__label muted">Avg. duration</p>
          <p className="dashboard-kpi__value">{averageDuration}</p>
        </article>
      </div>

      <div className="dashboard-chart-grid">
        <article className="panel dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <h3>Consultation status</h3>
            <p className="muted">Where consultations sit in the pipeline</p>
          </header>
          <StatusDonutChart items={analytics.statusBreakdown} />
        </article>

        <article className="panel dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <h3>Volume · last 14 days</h3>
            <p className="muted">Daily consultations recorded</p>
          </header>
          <VolumeBarChart items={analytics.consultationsLast14Days} />
        </article>

        <article className="panel dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <h3>Patient coverage</h3>
            <p className="muted">Patients with at least one consultation</p>
          </header>
          <PatientMixChart analytics={analytics} />
          <dl className="dashboard-outcome-stats">
            <div>
              <dt className="muted">Completed</dt>
              <dd>{analytics.completedCount}</dd>
            </div>
            <div>
              <dt className="muted">Failed</dt>
              <dd>{analytics.failedCount}</dd>
            </div>
            <div>
              <dt className="muted">Unassigned</dt>
              <dd>{analytics.unassignedRecordingCount}</dd>
            </div>
          </dl>
        </article>
      </div>
    </section>
  );
}

function UnattachedRecordingCard({
  consultation,
}: {
  consultation: ConsultationSummary;
}) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const assignPatient = useAssignConsultationPatient();
  const [isOpen, setIsOpen] = useState(false);
  const [assignError, setAssignError] = useState<string | null>(null);
  const [savingPatientId, setSavingPatientId] = useState<number | null>(null);

  const hasStoredAudio = consultation.hasAudio;
  const audioQuery = useConsultationAudio(consultation.id, hasStoredAudio && isOpen);
  const durationSeconds = consultation.durationSeconds ?? 0;

  useEffect(() => {
    return () => {
      const objectUrl = queryClient.getQueryData<string | null>(
        consultationKeys.consultationAudio(consultation.id),
      );
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [consultation.id, queryClient]);

  async function handleAssign(patientId: number) {
    setAssignError(null);
    setSavingPatientId(patientId);
    try {
      await assignPatient.mutateAsync({
        consultationId: consultation.id,
        patientId,
      });
      navigate(`/patients/${patientId}/consultations/${consultation.id}`);
    } catch (err) {
      setSavingPatientId(null);
      setAssignError((err as Error).message ?? 'Unable to attach recording to patient.');
    }
  }

  return (
    <details
      className="accordion-item"
      onToggle={(event) => setIsOpen(event.currentTarget.open)}
    >
      <summary className="accordion-item__summary">
        <span className="accordion-item__title">
          {formatDate(consultation.consultationDate)}
        </span>
        <span className="accordion-item__meta muted">
          {consultation.hasAudio
            ? formatDuration(consultation.durationSeconds)
            : consultation.hasDocument
              ? 'PDF'
              : consultation.status}
        </span>
      </summary>

      <div className="accordion-item__content dashboard-unattached-card">
        <div className="dashboard-unattached-card__player">
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
        </div>

        <PatientAttachPanel
          durationSeconds={durationSeconds}
          isSaving={assignPatient.isPending}
          savingPatientId={savingPatientId}
          saveError={assignError}
          onPatientSelect={(patientId) => void handleAssign(patientId)}
          compact
        />
      </div>
    </details>
  );
}
