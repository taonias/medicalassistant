import { useState } from 'react';
import { HistoryDateRangePicker } from './HistoryDateRangePicker';
import type { ConsultationSourceFilter } from '../utils/consultationSourceFilter';

interface Props {
  fromDate: string;
  toDate: string;
  sourceFilter: ConsultationSourceFilter;
  onDateRangeChange: (fromDate: string, toDate: string) => void;
  onSourceFilterChange: (next: ConsultationSourceFilter) => void;
}

export function ConsultationFiltersPanel({
  fromDate,
  toDate,
  sourceFilter,
  onDateRangeChange,
  onSourceFilterChange,
}: Props) {
  const [filtersExpanded, setFiltersExpanded] = useState(false);

  return (
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
              onChange={onDateRangeChange}
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
                  onClick={() => onSourceFilterChange(option.value)}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </details>
  );
}
