import { useEffect, useId, useMemo, useRef, useState } from 'react';
import {
  formatHistoryRangeLabel,
  getMonthMatrix,
  isValidHistoryRangePair,
  parseDateInput,
  shiftMonth,
  toDateInputValue,
} from '../utils/historyDateRange';
import { CalendarMonthToolbar } from './CalendarMonthToolbar';

interface Props {
  fromDate: string;
  toDate: string;
  onChange: (fromDate: string, toDate: string) => void;
  /** When true, omit the in-trigger "Period" label (use an external label instead). */
  hideLabel?: boolean;
}

const WEEKDAYS = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

export function HistoryDateRangePicker({ fromDate, toDate, onChange, hideLabel }: Props) {
  const today = useMemo(() => toDateInputValue(), []);
  const listboxId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [draftStart, setDraftStart] = useState<string | null>(null);
  const [hoverDate, setHoverDate] = useState<string | null>(null);

  const initialMonth = parseDateInput(fromDate);
  const [visibleYear, setVisibleYear] = useState(initialMonth.getFullYear());
  const [visibleMonth, setVisibleMonth] = useState(initialMonth.getMonth());

  const weeks = useMemo(
    () => getMonthMatrix(visibleYear, visibleMonth),
    [visibleYear, visibleMonth],
  );

  const previewEnd = draftStart ? (hoverDate ?? draftStart) : null;
  const previewFrom =
    draftStart && previewEnd
      ? previewEnd < draftStart
        ? previewEnd
        : draftStart
      : fromDate;
  const previewTo =
    draftStart && previewEnd
      ? previewEnd < draftStart
        ? draftStart
        : previewEnd
      : toDate;

  function closePicker() {
    setOpen(false);
    setDraftStart(null);
    setHoverDate(null);
  }

  useEffect(() => {
    if (!open) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') closePicker();
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    dialogRef.current?.focus();
  }, [open]);

  function isDisabled(date: string) {
    if (date > today) return true;
    if (!draftStart) return false;
    return !isValidHistoryRangePair(draftStart, date);
  }

  function isInRange(date: string) {
    return date >= previewFrom && date <= previewTo;
  }

  function isRangeEdge(date: string) {
    return date === previewFrom || date === previewTo;
  }

  function handleDayClick(date: string) {
    if (isDisabled(date)) return;

    if (!draftStart) {
      setDraftStart(date);
      setHoverDate(date);
      return;
    }

    if (!isValidHistoryRangePair(draftStart, date)) return;

    const start = date < draftStart ? date : draftStart;
    const end = date < draftStart ? draftStart : date;

    onChange(start, end);
    closePicker();
  }

  function moveMonth(delta: number) {
    const next = shiftMonth(visibleYear, visibleMonth, delta);
    const todayDate = parseDateInput(today);
    if (
      delta > 0 &&
      (next.year > todayDate.getFullYear() ||
        (next.year === todayDate.getFullYear() && next.monthIndex > todayDate.getMonth()))
    ) {
      return;
    }
    setVisibleYear(next.year);
    setVisibleMonth(next.monthIndex);
  }

  const canGoNextMonth = useMemo(() => {
    const todayDate = parseDateInput(today);
    return (
      visibleYear < todayDate.getFullYear() ||
      (visibleYear === todayDate.getFullYear() && visibleMonth < todayDate.getMonth())
    );
  }, [today, visibleMonth, visibleYear]);

  return (
    <div className="history-range-picker">
      <button
        type="button"
        className={[
          'history-range-picker__trigger',
          hideLabel ? 'history-range-picker__trigger--value-only' : undefined,
        ]
          .filter(Boolean)
          .join(' ')}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-label={hideLabel ? 'Period' : undefined}
        onClick={() => {
          setOpen((current) => !current);
          setDraftStart(null);
          setHoverDate(null);
          const focus = parseDateInput(fromDate);
          setVisibleYear(focus.getFullYear());
          setVisibleMonth(focus.getMonth());
        }}
      >
        {hideLabel ? null : (
          <span className="history-range-picker__trigger-label">Period</span>
        )}
        <span className="history-range-picker__trigger-value">
          {formatHistoryRangeLabel(fromDate, toDate)}
        </span>
      </button>

      {open ? (
        <div
          className="history-range-picker__backdrop"
          role="presentation"
          onClick={closePicker}
        >
          <div
            id={listboxId}
            ref={dialogRef}
            className="history-range-picker__dialog"
            role="dialog"
            aria-modal="true"
            aria-label="Select consultation history period"
            tabIndex={-1}
            onClick={(event) => event.stopPropagation()}
          >
            <CalendarMonthToolbar
              visibleYear={visibleYear}
              visibleMonth={visibleMonth}
              onChange={(year, monthIndex) => {
                setVisibleYear(year);
                setVisibleMonth(monthIndex);
              }}
              maxDate={today}
              onPreviousMonth={() => moveMonth(-1)}
              onNextMonth={() => moveMonth(1)}
              canGoNextMonth={canGoNextMonth}
            />

            <div className="history-range-picker__weekdays" aria-hidden="true">
              {WEEKDAYS.map((day) => (
                <span key={day}>{day}</span>
              ))}
            </div>

            <div className="history-range-picker__grid">
              {weeks.map((week, weekIndex) => (
                <div key={weekIndex} className="history-range-picker__week">
                  {week.map((date, dayIndex) => {
                    if (!date) {
                      return (
                        <span
                          key={`empty-${dayIndex}`}
                          className="history-range-picker__day history-range-picker__day--empty"
                        />
                      );
                    }

                    const disabled = isDisabled(date);
                    const inRange = isInRange(date);
                    const edge = isRangeEdge(date);
                    const isToday = date === today;

                    return (
                      <button
                        key={date}
                        type="button"
                        className={[
                          'history-range-picker__day',
                          inRange ? 'history-range-picker__day--in-range' : undefined,
                          edge ? 'history-range-picker__day--edge' : undefined,
                          isToday ? 'history-range-picker__day--today' : undefined,
                          disabled ? 'history-range-picker__day--disabled' : undefined,
                        ]
                          .filter(Boolean)
                          .join(' ')}
                        disabled={disabled}
                        aria-pressed={edge}
                        aria-label={date}
                        onMouseEnter={() => {
                          if (draftStart && !disabled) setHoverDate(date);
                        }}
                        onFocus={() => {
                          if (draftStart && !disabled) setHoverDate(date);
                        }}
                        onClick={() => handleDayClick(date)}
                      >
                        {Number(date.slice(-2))}
                      </button>
                    );
                  })}
                </div>
              ))}
            </div>

            <p className="muted history-range-picker__hint">
              {draftStart
                ? 'Select an end date. Future dates and ranges over one month are disabled.'
                : 'Select a start date, then an end date. Future dates cannot be selected.'}
            </p>
          </div>
        </div>
      ) : null}
    </div>
  );
}
