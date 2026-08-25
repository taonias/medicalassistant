import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ConfirmModal } from '../../../shared/components/ConfirmModal';
import { ConsultationStatusIcon } from '../../../shared/components/ConsultationStatusIcon';
import { EmptyState } from '../../../shared/components/EmptyState';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { DiscardIcon } from '../../../app/shell/navigation/NavIcons';
import { formatDate, formatDuration } from '../../../shared/utils/format';
import { useDeleteConsultation } from '../../../modules/consultations';
import type { ConsultationHistoryItem } from '../types';
import { usePatientHistory } from '../hooks/usePatients';
import {
  consultationSource,
  consultationSummaryText,
  type ConsultationSourceFilter,
} from '../utils/consultationSourceFilter';
import { ConsultationPager } from './ConsultationPager';

const CONSULTATIONS_PAGE_SIZE = 10;

interface Props {
  patientId: number;
  fromDate: string;
  toDate: string;
  sourceFilter: ConsultationSourceFilter;
  page: number;
  onPageChange: (page: number) => void;
}

export function ConsultationHistoryPanel({
  patientId,
  fromDate,
  toDate,
  sourceFilter,
  page,
  onPageChange,
}: Props) {
  const history = usePatientHistory(patientId, {
    fromDate,
    toDate,
    page,
    pageSize: CONSULTATIONS_PAGE_SIZE,
    source: sourceFilter,
  });
  const deleteConsultation = useDeleteConsultation();
  const [pendingDelete, setPendingDelete] = useState<ConsultationHistoryItem | null>(null);

  const consultations = history.data?.consultations ?? [];
  const totalConsultations = history.data?.totalConsultations ?? consultations.length;
  const totalPages = Math.max(1, Math.ceil(totalConsultations / CONSULTATIONS_PAGE_SIZE));

  useEffect(() => {
    if (page > totalPages) {
      onPageChange(totalPages);
    }
  }, [page, totalPages, onPageChange]);

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

  return (
    <>
      <section className="panel consultations-results">
        <h2>Consultations</h2>
        <ConsultationPager
          className="consultation-pager--top"
          page={page}
          pageSize={CONSULTATIONS_PAGE_SIZE}
          totalCount={totalConsultations}
          onPageChange={onPageChange}
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
          onPageChange={onPageChange}
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
    </>
  );
}
