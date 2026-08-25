import { useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { parseSourceFilter, type ConsultationSourceFilter } from '../utils/consultationSourceFilter';
import { addDaysToDateInput, toDateInputValue } from '../utils/historyDateRange';

/**
 * URL-driven filter/pagination state for the patient consultations history
 * view: date range, source filter, and page number, all persisted in the
 * route's search params. Backfills the default date range into the URL on
 * first mount so the range is always explicit and shareable/bookmarkable.
 */
export function useConsultationHistoryFilters() {
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

  return { fromDate, toDate, sourceFilter, page, updateDateRange, updateSourceFilter, setPage };
}
