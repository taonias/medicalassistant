import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useOutletContext, useSearchParams } from 'react-router-dom';
import type { ConsultationHistoryItem } from '../../../shared/types/api';
import { ConfirmModal } from '../../../shared/components/ConfirmModal';
import { ConsultationStatusIcon } from '../../../shared/components/ConsultationStatusIcon';
import { EmptyState } from '../../../shared/components/EmptyState';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { DiscardIcon } from '../../../layouts/navigation/NavIcons';
import { formatDate, formatDuration } from '../../../shared/utils/format';
import { AudioUploader, isPdfFile } from '../../audio-capture/components/AudioUploader';
import { UploadProgress } from '../../audio-capture/components/UploadProgress';
import {
  useCreateConsultation,
  useDeleteConsultation,
  useUploadConsultationAudio,
  useUploadConsultationDocument,
} from '../../consultations/hooks/useConsultations';
import { HistoryDateRangePicker } from '../components/HistoryDateRangePicker';
import { ConsultationPager } from '../components/ConsultationPager';
import { PatientDetailsPanel } from '../components/PatientDetailsPanel';
import { PatientOverviewCharts } from '../components/PatientOverviewCharts';
import { PatientDoctorNotesPanel } from '../../doctor-notes/components/PatientDoctorNotesPanel';
import { usePatient, usePatientHistory } from '../hooks/usePatients';
import { addDaysToDateInput, toDateInputValue } from '../utils/historyDateRange';

const CONSULTATIONS_PAGE_SIZE = 10;

interface PatientOutletContext {
  patientId: number;
}

function createIdempotencyKey() {
  return crypto.randomUUID();
}

type ConsultationSourceFilter = 'all' | 'audio' | 'pdf';
type ConsultationSourceKind = 'audio' | 'pdf' | 'unknown';

function parseSourceFilter(value: string | null): ConsultationSourceFilter {
  if (value === 'audio' || value === 'pdf') return value;
  return 'all';
}

function consultationSource(item: ConsultationHistoryItem): ConsultationSourceKind {
  const raw = item as ConsultationHistoryItem & {
    HasAudio?: boolean;
    HasDocument?: boolean;
    Source?: string;
  };
  const source = (item.source ?? raw.Source ?? '').toLowerCase();
  if (source === 'pdf') return 'pdf';
  if (source === 'audio') return 'audio';

  const hasAudio = Boolean(item.hasAudio ?? raw.HasAudio);
  const hasDocument = Boolean(item.hasDocument ?? raw.HasDocument);
  if (hasDocument && !hasAudio) return 'pdf';
  if (hasAudio) return 'audio';

  const status = item.status.replace(/\s+/g, '').toLowerCase();
  if (status === 'documentuploaded') return 'pdf';
  if (status === 'audiouploaded') return 'audio';
  return 'unknown';
}

function consultationSummaryText(item: ConsultationHistoryItem) {
  return item.structuredSummary?.trim() || null;
}

export function PatientOverviewTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const [isEditingDetails, setIsEditingDetails] = useState(false);
  const patientQuery = usePatient(patientId);
  const history = usePatientHistory(patientId);

  const patientSummary = patientQuery.data?.summary?.trim();
  const consultations = history.data?.consultations ?? [];
  const patientDataItems = consultations.filter(
    (item: ConsultationHistoryItem) => item.structuredSummary,
  );

  return (
    <div className="stack">
      {patientQuery.isLoading ? (
        <LoadingSkeleton label="Loading patient details" />
      ) : patientQuery.error || !patientQuery.data ? (
        <ErrorMessage
          message="Unable to load patient details"
          onRetry={() => {
            void patientQuery.refetch();
          }}
        />
      ) : (
        <PatientDetailsPanel
          patient={patientQuery.data}
          isEditing={isEditingDetails}
          onEdit={() => setIsEditingDetails(true)}
          onCancel={() => setIsEditingDetails(false)}
          onSaved={() => setIsEditingDetails(false)}
        />
      )}

      <section className="panel">
        <h2>Patient data</h2>
        {history.isLoading ? (
          <LoadingSkeleton label="Loading patient data" />
        ) : history.error ? (
          <ErrorMessage
            message="Unable to load patient data"
            onRetry={() => {
              void history.refetch();
            }}
          />
        ) : patientDataItems.length === 0 ? (
          <EmptyState
            title="No patient data yet"
            description="Structured extractions appear after consultations are processed."
          />
        ) : (
          <ul className="timeline">
            {patientDataItems.map((item) => (
              <li key={item.id} className="timeline__item timeline__item--block">
                <strong>{formatDate(item.consultationDate)}</strong>
                <p>{item.structuredSummary}</p>
                <Link
                  to={`/patients/${patientId}/consultations/${item.id}`}
                  className="button button--ghost button--small"
                >
                  Open consultation
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="panel">
        <h2>History summary</h2>
        {patientQuery.isLoading ? (
          <LoadingSkeleton label="Loading overview" />
        ) : patientQuery.error ? (
          <ErrorMessage
            message="Unable to load patient overview"
            onRetry={() => {
              void patientQuery.refetch();
            }}
          />
        ) : (
          <p>
            {patientSummary || 'No history summary is available for this patient yet.'}
          </p>
        )}
      </section>

      {history.isLoading ? (
        <section className="panel">
          <h2>Activity</h2>
          <LoadingSkeleton label="Loading activity charts" />
        </section>
      ) : history.error ? null : (
        <PatientOverviewCharts consultations={consultations} />
      )}

      <PatientDoctorNotesPanel patientId={patientId} />
    </div>
  );
}

export function PatientConsultationsTab() {
  const { patientId } = useOutletContext<PatientOutletContext>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const today = useMemo(() => toDateInputValue(), []);
  const defaultFromDate = useMemo(() => addDaysToDateInput(today, -6), [today]);

  const fromDate = searchParams.get('fromDate') ?? defaultFromDate;
  const toDate = searchParams.get('toDate') ?? today;
  const sourceFilter = parseSourceFilter(searchParams.get('source'));
  const page = Math.max(1, Number(searchParams.get('page') ?? '1') || 1);

  useEffect(() => {
    const next = new URLSearchParams(searchParams);
    let changed = false;
    if (!searchParams.get('fromDate')) {
      next.set('fromDate', defaultFromDate);
      changed = true;
    }
    if (!searchParams.get('toDate')) {
      next.set('toDate', today);
      changed = true;
    }
    if (changed) {
      setSearchParams(next, { replace: true });
    }
  }, [defaultFromDate, searchParams, setSearchParams, today]);

  const history = usePatientHistory(patientId, {
    fromDate,
    toDate,
    page,
    pageSize: CONSULTATIONS_PAGE_SIZE,
    source: sourceFilter,
  });
  const deleteConsultation = useDeleteConsultation();
  const createConsultation = useCreateConsultation();
  const uploadAudio = useUploadConsultationAudio();
  const uploadDocument = useUploadConsultationDocument();
  const [pendingDelete, setPendingDelete] = useState<ConsultationHistoryItem | null>(null);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [selectedFileName, setSelectedFileName] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [pendingUpload, setPendingUpload] = useState<{
    file: File;
    durationSeconds?: number;
  } | null>(null);
  const [uploadExpanded, setUploadExpanded] = useState(false);
  const [filtersExpanded, setFiltersExpanded] = useState(false);

  const consultations = history.data?.consultations ?? [];
  const totalConsultations = history.data?.totalConsultations ?? consultations.length;
  const totalPages = Math.max(1, Math.ceil(totalConsultations / CONSULTATIONS_PAGE_SIZE));

  useEffect(() => {
    if (page > totalPages) {
      const next = new URLSearchParams(searchParams);
      next.set('page', String(totalPages));
      setSearchParams(next, { replace: true });
    }
  }, [page, searchParams, setSearchParams, totalPages]);

  const isUploading =
    createConsultation.isPending ||
    uploadAudio.isPending ||
    uploadDocument.isPending ||
    pendingUpload !== null;

  useEffect(() => {
    if (isUploading || uploadError) setUploadExpanded(true);
  }, [isUploading, uploadError]);

  function patchSearchParams(
    patch: {
      fromDate?: string;
      toDate?: string;
      source?: ConsultationSourceFilter;
      page?: number;
    },
    replace = true,
  ) {
    const next = new URLSearchParams(searchParams);
    if (patch.fromDate != null) next.set('fromDate', patch.fromDate);
    if (patch.toDate != null) next.set('toDate', patch.toDate);
    if (patch.source != null) {
      if (patch.source === 'all') next.delete('source');
      else next.set('source', patch.source);
    }
    if (patch.page != null) {
      if (patch.page <= 1) next.delete('page');
      else next.set('page', String(patch.page));
    }
    setSearchParams(next, { replace });
  }

  function updateDateRange(nextFrom: string, nextTo: string) {
    patchSearchParams({ fromDate: nextFrom, toDate: nextTo, page: 1 });
  }

  function updateSourceFilter(next: ConsultationSourceFilter) {
    patchSearchParams({ source: next, page: 1 });
  }

  function setPage(nextPage: number) {
    patchSearchParams({ page: nextPage });
  }

  async function confirmDelete() {
    if (!pendingDelete) return;

    try {
      await deleteConsultation.mutateAsync({
        consultationId: pendingDelete.id,
        patientId,
      });
      setPendingDelete(null);
    } catch {
      // Error surfaced via deleteConsultation.error below.
    }
  }

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
    <div className="stack">
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

      <details
        className="panel collapsible-panel consultations-filters"
        open={filtersExpanded}
        onToggle={(event) => {
          setFiltersExpanded(event.currentTarget.open);
        }}
      >
        <summary className="collapsible-panel__summary">
          <h2>Filters</h2>
        </summary>
        <div className="collapsible-panel__body">
          <div className="consultations-filters__fields">
            <div className="consultations-filters__field">
              <span className="consultations-filters__label" id="consultations-filter-period-label">
                Period
              </span>
              <HistoryDateRangePicker
                fromDate={fromDate}
                toDate={toDate}
                onChange={updateDateRange}
                hideLabel
              />
            </div>
            <div className="consultations-filters__field">
              <span className="consultations-filters__label" id="consultations-filter-type-label">
                Type
              </span>
              <div
                className="consultation-source-filter"
                role="group"
                aria-labelledby="consultations-filter-type-label"
              >
                {(
                  [
                    { value: 'all', label: 'All' },
                    { value: 'audio', label: 'Audio' },
                    { value: 'pdf', label: 'PDF' },
                  ] as const
                ).map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    className={[
                      'consultation-source-filter__button',
                      sourceFilter === option.value
                        ? 'consultation-source-filter__button--active'
                        : undefined,
                    ]
                      .filter(Boolean)
                      .join(' ')}
                    aria-pressed={sourceFilter === option.value}
                    onClick={() => updateSourceFilter(option.value)}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        </div>
      </details>

      <section className="panel consultations-results">
        <h2>Consultations</h2>
        <ConsultationPager
          className="consultation-pager--top"
          page={page}
          pageSize={CONSULTATIONS_PAGE_SIZE}
          totalCount={totalConsultations}
          onPageChange={setPage}
          disabled={history.isFetching}
        />
        {deleteConsultation.error ? (
          <ErrorMessage
            message={(deleteConsultation.error as Error).message ?? 'Unable to delete consultation'}
          />
        ) : null}
        {history.isLoading ? (
          <LoadingSkeleton label="Loading consultation history" />
        ) : history.error ? (
          <ErrorMessage
            message="Unable to load consultation history"
            onRetry={() => {
              void history.refetch();
            }}
          />
        ) : consultations.length === 0 ? (
          <EmptyState
            title={
              sourceFilter === 'all'
                ? 'No consultations in this period'
                : sourceFilter === 'audio'
                  ? 'No audio consultations in this period'
                  : 'No PDF consultations in this period'
            }
          />
        ) : (
          <ul className="timeline">
            {consultations.map((item: ConsultationHistoryItem) => {
              const summaryText = consultationSummaryText(item);
              const source = consultationSource(item);
              const showDuration =
                source !== 'pdf' &&
                item.durationSeconds != null &&
                !Number.isNaN(item.durationSeconds);
              const sourceLabel = source === 'pdf' ? 'PDF' : source === 'audio' ? 'Audio' : null;

              return (
                <li
                  key={item.id}
                  className={[
                    'timeline__item',
                    source === 'pdf' ? 'timeline__item--pdf' : undefined,
                    source === 'audio' ? 'timeline__item--audio' : undefined,
                  ]
                    .filter(Boolean)
                    .join(' ')}
                >
                  <Link
                    to={`/patients/${patientId}/consultations/${item.id}`}
                    className="timeline__item-link"
                  >
                    <div className="timeline__item-main">
                      <div className="timeline__leading">
                        {sourceLabel ? (
                          <span
                            className={[
                              'timeline__source',
                              source === 'pdf'
                                ? 'timeline__source--pdf'
                                : 'timeline__source--audio',
                            ].join(' ')}
                          >
                            {sourceLabel}
                          </span>
                        ) : null}
                        <ConsultationStatusIcon status={item.status} />
                      </div>
                      <div className="timeline__datetime">
                        <span className="timeline__date">{formatDate(item.consultationDate)}</span>
                        {showDuration ? (
                          <span className="timeline__duration muted">
                            {formatDuration(item.durationSeconds)}
                          </span>
                        ) : null}
                      </div>
                    </div>
                    {summaryText ? (
                      <p className="timeline__summary">{summaryText}</p>
                    ) : null}
                  </Link>
                  <button
                    type="button"
                    className="icon-button icon-button--danger timeline__delete"
                    aria-label={`Delete consultation from ${formatDate(item.consultationDate)}`}
                    title="Delete consultation"
                    disabled={deleteConsultation.isPending}
                    onClick={(event) => {
                      event.preventDefault();
                      event.stopPropagation();
                      setPendingDelete(item);
                    }}
                  >
                    <DiscardIcon />
                  </button>
                </li>
              );
            })}
          </ul>
        )}
        <ConsultationPager
          className="consultation-pager--bottom"
          page={page}
          pageSize={CONSULTATIONS_PAGE_SIZE}
          totalCount={totalConsultations}
          onPageChange={setPage}
          disabled={history.isFetching}
        />
      </section>

      {pendingDelete ? (
        <ConfirmModal
          title="Delete consultation"
          message={`Delete the consultation from ${formatDate(pendingDelete.consultationDate)}? This cannot be undone.`}
          confirmLabel="Delete"
          cancelLabel="Cancel"
          danger
          isPending={deleteConsultation.isPending}
          onCancel={() => {
            if (!deleteConsultation.isPending) setPendingDelete(null);
          }}
          onConfirm={() => void confirmDelete()}
        />
      ) : null}
    </div>
  );
}