import { useMemo } from 'react';
import { parseDateInput, toDateInputValue } from '../utils/historyDateRange';

interface Props {
  visibleYear: number;
  visibleMonth: number;
  onChange: (year: number, monthIndex: number) => void;
  /** Inclusive max YYYY-MM-DD; months after this are unreachable. */
  maxDate?: string;
  /** Earliest year offered in the year jump control. */
  minYear?: number;
  onPreviousMonth: () => void;
  onNextMonth: () => void;
  canGoNextMonth: boolean;
}

const MONTH_OPTIONS = Array.from({ length: 12 }, (_, monthIndex) => ({
  value: monthIndex,
  label: new Intl.DateTimeFormat(undefined, { month: 'long' }).format(
    new Date(2000, monthIndex, 1),
  ),
}));

export function CalendarMonthToolbar({
  visibleYear,
  visibleMonth,
  onChange,
  maxDate,
  minYear = 1900,
  onPreviousMonth,
  onNextMonth,
  canGoNextMonth,
}: Props) {
  const today = useMemo(() => toDateInputValue(), []);
  const latestAllowed = maxDate ?? today;
  const limit = useMemo(() => parseDateInput(latestAllowed), [latestAllowed]);
  const maxYear = limit.getFullYear();
  const maxMonthIndex = limit.getMonth();

  const yearOptions = useMemo(() => {
    const years: number[] = [];
    for (let year = maxYear; year >= minYear; year -= 1) {
      years.push(year);
    }
    return years;
  }, [maxYear, minYear]);

  const availableMonths = useMemo(() => {
    if (visibleYear < maxYear) return MONTH_OPTIONS;
    return MONTH_OPTIONS.filter((month) => month.value <= maxMonthIndex);
  }, [maxMonthIndex, maxYear, visibleYear]);

  function jumpTo(year: number, monthIndex: number) {
    let nextYear = year;
    let nextMonth = monthIndex;

    if (nextYear > maxYear) {
      nextYear = maxYear;
      nextMonth = maxMonthIndex;
    } else if (nextYear === maxYear && nextMonth > maxMonthIndex) {
      nextMonth = maxMonthIndex;
    }

    if (nextYear < minYear) {
      nextYear = minYear;
      nextMonth = 0;
    }

    onChange(nextYear, nextMonth);
  }

  return (
    <div className="history-range-picker__toolbar">
      <button
        type="button"
        className="icon-button history-range-picker__nav"
        aria-label="Previous month"
        onClick={onPreviousMonth}
      >
        ‹
      </button>

      <div className="history-range-picker__jump">
        <label className="history-range-picker__jump-field">
          <span className="sr-only">Month</span>
          <select
            className="history-range-picker__select"
            value={visibleMonth}
            aria-label="Jump to month"
            onChange={(event) => jumpTo(visibleYear, Number(event.target.value))}
          >
            {availableMonths.map((month) => (
              <option key={month.value} value={month.value}>
                {month.label}
              </option>
            ))}
          </select>
        </label>

        <label className="history-range-picker__jump-field">
          <span className="sr-only">Year</span>
          <select
            className="history-range-picker__select history-range-picker__select--year"
            value={visibleYear}
            aria-label="Jump to year"
            onChange={(event) => jumpTo(Number(event.target.value), visibleMonth)}
          >
            {yearOptions.map((year) => (
              <option key={year} value={year}>
                {year}
              </option>
            ))}
          </select>
        </label>
      </div>

      <button
        type="button"
        className="icon-button history-range-picker__nav"
        aria-label="Next month"
        disabled={!canGoNextMonth}
        onClick={onNextMonth}
      >
        ›
      </button>
    </div>
  );
}
